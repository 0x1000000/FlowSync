using System.Runtime.CompilerServices;

namespace FlowSync;

/// <summary>
/// Entry point for creating <see cref="FlowSyncTask{T}"/> from regular <see cref="Task"/> factories
/// and for reading flow cancellation context inside FlowSync methods.
/// </summary>
public class FlowSyncTask
{
    /// <summary>
    /// Retrieves the current flow cancellation context inside an async method that returns <see cref="FlowSyncTask{T}"/>.
    /// The returned token is canceled both for explicit external cancellation and strategy-enforced local cancellation.
    /// </summary>
    public static CancellationTokenAwaiter GetCancellationContext()
    {
        return new CancellationTokenAwaiter();
    }

    /// <summary>
    /// Wraps a regular <see cref="Task"/> factory into a <see cref="FlowSyncTask{T}"/> so it can participate in coalescing.
    /// </summary>
    public static FlowSyncTask<T> Create<T>(Func<CancellationToken, Task<T>> taskFactory)
    {
        return new FlowSyncTask<T>(new FlowSyncTaskFactory<T>(taskFactory));
    }

    private class FlowSyncTaskFactory<T> : IFlowSyncFactory<T>, IAsyncStateMachine
    {
        private readonly Func<CancellationToken, Task<T>> _taskFactory;

        private FlowSyncTaskAwaiter<T>? _flowSyncTaskAwaiter = null;

        public FlowSyncTaskFactory(Func<CancellationToken, Task<T>> taskFactory)
        {
            this._taskFactory = taskFactory;
        }

        public FlowSyncTaskAwaiter<T> CreateAwaiter(CancellationToken cancellationToken = default, FlowSyncTaskAwaiter<T>? follower = null)
        {
            this._flowSyncTaskAwaiter = new FlowSyncTaskAwaiter<T>(this, follower, cancellationToken);
            return this._flowSyncTaskAwaiter;
        }

        public void MoveNext()
        {
            var flowSyncTaskAwaiter = this._flowSyncTaskAwaiter ?? throw new Exception("Should not be null");

            this._taskFactory(flowSyncTaskAwaiter.CancellationToken).ContinueWith(
                result =>
                {
                    if (result.IsCanceled)
                    {
                        flowSyncTaskAwaiter.Cancel(false);
                    }
                    else if (result.IsFaulted)
                    {
                        flowSyncTaskAwaiter.SetException(result.Exception);
                    }
                    else if (result.IsCompletedSuccessfully)
                    {
                        flowSyncTaskAwaiter.SetResult(result.Result);
                    }
                    else
                    {
                        flowSyncTaskAwaiter.SetException(new Exception("Unknown state"));
                    }
                });
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            throw new NotImplementedException();
        }
    }

}

/// <summary>
/// Lazy awaitable flow that can be coalesced by a strategy.
/// </summary>
[AsyncMethodBuilder(typeof(FlowSyncSyncTaskMethodBuilder<>))]
public readonly struct FlowSyncTask<T>
{
    private readonly IFlowSyncFactory<T> _factory;

    internal FlowSyncTask(IFlowSyncFactory<T> factory)
    {
        this._factory = factory;
    }

    /// <summary>
    /// Enters coalescing with the strategy default group key.
    /// Returns a lazy awaiter; underlying work starts when awaited, started, or converted via <c>StartAsTask</c>.
    /// </summary>
    public FlowSyncTaskAwaiter<T> CoalesceInDefaultGroupUsing(IFlowSyncStrategy<T> syncStrategy)
    {
        return syncStrategy.EnterSyncSection(this._factory);
    }

    /// <summary>
    /// Enters coalescing for the specified group key.
    /// Returns a lazy awaiter; underlying work starts when awaited, started, or converted via <c>StartAsTask</c>.
    /// </summary>
    public FlowSyncTaskAwaiter<T> CoalesceInGroupUsing(IFlowSyncStrategy<T> syncStrategy, object groupKey)
    {
        if (groupKey == null)
        {
            throw new ArgumentNullException(nameof(groupKey));
        }
        return syncStrategy.EnterSyncSection(this._factory, groupKey);
    }

    /// <summary>
    /// Starts this flow without coalescing and returns it as <see cref="Task{TResult}"/>.
    /// </summary>
    public Task<T> StartAsTask(bool startAsynchronously = false, bool runContinuationsAsynchronously = true)
    {
        return this.CreateWithoutCoalescing().StartAsTask(startAsynchronously, runContinuationsAsynchronously);
    }

    internal FlowSyncTaskAwaiter<T> CreateWithoutCoalescing()
    {
        return this._factory.CreateAwaiter();
    }
}
