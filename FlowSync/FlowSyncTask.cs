using System.Runtime.CompilerServices;

namespace FlowSync;

public class FlowSyncTask
{
    public static CancellationTokenAwaiter GetCancellationContext()
    {
        return new CancellationTokenAwaiter();
    }
}

[AsyncMethodBuilder(typeof(FlowSyncSyncTaskMethodBuilder<>))]
public readonly struct FlowSyncTask<T>
{
    private readonly IFlowSyncStarter<T> _starter;

    internal FlowSyncTask(IFlowSyncStarter<T> starter)
    {
        this._starter = starter;
    }

    public FlowSyncTaskAwaiter<T> Sync(IFlowSyncStrategy<T> syncStrategy, string? resourceId = null)
    {
        return syncStrategy.EnterSyncSection(this._starter, resourceId);
    }

}