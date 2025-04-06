using FlowSync.Utils;

namespace FlowSync;

public class UseFirstCoalescingSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private readonly AtomicUpdateDictionary<string, FlowSyncTaskAwaiter<T>> _storage = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        string? resourceId)
    {
        return this._storage.AddOrUpdate(
            key: resourceId ?? string.Empty,
            arg: (self: this, flowStarter),
            addValueFactory: static (key, args)
                => args.self.SubscribeRemoval(key, args.flowStarter.CreateAwaiter()),
            updateValueFactory: static (key, arg, awaiter) => awaiter.IsCompleted
                ? arg.self.SubscribeRemoval(key, arg.flowStarter.CreateAwaiter())
                : awaiter
        );
    }

    public void Cancel(string? resourceId = null)
    {
        this._storage.TryRead(
            resourceId ?? string.Empty,
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

    private FlowSyncTaskAwaiter<T> SubscribeRemoval(string key, FlowSyncTaskAwaiter<T> awaiter)
    {
        awaiter.LazyOnCompleted(
            () => this._storage.TryRemove(key, currentAwaiter=> currentAwaiter == awaiter)
        );
        return awaiter;
    }

    public void Dispose()
    {
        this._storage.Dispose();
    }
}