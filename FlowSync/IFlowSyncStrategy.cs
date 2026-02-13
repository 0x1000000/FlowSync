using FlowSync.Utils;

namespace FlowSync;

/// <summary>
/// Defines how concurrent operations are coalesced for a given group.
/// </summary>
public interface IFlowSyncStrategy<T> : IDisposable
{
    /// <summary>
    /// Attempts to enter the coalescing section for the given group.
    /// </summary>
    /// <param name="flowFactory">Starts the underlying flow when this call becomes the active execution.</param>
    /// <param name="groupKey">Optional key used to coalesce callers for the same group.</param>
    /// <returns>An awaiter that represents the coalesced flow for this caller.</returns>
    FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncFactory<T> flowFactory,
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