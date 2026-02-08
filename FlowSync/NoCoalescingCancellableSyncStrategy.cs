using FlowSync.Utils;

namespace FlowSync;

public class NoCoalescingCancellableSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private readonly AtomicUpdateDictionary<object, CancellationTokenSource> _storage = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        object? resourceId)
    {
        object key = resourceId ?? AtomicUpdateDictionary.DefaultKey;
        var (_, result) = this._storage.AddOrUpdate(
            key: key,
            arg: (this, flowStarter),
            addValueFactory: static (key, args) =>
            {
                var (self, flowStarter) = args;
                var cts = new CancellationTokenSource();
                var result = self.SubscribeRemoval(key, flowStarter.CreateAwaiter(cts.Token), cts);
                return (cts, result);
            },
            updateValueFactory: static (key, args, cts) =>
            {
                var (self, flowStarter) = args;
                var result = self.SubscribeRemoval(key, flowStarter.CreateAwaiter(cts.Token), cts);
                return (cts, result);
            }
        );

        return result;
    }

    public void Cancel(object? resourceId = null)
    {
        this._storage.TryUpdate(
            key: resourceId ?? AtomicUpdateDictionary.DefaultKey,
            arg: default(object?),
            updateValueFactory: static (_, _, cts) =>
            {
                cts.Cancel();
                cts.Dispose();
                return new CancellationTokenSource();
            },
            newValue: out _
        );
    }

    public void CancelAll()
    {
        this._storage.ReadAll(this,
            (_, _, cts) =>
            {
                cts.Cancel();
                cts.Dispose();
            });
    }


    private FlowSyncTaskAwaiter<T> SubscribeRemoval(object key, FlowSyncTaskAwaiter<T> awaiter, CancellationTokenSource source)
    {
        awaiter.LazyOnCompleted(
            () => this._storage.TryScheduleRemoval(key, currentSource => currentSource == source)
        );
        return awaiter;
    }

    public void Dispose()
    {
        this._storage.Dispose();
    }
}

