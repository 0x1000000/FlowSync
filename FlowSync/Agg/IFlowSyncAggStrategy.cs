using FlowSync.Utils;

namespace FlowSync;

/// <summary>
/// Defines how concurrent operations are coalesced for a given group when each call contributes an argument
/// to an aggregated accumulator.
/// </summary>
public interface IFlowSyncAggStrategy<T, in TArg, out TAcc>: IDisposable
{
    /// <summary>
    /// Attempts to enter the aggregate coalescing section for the given group.
    /// </summary>
    /// <param name="flowStarter">Creates the underlying awaiter for a prepared accumulator.</param>
    /// <param name="arg">Input argument contributed by the current call.</param>
    /// <param name="groupKey">Optional key used to coalesce callers for the same group.</param>
    /// <returns>An awaiter that represents the coalesced aggregated flow for this caller.</returns>
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


