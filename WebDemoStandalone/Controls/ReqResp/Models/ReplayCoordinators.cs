namespace WebDemoStandalone.Controls.ReqResp.Models;

public interface IReplayCoordinator : IDisposable
{
    ValueTask<bool> WaitAsync(CancellationToken cancellationToken = default);
}

public class InfiniteReplayWithBarrier(int participants) : IReplayCoordinator
{
    private const int MillisecondsDelayBetweenRuns = 2000;
    private readonly AsyncCyclicBarrier _asyncCyclicBarrier = new(participants);

    public void Dispose()
    {
        this._asyncCyclicBarrier.Dispose();
    }

    public async ValueTask<bool> WaitAsync(CancellationToken cancellationToken = default)
    {
        await this._asyncCyclicBarrier.WaitAsync(cancellationToken);
        await Task.Delay(MillisecondsDelayBetweenRuns, cancellationToken);
        return true;
    }
}

