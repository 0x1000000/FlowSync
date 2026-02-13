namespace FlowSync;

/// <summary>
/// Pass-through strategy that performs no coalescing and no strategy-level cancellation.
/// Each call starts its own independent flow.
/// </summary>
public class NoCoalescingSyncStrategy<T> : IFlowSyncStrategy<T>
{
    public static readonly NoCoalescingSyncStrategy<T> Instance = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncFactory<T> flowFactory,
        object? groupKey)
    {
        return flowFactory.CreateAwaiter();
    }

    public void Cancel(object? groupKey = null)
    {
        //Do nothing
    }

    public void CancelAll()
    {
        //Do Nothing
    }

    public bool IsRunning(string? groupKey = null)
    {
        //Do nothing
        return false;
    }

    public void Dispose()
    {
        //Do nothing
    }
}
