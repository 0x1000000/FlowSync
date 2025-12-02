namespace FlowSync;

public interface IFlowSyncStrategy<T> : IDisposable
{
    FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        object? resourceId = null);

    void Cancel(object? resourceId = null);

    void CancelAll();
}