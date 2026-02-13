using FlowSync.Utils;

namespace FlowSync;

/// <summary>
/// Coalescing strategy where the most recent overlapping call for a group takes over,
/// and earlier in-flight calls become followers of the latest call.
/// </summary>
public class UseLastCoalescingSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private readonly AtomicUpdateDictionary<object, FlowSyncTaskAwaiter<T>> _storage = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncFactory<T> flowFactory,
        object? groupKey)
    {
        return this._storage.AddOrUpdate(
            key: groupKey ?? AtomicUpdateDictionary.DefaultKey,
            arg: (self: this, flowStarter: flowFactory),
            addValueFactory: static (key, args)
                => args.self.SubscribeRemoval(key, args.flowStarter.CreateAwaiter()),
            updateValueFactory: static (key, args, previous) => args.self.SubscribeRemoval(
                key,
                previous.IsCompleted
                    ? args.flowStarter.CreateAwaiter()
                    : args.flowStarter.CreateAwaiter(follower: previous)
            )
        );
    }

    public void Cancel(object groupKey)
    {
        this._storage.TryRead(
            groupKey,
            this,
            static (_, _, awaiter) => awaiter.Cancel(isExternalCancel: true)
        );
    }

    public void CancelAll()
    {
        this._storage.ReadAll(
            this,
            static (_, _, awaiter) => awaiter.Cancel(isExternalCancel: true)
        );
    }

    private FlowSyncTaskAwaiter<T> SubscribeRemoval(object key, FlowSyncTaskAwaiter<T> awaiter)
    {
        awaiter.LazyOnCompleted(
            () => this._storage.TryScheduleRemoval(key, currentAwaiter => currentAwaiter == awaiter)
        );
        return awaiter;
    }

    public void Dispose()
    {
        this._storage.Dispose();
    }
}

