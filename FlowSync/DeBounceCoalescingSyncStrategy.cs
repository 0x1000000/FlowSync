using FlowSync.Utils;

namespace FlowSync;

public class DeBounceCoalescingSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private record struct Entry(
        int Id,
        FlowSyncTaskAwaiter<T> Remote,
        DateTime DateAdded,
        IFlowSyncStarter<T> Starter,
        FlowSyncTaskAwaiter<T>? CurrentAwaiter,
        CancellationTokenSource? CancellationTokenSource);

    private readonly AtomicUpdateDictionary<object, Entry> _storage = new();

    private readonly TimeSpan _duration;

    private int _counter;

    public DeBounceCoalescingSyncStrategy(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Duration must be greater than zero.");
        }
        this._duration = duration;
    }

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        object? resourceId = null)
    {
        resourceId ??= AtomicUpdateDictionary.DefaultKey;

        var (result, awaiterToCancel) = this._storage
            .AddOrUpdate(
                key: resourceId,
                arg: (this, flowStarter),
                addValueFactory: static (resourceId, args) =>
                {
                    var (self, flowStarter) = args;

                    var remote = new FlowSyncTaskAwaiter<T>(null, null, CancellationToken.None);

                    remote.LazyOnCompleted(() => self.OnRemoteCompleted(resourceId, remote));

                    var cancellationTokenSource = new CancellationTokenSource();
                    var newEntry = new Entry(
                        self.GenId(),
                        remote,
                        DateTime.Now,
                        flowStarter,
                        null,
                        cancellationTokenSource
                    );

                    Task.Delay(self._duration, cancellationTokenSource.Token)
                        .ContinueWith(t => self.OnTimer(t, resourceId, newEntry));

                    return (newEntry, null);
                },
                updateValueFactory: static (key, args, existingEntry) =>
                {
                    var (self, flowStarter) = args;

                    var replacingEntry = new Entry(
                        self.GenId(),
                        existingEntry.Remote,
                        DateTime.Now,
                        flowStarter,
                        null,
                        existingEntry.CancellationTokenSource ?? new CancellationTokenSource()
                    );

                    FlowSyncTaskAwaiter<T>? awaiterToCancel = null;
                    if (existingEntry.CurrentAwaiter != null)
                    {
                        awaiterToCancel = existingEntry.CurrentAwaiter;

                        //New timer is required the previous flow already running.
                        Task.Delay(self._duration, replacingEntry.CancellationTokenSource!.Token)
                            .ContinueWith(t => self.OnTimer(t, key, replacingEntry), TaskScheduler.Default);
                    }

                    return (replacingEntry, awaiterToCancel);
                }
            );

        awaiterToCancel?.Cancel(isExternalCancel: false);

        return result.Remote;
    }

    public void Cancel(object resourceId)
    {
        this._storage.TryRead(
            resourceId,
            this,
            static (_, _, e) =>
            {
                if (e.CancellationTokenSource != null)
                {
                    e.CancellationTokenSource.Cancel();
                }
                else
                {
                    e.CurrentAwaiter?.Cancel(isExternalCancel: true);
                }
            }
        );
    }

    public void CancelAll()
    {
        this._storage.ReadAll(this,
            (_, _, e) =>
            {
                if (e.CancellationTokenSource != null)
                {
                    e.CancellationTokenSource.Cancel();
                }
                else
                {
                    e.CurrentAwaiter?.Cancel(isExternalCancel: true);
                }
            });
    }

    public void Dispose() => this._storage.Dispose();

    private void OnTimer(Task delayTask, object resourceId, Entry originalEntry)
    {
        this._storage.TryUpdate(
            resourceId,
            (delayTask, originalEntry, this),
            static (resourceId, args, currentEntry) =>
            {
                var (delayTask, originalEntry, self) = args;

                if (delayTask.IsCanceled)
                {
                    originalEntry.CurrentAwaiter?.Cancel(isExternalCancel: true);
                    if (originalEntry.Id != currentEntry.Id)
                    {
                        currentEntry.CurrentAwaiter?.Cancel(isExternalCancel: true);
                    }

                    originalEntry.CancellationTokenSource?.Dispose();
                    originalEntry.Remote.Cancel(isExternalCancel: true);
                    return originalEntry with { CancellationTokenSource = null };
                }

                TimeSpan timeDiffMs;
                if (currentEntry.Id != originalEntry.Id &&
                    (timeDiffMs = currentEntry.DateAdded - originalEntry.DateAdded) > TimeSpan.Zero)
                {
                    //Was replaced - it needs a new delay
                    Task.Delay(timeDiffMs, currentEntry.CancellationTokenSource!.Token)
                        .ContinueWith(t => self.OnTimer(t, resourceId, currentEntry), TaskScheduler.Default);
                    return currentEntry;
                }

                if (currentEntry.CurrentAwaiter != null)
                {
                    throw new Exception("It was not supposed to be started");
                }

                //It is time to start
                currentEntry.CancellationTokenSource?.Dispose();
                currentEntry = currentEntry with
                {
                    CurrentAwaiter = currentEntry.Starter.CreateAwaiter(),
                    CancellationTokenSource = null
                };

                currentEntry.CurrentAwaiter.LazyOnCompleted(() => self.OnAwaiterComplete(resourceId, currentEntry));
                currentEntry.Remote.OnStarted(
                    isCompleted =>
                    {
                        if (!isCompleted)
                        {
                            self.OnRemoteStarted(resourceId, currentEntry);
                        }
                    }
                );
                return currentEntry;
            },
            out _
        );
    }

    private void OnRemoteStarted(object resourceId, Entry originalEntry)
    {
        this._storage.TryRead(
            resourceId,
            originalEntry,
            static (_, originalEntry, currentEntry) =>
            {
                if (originalEntry.Id == currentEntry.Id)
                {
                    originalEntry.CurrentAwaiter?.Start();
                }
            }
        );
    }

    private void OnAwaiterComplete(object resourceId, Entry originalEntry)
    {
        this._storage.TryRead(
            resourceId,
            originalEntry,
            static (_, originalEntry, currentEntry) =>
            {
                if (originalEntry.Id == currentEntry.Id)
                {
                    originalEntry.CurrentAwaiter!.TryToSetCompleted(originalEntry.Remote);
                }
            }
        );
    }

    private void OnRemoteCompleted(object resourceId, FlowSyncTaskAwaiter<T> remote)
    {
        if (!this._storage.TryScheduleRemoval(resourceId, currentEntry => currentEntry.Remote == remote))
        {
            Task.Run(
                () =>
                {
                    this._storage.TryScheduleRemoval(
                        resourceId,
                        currentEntry => currentEntry.Remote == remote
                    );
                }
            );
        }
    }

    private int GenId() => Interlocked.Increment(ref this._counter);
}

