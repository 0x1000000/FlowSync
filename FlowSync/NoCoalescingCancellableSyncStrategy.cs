using FlowSync.Utils;

namespace FlowSync;

public class NoCoalescingCancellableSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private record struct Entry(HashSet<FlowSyncTaskAwaiter<T>> Awaiters, CancellationTokenSource CancellationTokenSource);
    private readonly AtomicUpdateDictionary<object, Entry> _storage = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncFactory<T> flowFactory,
        object? groupKey)
    {
        var key = groupKey ?? AtomicUpdateDictionary.DefaultKey;
        var (_, result) = this._storage.AddOrUpdate(
            key: key,
            arg: (this, flowFactory),
            addValueFactory: static (key, args) =>
            {
                var (self, flowStarter) = args;
                var cts = new CancellationTokenSource();

                var awaiter = flowStarter.CreateAwaiter(cts.Token);
                HashSet<FlowSyncTaskAwaiter<T>> flowSyncTaskAwaiters = [awaiter];
                var result = self.SubscribeRemoval(key, awaiter, flowSyncTaskAwaiters);

                return (new Entry(flowSyncTaskAwaiters, cts), result);
            },
            updateValueFactory: static (key, args, entry) =>
            {
                var (self, flowStarter) = args;
                var result = self.SubscribeRemoval(key, flowStarter.CreateAwaiter(entry.CancellationTokenSource.Token), entry.Awaiters);
                entry.Awaiters.Add(result);
                return (entry, result);
            }
        );

        return result;
    }

    public void CancelDefaultGroup() => this.Cancel(AtomicUpdateDictionary.DefaultKey);

    public void Cancel(object groupKey)
    {
        this._storage.TryUpdate(
            key: groupKey,
            arg: default(object?),
            updateValueFactory: static (_, _, entry) =>
            {
                entry.CancellationTokenSource.Cancel();
                entry.CancellationTokenSource.Dispose();
                foreach (var awaiter in entry.Awaiters)
                {
                    awaiter.Cancel(true);
                }
                return entry;
            },
            newValue: out _
        );
    }

    public void CancelAll()
    {
        this._storage.ReadAll(this,
            (_, _, entry) =>
            {
                entry.CancellationTokenSource.Cancel();
                entry.CancellationTokenSource.Dispose();
                foreach (var awaiter in entry.Awaiters)
                {
                    awaiter.Cancel(true);
                }
            });
    }

    private FlowSyncTaskAwaiter<T> SubscribeRemoval(object key, FlowSyncTaskAwaiter<T> awaiter, HashSet<FlowSyncTaskAwaiter<T>> list)
    {
        awaiter.LazyOnCompleted(
            () =>
            {
                this._storage.TryUpdate(
                    key,
                    (awaiter, list),
                    static (_, args, entry) =>
                    {
                        var (awaiter, list) = args;
                        if (entry.Awaiters == list)
                        {
                            entry.Awaiters.Remove(awaiter);
                        }

                        return entry;
                    },
                    out _
                );
                this._storage.TryScheduleRemoval(key, entry => entry.Awaiters == list && list.Count == 0);
            }
        );
        return awaiter;
    }

    public void Dispose()
    {
        this._storage.Dispose();
    }
}

