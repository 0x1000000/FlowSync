namespace FlowSync;

public interface IFlowSyncFactory<T>
{
    FlowSyncTaskAwaiter<T> CreateAwaiter(CancellationToken cancellationToken = default, FlowSyncTaskAwaiter<T>? follower = null);
}