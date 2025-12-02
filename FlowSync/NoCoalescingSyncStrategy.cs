namespace FlowSync;

public class NoCoalescingSyncStrategy<T> : IFlowSyncStrategy<T>
{
    public static readonly NoCoalescingSyncStrategy<T> Instance = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        object? resourceId)
    {
        return flowStarter.CreateAwaiter();
    }

    public void Cancel(object? resourceId = null)
    {
        //Do nothing
    }

    public void CancelAll()
    {
        //Do Nothing
    }

    public bool IsRunning(string? resourceId = null)
    {
        //Do nothing
        return false;
    }

    public void Dispose()
    {
        //Do nothing
    }
}