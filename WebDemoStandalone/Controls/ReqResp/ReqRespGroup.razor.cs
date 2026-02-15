using Microsoft.AspNetCore.Components;
using WebDemoStandalone.Controls.ReqResp.Models;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespGroup : IDisposable
{
    private readonly object _syncRoot = new();
    private ActorStoryGroup? _activeGroup;
    private CancellationTokenSource? _pbCancellationTokenSource;
    private int _progressPrc;
    private TaskCompletionSource? _waiterForCycle;
    private int _waiterForCycleId;

    [Parameter]
    public ActorStoryGroup? ActorStories { get; set; }

    protected string ProgressStyle => $"width: {this._progressPrc}%;";

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(this._activeGroup, this.ActorStories))
        {
            return;
        }

        TaskCompletionSource? previousWaiter;
        lock (this._syncRoot)
        {
            this._activeGroup = this.ActorStories;
            previousWaiter = this._waiterForCycle;
            this._waiterForCycle = null;
            this._waiterForCycleId = 0;
        }

        previousWaiter?.TrySetCanceled();
        this.StopProgressBar();
    }

    private void StartProgressBar(ActorStoryGroup group)
    {
        CancellationTokenSource? previousCts;

        lock (this._syncRoot)
        {
            previousCts = this._pbCancellationTokenSource;
            this._pbCancellationTokenSource = new CancellationTokenSource();
            this._progressPrc = 0;
        }

        previousCts?.Cancel();
        previousCts?.Dispose();

        _ = this.RunProgressBar(group, this._pbCancellationTokenSource.Token);
        _ = this.InvokeAsync(this.StateHasChanged);
    }

    private void StopProgressBar()
    {
        CancellationTokenSource? cts;

        lock (this._syncRoot)
        {
            cts = this._pbCancellationTokenSource;
            this._pbCancellationTokenSource = null;
            this._progressPrc = 0;
        }

        cts?.Cancel();
        cts?.Dispose();
        _ = this.InvokeAsync(this.StateHasChanged);
    }

    private async Task RunProgressBar(ActorStoryGroup group, CancellationToken cancellationToken)
    {
        var targetDuration = group.EstimatedDuration > TimeSpan.Zero
            ? group.EstimatedDuration
            : TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            var start = DateTime.UtcNow;
            var end = start.Add(targetDuration);
            var delay = TimeSpan.FromSeconds(1);

            int startId;
            lock (this._syncRoot)
            {
                startId = this._waiterForCycleId;
            }

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow <= end)
            {
                var elapsed = DateTime.UtcNow - start;
                var progress = (int)Math.Round((elapsed.TotalMilliseconds / targetDuration.TotalMilliseconds) * 100);
                progress = Math.Clamp(progress, 0, 100);

                if (progress != this._progressPrc)
                {
                    this._progressPrc = progress;
                    _ = this.InvokeAsync(this.StateHasChanged);
                }

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            this._progressPrc = 100;
            _ = this.InvokeAsync(this.StateHasChanged);

            // Wait a moment at 100% before resetting.
            TaskCompletionSource? waiterForCycle = null;
            lock (this._syncRoot)
            {
                if (startId == this._waiterForCycleId)
                {
                    waiterForCycle = this._waiterForCycle;
                }
            }

            if (waiterForCycle != null)
            {
                try
                {
                    await waiterForCycle.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (TimeoutException)
                {
                    // Continue the loop if no restart arrived within the timeout.
                }
            }

            this._progressPrc = 0;
            _ = this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void OnActorRestart(int actorIndex, int cycleNo)
    {
        if (actorIndex != 0)
        {
            return;
        }

        TaskCompletionSource? previousWaiter;
        ActorStoryGroup? groupToStart;

        lock (this._syncRoot)
        {
            previousWaiter = this._waiterForCycle;
            this._waiterForCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this._waiterForCycleId = cycleNo;
            groupToStart = this._activeGroup;
        }

        if (previousWaiter == null)
        {
            if (groupToStart != null)
            {
                this.StartProgressBar(groupToStart);
            }
        }
        else
        {
            previousWaiter.TrySetResult();
        }
    }

    public void Dispose()
    {
        this.StopProgressBar();
    }
}
