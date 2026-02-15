namespace WebDemoStandalone.Controls.ReqResp.Models;

public sealed class AsyncCyclicBarrier
{
    private readonly object _sync = new();
    private readonly int _participants;
    private int _arrived;
    private bool _isDisposed;
    private TaskCompletionSource _phase = NewPhase();

    public AsyncCyclicBarrier(int participants)
    {
        if (participants <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(participants), "Participants must be greater than zero.");
        }

        this._participants = participants;
    }

    public ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        Task phaseToAwait;

        lock (this._sync)
        {
            this.ThrowIfDisposed();

            phaseToAwait = this._phase.Task;
            this._arrived++;

            if (this._arrived == this._participants)
            {
                var completedPhase = this._phase;
                this._phase = NewPhase();
                this._arrived = 0;
                completedPhase.TrySetResult();
            }
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return new ValueTask(phaseToAwait);
        }

        return new ValueTask(phaseToAwait.WaitAsync(cancellationToken));
    }

    public ValueTask WaitWithoutCountingAsync(CancellationToken cancellationToken = default)
    {
        Task phaseToAwait;

        lock (this._sync)
        {
            this.ThrowIfDisposed();
            phaseToAwait = this._phase.Task;
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return new ValueTask(phaseToAwait);
        }

        return new ValueTask(phaseToAwait.WaitAsync(cancellationToken));
    }

    public void Dispose()
    {
        lock (this._sync)
        {
            if (this._isDisposed)
            {
                return;
            }

            this._isDisposed = true;
            this._phase.TrySetCanceled();
        }
    }

    private void ThrowIfDisposed()
    {
        if (this._isDisposed)
        {
            throw new ObjectDisposedException(nameof(AsyncCyclicBarrier));
        }
    }

    private static TaskCompletionSource NewPhase()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
