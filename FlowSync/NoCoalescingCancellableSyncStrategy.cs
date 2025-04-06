using FlowSync.Utils;

namespace FlowSync;

public class NoCoalescingCancellableSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private readonly AtomicUpdateDictionary<string, CancellationTokenSource> _storage = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        string? resourceId)
    {
        var key = resourceId ?? string.Empty;
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

    public void Cancel(string? resourceId = null)
    {
        this._storage.TryUpdate(
            key: resourceId ?? string.Empty,
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


    private FlowSyncTaskAwaiter<T> SubscribeRemoval(string key, FlowSyncTaskAwaiter<T> awaiter, CancellationTokenSource source)
    {
        awaiter.LazyOnCompleted(
            () => this._storage.TryRemove(key, currentSource => currentSource == source)
        );
        return awaiter;
    }

    public void Dispose()
    {
        this._storage.Dispose();
    }
}
