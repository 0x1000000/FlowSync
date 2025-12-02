using System.Runtime.CompilerServices;

namespace FlowSync;

public class FlowSyncTask
{
    public static CancellationTokenAwaiter GetCancellationContext()
    {
        return new CancellationTokenAwaiter();
    }

    public static FlowSyncTask<T> Create<T>(Func<CancellationToken, Task<T>> taskFactory)
    {
        return new FlowSyncTask<T>(new FlowSyncTaskFactory<T>(taskFactory));
    }

    private class FlowSyncTaskFactory<T> : IFlowSyncStarter<T>, IAsyncStateMachine
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

[AsyncMethodBuilder(typeof(FlowSyncSyncTaskMethodBuilder<>))]
public readonly struct FlowSyncTask<T>
{
    private readonly IFlowSyncStarter<T> _starter;

    internal FlowSyncTask(IFlowSyncStarter<T> starter)
    {
        this._starter = starter;
    }

    public FlowSyncTaskAwaiter<T> CoalesceUsing(IFlowSyncStrategy<T> syncStrategy, object? resourceId = null)
    {
        return syncStrategy.EnterSyncSection(this._starter, resourceId);
    }
}