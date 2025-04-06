namespace FlowSync;

public interface IFlowCancellationContext
{
    CancellationToken CancellationToken { get; }

    bool IsCancelledLocally { get; }
}
