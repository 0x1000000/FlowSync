using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespGroup : IDisposable
{
    private readonly object _syncRoot = new();
    private ActorStoryGroup? _actorStories;
    private CancellationTokenSource? _pbCancellationTokenSource;
    private int _progressPrc;

    [Parameter]
    public ActorStoryGroup? ActorStories
    {
        get => this._actorStories;
        set
        {
            if (this._actorStories != value)
            {
                this._actorStories = value;
                if (value != null)
                {
                    this.StartProgressBar(value);
                }
                else
                {
                    this.StopProgressBar();
                }
            }
        }
    }

    protected string ProgressStyle => $"width: {this._progressPrc}%;";

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
            var delay = TimeSpan.FromSeconds(2);

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

            var tasks = group.Stories
                .Select(x => x.StoryLine.ReplayCoordinator.WaitWithoutCountingAsync(cancellationToken).AsTask())
                .ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (tasks.Any(r => r.IsFaulted || !r.Result))
            {
                break;
            }

            this._progressPrc = 0;
            _ = this.InvokeAsync(this.StateHasChanged);
        }
    }

    public void Dispose()
    {
        this.StopProgressBar();
    }
}
