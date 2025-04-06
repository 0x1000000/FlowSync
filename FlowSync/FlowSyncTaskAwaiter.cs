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

    internal CancellationToken CancellationToken => this._syncObj.Token;

    public bool IsCancelledLocally
    {
        get
        {
            lock (_syncObj)
            {
                return this._isExternalCancel.HasValue && !this._isExternalCancel.Value;
            }
        }
    }

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

    public FlowSyncTaskAwaiter<T> Start(bool startAsynchronously = false)
    {
        if (startAsynchronously)
        {
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

    CancellationToken IFlowCancellationContext.CancellationToken => this.CancellationToken;
}
