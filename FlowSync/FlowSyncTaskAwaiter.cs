using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace FlowSync;

public sealed class FlowSyncTaskAwaiter<T> : INotifyCompletion, IFlowCancellationContext
{
    private readonly CancellationTokenSource _syncObj = new();

    private FlowSyncTaskAwaiter<T>? _followerRef;

    private bool _isFollower;

    [MaybeNull] private T _result;

    private Exception? _exception;

    private IAsyncStateMachine? _asyncStateMachine;

    private Action? _continuation;

    private Action<bool>? _onStarted;

    private bool _isCompleted;

    private bool? _isExternalCancel;

    private CancellationTokenRegistration? _cancellationTokenRegistration;

    internal FlowSyncTaskAwaiter(
        IAsyncStateMachine? asyncStateMachine,
        FlowSyncTaskAwaiter<T>? followerRef,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            this._cancellationTokenRegistration = cancellationToken.Register(this.OnExternalTokeCancellation);
        }

        this._asyncStateMachine = asyncStateMachine;
        this._followerRef = followerRef;
        followerRef?.CancelInternalFlowAndSetAsFollower();
    }

    public FlowSyncTaskAwaiter<T> GetAwaiter() => this;

    CancellationToken IFlowCancellationContext.CancellationToken => this.CancellationToken;

    bool IFlowCancellationContext.IsCancelledLocally
    {
        get
        {
            lock (_syncObj)
            {
                return this._isExternalCancel.HasValue && !this._isExternalCancel.Value;
            }
        }
    }

    /// Retrieves the result of the asynchronous operation.
    ///
    /// If the operation completed with an exception, it will be rethrown here.
    /// If the operation has not yet completed, an exception is thrown.
    ///
    /// <returns>The result of the operation.</returns>
    /// <exception cref="Exception">Thrown if the operation has not completed.</exception>
    /// <exception cref="ExceptionDispatchInfo">Rethrows any exception that occurred during execution.</exception>
    public T GetResult()
    {
        lock (this._syncObj)
        {
            if (this._exception != null)
            {
                ExceptionDispatchInfo.Throw(this._exception);
            }

            if (!this._isCompleted)
            {
                throw new Exception("Not Completed");
            }

            return this._result!;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the operation has completed execution.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (this._syncObj)
            {
                return this._isCompleted;
            }
        }
    }

    /// Starts the execution of the async state machine associated with this awaiter.
    ///
    /// If <paramref name="startAsynchronously"/> is <c>true</c>, the state machine is scheduled to run
    /// on the thread pool to avoid blocking the current thread. If <c>false</c>, the state machine is
    /// executed synchronously on the calling thread.
    ///
    /// This method is safe to call multiple times, but only the first invocation will execute the state machine.
    /// Subsequent calls will be ignored once the state machine has started.
    ///
    /// <param name="startAsynchronously">If true, runs the state machine on the thread pool; otherwise runs it inline.</param>
    /// <returns>The same awaiter instance.</returns>
    public FlowSyncTaskAwaiter<T> Start(bool startAsynchronously = false)
    {
        if (startAsynchronously)
        {
            lock (this._syncObj)
            {
                if (this._asyncStateMachine == null)
                {
                    //Already started.
                    return this;
                }
            }
            ThreadPool.QueueUserWorkItem(static self => self.Start(), this, true);
            return this;
        }

        lock (this._syncObj)
        {
            if (this._asyncStateMachine != null)
            {
                try
                {
                    this._asyncStateMachine.MoveNext();
                }
                finally
                {
                    this._asyncStateMachine = null;
                }

            }
        }
        return this;
    }

    /// <summary>
    /// Registers a continuation to be invoked when the operation completes.
    /// Note: This initiates the asynchronous operation itself.
    /// </summary>
    public void OnCompleted(Action continuation)
    {
        lock (this._syncObj)
        {
            if (this._asyncStateMachine != null)
            {
                try
                {
                    this._asyncStateMachine.MoveNext();
                }
                finally
                {
                    if (this._onStarted != null)
                    {
                        this._onStarted(false);
                        this._onStarted = null;
                    }

                    this._asyncStateMachine = null;
                }
            }

            if (this._continuation == null)
            {
                this._continuation = continuation;
            }
            else
            {
                this._continuation += continuation;
            }

            this.LockUnsafeTryCallContinuation();
        }
    }

    /// <summary>
    /// Registers a continuation to be invoked when the operation completes.
    /// Note: This does not initiate the asynchronous operation itself.
    /// </summary>
    public void LazyOnCompleted(Action continuation)
    {
        lock (this._syncObj)
        {
            if (this._continuation == null)
            {
                this._continuation = continuation;
            }
            else
            {
                this._continuation += continuation;
            }

            this.LockUnsafeTryCallContinuation();
        }
    }

    /// <summary>
    /// Registers a handler to be invoked when the operation starts.
    /// </summary>
    public void OnStarted(Action<bool> onStarted)
    {
        lock (this._syncObj)
        {
            if (this._isCompleted)
            {
                onStarted(true);
            }
            else if (this._continuation != null)
            {
                onStarted(false);
            }
            else
            {
                if (this._onStarted == null)
                {
                    this._onStarted = onStarted;
                }
                else
                {
                    this._onStarted += onStarted;
                }
            }
        }
    }

    internal CancellationToken CancellationToken => this._syncObj.Token;

    internal void SetResult(T result, bool fromLeaderAwaiter = false)
    {
        FlowSyncTaskAwaiter<T>? callFollower = null;
        lock (this._syncObj)
        {
            if (!this._isCompleted)
            {
                if (!this._isFollower || fromLeaderAwaiter)
                {
                    this._result = result;
                    callFollower = this._followerRef;
                    this.LockUnsafeSetCompletedAndCleanUp();
                    this.LockUnsafeTryCallContinuation();
                }
            }
        }

        callFollower?.SetResult(result, fromLeaderAwaiter: true);
    }

    internal void SetException(Exception exception, bool fromLeaderAwaiter = false)
    {
        FlowSyncTaskAwaiter<T>? callFollower = null;
        lock (this._syncObj)
        {
            if (!this._isCompleted)
            {
                if (!this._isFollower || fromLeaderAwaiter)
                {
                    this._exception = exception;
                    callFollower = this._followerRef;
                    this.LockUnsafeSetCompletedAndCleanUp();
                    this.LockUnsafeTryCallContinuation();
                }
            }
        }

        callFollower?.SetException(exception, fromLeaderAwaiter: true);
    }

    internal void Cancel(bool isExternalCancel)
    {
        FlowSyncTaskAwaiter<T>? callFollower = null;
        lock (this)
        {
            if (!this._isCompleted)
            {
                this._isExternalCancel = isExternalCancel;
                this._isCompleted = true;//Ignore fallback from this._syncObj.Cancel();
                this._syncObj.Cancel();
                //if (!this._isFollower)
                {
                    this._exception = new OperationCanceledException();
                    callFollower = this._followerRef;
                    this.LockUnsafeSetCompletedAndCleanUp();
                    this.LockUnsafeTryCallContinuation();
                }
            }
        }

        callFollower?.Cancel(isExternalCancel);
    }

    internal void TryToSetCompleted(FlowSyncTaskAwaiter<T> target)
    {
        lock (this._syncObj)
        {
            if (this._isCompleted)
            {
                lock (target._syncObj)
                {
                    if (!target._isCompleted)
                    {
                        if (this._exception != null)
                        {
                            target.SetException(this._exception);
                        }
                        else
                        {
                            target.SetResult(this._result!);
                        }
                    }
                }
            }
        }
    }

    private void OnExternalTokeCancellation()
    {
        lock (this._syncObj)
        {
            if (!this._isCompleted)
            {
                this._isExternalCancel = true;
                this._syncObj.Cancel();
            }
        }
    }

    private void CancelInternalFlowAndSetAsFollower()
    {
        lock (this._syncObj)
        {
            if (this._isFollower)
            {
                throw new Exception("This awaiter already follows another awaiter");
            }

            this._isFollower = true;

            if (!this._isCompleted)
            {
                this._cancellationTokenRegistration?.Dispose();
                this._cancellationTokenRegistration = null;
                this._asyncStateMachine = null;
                this._isExternalCancel = false;
                this._syncObj.Cancel();
            }
        }
    }

    private void LockUnsafeSetCompletedAndCleanUp()
    {
        this._isCompleted = true;

        //Clean up
        this._cancellationTokenRegistration?.Dispose();
        this._cancellationTokenRegistration = null;
        this._followerRef = null;
        this._asyncStateMachine = null;
    }

    private void LockUnsafeTryCallContinuation()
    {
        if (this._isCompleted && this._continuation != null)
        {
            try
            {
                this._continuation();
            }
            finally
            {
                this._continuation = null;
            }
        }
    }
}
