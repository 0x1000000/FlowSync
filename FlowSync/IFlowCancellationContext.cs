namespace FlowSync;

/// <summary>
/// Represents a cancellation context that combines external cancellation signals
/// with flow-level (local) cancellation based on synchronization strategy.
/// </summary>
public interface IFlowCancellationContext
{
    /// <summary>
    /// A cancellation token that is triggered when either external cancellation is requested
    /// or the operation is deemed unnecessary according to the synchronization strategy.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Indicates whether the cancellation was triggered locally by the synchronization strategy,
    /// rather than by an external cancellation request.
    /// </summary>
    bool IsCancelledLocally { get; }
}
