namespace FlowSync;

public interface IFlowSyncStarter<T>
{
    FlowSyncTaskAwaiter<T> CreateAwaiter(CancellationToken cancellationToken = default, FlowSyncTaskAwaiter<T>? follower = null);
}