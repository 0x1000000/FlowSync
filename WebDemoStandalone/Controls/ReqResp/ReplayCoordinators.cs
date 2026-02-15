namespace WebDemoStandalone.Controls.ReqResp;

public interface IReplayCoordinator : IDisposable
{
    ValueTask<bool> WaitAsync(CancellationToken cancellationToken = default);

    ValueTask<bool> WaitWithoutCountingAsync(CancellationToken cancellationToken = default);
}

public class InfiniteReplayWithBarrier(int participants) : IReplayCoordinator
{
    private readonly AsyncCyclicBarrier _asyncCyclicBarrier = new(participants);

    public void Dispose()
    {
        this._asyncCyclicBarrier.Dispose();
    }

    public async ValueTask<bool> WaitAsync(CancellationToken cancellationToken = default)
    {
        await this._asyncCyclicBarrier.WaitAsync(cancellationToken);
        return true;
    }

    public async ValueTask<bool> WaitWithoutCountingAsync(CancellationToken cancellationToken = default)
    {
        await this._asyncCyclicBarrier.WaitWithoutCountingAsync(cancellationToken);
        return true;
    }
}

