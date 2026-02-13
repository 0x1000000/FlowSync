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

[AsyncMethodBuilder(typeof(FlowSyncSyncTaskMethodBuilder<>))]
public readonly struct FlowSyncTask<T>
{
    private readonly IFlowSyncFactory<T> _factory;

    internal FlowSyncTask(IFlowSyncFactory<T> factory)
    {
        this._factory = factory;
    }

    public FlowSyncTaskAwaiter<T> CoalesceInDefaultGroupUsing(IFlowSyncStrategy<T> syncStrategy)
    {
        return syncStrategy.EnterSyncSection(this._factory);
    }

    public FlowSyncTaskAwaiter<T> CoalesceInGroupUsing(IFlowSyncStrategy<T> syncStrategy, object groupKey)
    {
        if (groupKey == null)
        {
            throw new ArgumentNullException(nameof(groupKey));
        }
        return syncStrategy.EnterSyncSection(this._factory, groupKey);
    }

    public Task<T> StartAsTask(bool startAsynchronously = false, bool runContinuationsAsynchronously = true)
    {
        return this.CreateWithoutCoalescing().StartAsTask(startAsynchronously, runContinuationsAsynchronously);
    }

    internal FlowSyncTaskAwaiter<T> CreateWithoutCoalescing()
    {
        return this._factory.CreateAwaiter();
    }
}