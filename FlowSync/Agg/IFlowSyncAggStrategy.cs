using FlowSync.Utils;

namespace FlowSync;

public interface IFlowSyncAggStrategy<T, in TArg, out TAcc>: IDisposable
{
    FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncAggStarter<T, TAcc> flowStarter,
        TArg arg,
        object? groupKey = null);

    /// <summary>
    /// Cancels any in-flight or queued work for the default group.
    /// </summary>
    void CancelDefaultGroup() => this.Cancel(AtomicUpdateDictionary.DefaultKey);

    /// <summary>
    /// Cancels any in-flight or queued work for the specified group.
    /// </summary>
    void Cancel(object groupKey);

    /// <summary>
    /// Cancels all in-flight or queued work managed by this strategy.
    /// </summary>
    void CancelAll();
}


