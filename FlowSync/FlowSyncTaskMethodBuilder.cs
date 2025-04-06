using System.Runtime.CompilerServices;

namespace FlowSync;

public class FlowSyncSyncTaskMethodBuilder<T> : IFlowSyncStarter<T>
{
    private FlowSyncTaskAwaiter<T>? _awaiter;

    private IAsyncStateMachine? _asyncStateMachine;

    public FlowSyncTask<T> Task { get; }

    public FlowSyncSyncTaskMethodBuilder()
    {
        this.Task = new FlowSyncTask<T>(this);
    }

    public static FlowSyncSyncTaskMethodBuilder<T> Create() => new();

    public FlowSyncTaskAwaiter<T> CreateAwaiter(
        CancellationToken cancellationToken = default,
        FlowSyncTaskAwaiter<T>? follower = null)
    {
        return this._awaiter = new FlowSyncTaskAwaiter<T>(this._asyncStateMachine, follower, cancellationToken);
    }

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        //instead of stateMachine.MoveNext();
        this._asyncStateMachine = stateMachine;
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        this._asyncStateMachine = stateMachine;
    }

    public void SetException(Exception exception) => this._awaiter!.SetException(exception);

    public void SetResult(T result) => this._awaiter!.SetResult(result);

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
        =>
            this.GenericAwaitOnCompleted(ref awaiter, ref stateMachine);

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
        =>
            this.GenericAwaitOnCompleted(ref awaiter, ref stateMachine);

    public void GenericAwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (typeof(TAwaiter) == typeof(CancellationTokenAwaiter))
        {
            ref var ctAwaiter = ref Unsafe.As<TAwaiter, CancellationTokenAwaiter>(ref awaiter);
            ctAwaiter.CancellationContext = this._awaiter!;
            stateMachine.MoveNext();
        }
        else
        {
            awaiter.OnCompleted(stateMachine.MoveNext);
        }
    }
}
