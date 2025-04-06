namespace FlowSync;

public interface IFlowSyncStrategy<T> : IDisposable
{
    FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        string? resourceId = null);

    void Cancel(string? resourceId = null);

    void CancelAll();
}