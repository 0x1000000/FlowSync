namespace FlowSync;

public interface IFlowSyncAggStarter<T, in TArg>
{
    FlowSyncTaskAwaiter<T> CreateAwaiter(TArg arg, CancellationToken cancellationToken = default);
}
