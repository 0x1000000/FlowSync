namespace FlowSync;

/// <summary>
/// Defines how concurrent operations are coalesced for a given resource.
/// </summary>
public interface IFlowSyncStrategy<T> : IDisposable
{
    /// <summary>
    /// Attempts to enter the coalescing section for the given resource.
    /// </summary>
    /// <param name="flowStarter">Starts the underlying flow when this call becomes the active execution.</param>
    /// <param name="resourceId">Optional key used to coalesce callers for the same resource.</param>
    /// <returns>An awaiter that represents the coalesced flow for this caller.</returns>
    FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncStarter<T> flowStarter,
        object? resourceId = null);

    /// <summary>
    /// Cancels any in-flight or queued work for the specified resource.
    /// </summary>
    /// <param name="resourceId">Optional key; when omitted, the strategy may cancel a default resource.</param>
    void Cancel(object? resourceId = null);

    /// <summary>
    /// Cancels all in-flight or queued work managed by this strategy.
    /// </summary>
    void CancelAll();
}