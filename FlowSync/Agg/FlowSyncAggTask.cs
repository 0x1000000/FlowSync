using System.Runtime.CompilerServices;

namespace FlowSync;

/// <summary>
/// Entry point for creating aggregated FlowSync tasks from regular <see cref="Task"/> factories.
/// </summary>
public class FlowSyncAggTask
{
    /// <summary>
    /// Wraps a regular aggregated task factory into a <see cref="FlowSyncAggTask{T,TAcc}"/>.
    /// </summary>
    public static FlowSyncAggTask<T, TAcc> Create<T, TAcc>(Func<TAcc, CancellationToken, Task<T>> taskFactory)
    {
        return new FlowSyncAggTask<T, TAcc>(new FlowSyncTaskFactory<T, TAcc>(taskFactory));
    }

    private class FlowSyncTaskFactory<T, TAcc> : IFlowSyncAggStarter<T, TAcc>, IAsyncStateMachine
    {
        private readonly Func<TAcc, CancellationToken, Task<T>> _taskFactory;

        private FlowSyncTaskAwaiter<T>? _flowSyncTaskAwaiter = null;

        private TAcc _arg = default!;

        public FlowSyncTaskFactory(Func<TAcc, CancellationToken, Task<T>> taskFactory)
        {
            this._taskFactory = taskFactory;
        }

        public FlowSyncTaskAwaiter<T> CreateAwaiter(TAcc arg, CancellationToken cancellationToken = default)
        {
            this._arg = arg;
            this._flowSyncTaskAwaiter = new FlowSyncTaskAwaiter<T>(this, null, cancellationToken);
            return this._flowSyncTaskAwaiter;
        }

        public void MoveNext()
        {
            var flowSyncTaskAwaiter = this._flowSyncTaskAwaiter ?? throw new Exception("Should not be null");

            this._taskFactory(this._arg, flowSyncTaskAwaiter.CancellationToken).ContinueWith(
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
/// Lazy awaitable aggregated flow that can be coalesced by an aggregate strategy.
/// </summary>
public readonly struct FlowSyncAggTask<T, TAcc>
{
    private readonly IFlowSyncAggStarter<T, TAcc> _starter;

    internal FlowSyncAggTask(IFlowSyncAggStarter<T, TAcc> starter)
    {
        this._starter = starter;
    }

    /// <summary>
    /// Enters aggregate coalescing with the strategy default group key.
    /// Returns a lazy awaiter.
    /// </summary>
    public FlowSyncTaskAwaiter<T> CoalesceInDefaultGroupUsing<TArg>(IFlowSyncAggStrategy<T, TArg, TAcc> syncStrategy, TArg arg)
    {
        return syncStrategy.EnterSyncSection(this._starter, arg);
    }

    /// <summary>
    /// Enters aggregate coalescing for the specified group key.
    /// Returns a lazy awaiter.
    /// </summary>
    public FlowSyncTaskAwaiter<T> CoalesceInGroupUsing<TArg>(IFlowSyncAggStrategy<T, TArg, TAcc> syncStrategy, TArg arg, object groupKey)
    {
        if (groupKey == null)
        {
            throw new ArgumentNullException(nameof(groupKey));
        }
        return syncStrategy.EnterSyncSection(this._starter, arg, groupKey);
    }
}
