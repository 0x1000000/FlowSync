using System.Runtime.CompilerServices;

namespace FlowSync;

//So far I cannot convert it into a struct since:<>u__1 = awaiter
public class CancellationTokenAwaiter : INotifyCompletion
{
    internal CancellationTokenAwaiter()
    {
    }

    internal IFlowCancellationContext? CancellationContext;

    public IFlowCancellationContext GetResult() => this.CancellationContext ?? throw new InvalidOperationException("CancellationContext is supposed to be initialized here");

    public bool IsCompleted => false; //Always go to AwaitOnCompleted

    public CancellationTokenAwaiter GetAwaiter() => this;

    public void OnCompleted(Action continuation)
    {
        throw new InvalidOperationException($"You can get the cancellation context only async methods that returns {nameof(FlowSyncTask)}");
    }
}
