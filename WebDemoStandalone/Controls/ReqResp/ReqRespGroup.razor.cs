using FlowSync;
using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespGroup
{
    private readonly HashSet<int> _completedActorIds = [];
    private IFlowSyncStrategy<int>? _syncStrategy;

    public ReqRespGroup()
    {
    }

    [Parameter]
    public IFlowSyncStrategy<int>? SyncStrategy
    {
        get => this._syncStrategy;
        set
        {
            if (this._syncStrategy != value)
            {
                this._syncStrategy = value;
                if (value != null)
                {
                    this.InitStoryLines(value);
                }
            }
        }
    }

    private void InitStoryLines(IFlowSyncStrategy<int> syncStrategy)
    {
        var processingTime = TimeSpan.FromMilliseconds(6400);

        // 2x slower pacing for clearer observation:
        // - Simultaneous starts (actors 3 and 4 at 5200ms)
        // - Long gap before second request per actor
        // - Late second phase requests
        this.StoryLines =
        [
            new StoryLine(
                [
                    new StoryRequest(TimeSpan.FromMilliseconds(1600), 10, processingTime),
                    new StoryRequest(TimeSpan.FromMilliseconds(7600), 11, processingTime)
                ],
                syncStrategy
            ),
            new StoryLine(
                [
                    new StoryRequest(TimeSpan.FromMilliseconds(3400), 20, processingTime),
                    new StoryRequest(TimeSpan.FromMilliseconds(4400), 21, processingTime)
                ],
                syncStrategy
            ),
            new StoryLine(
                [
                    new StoryRequest(TimeSpan.FromMilliseconds(5200), 30, processingTime),
                    new StoryRequest(TimeSpan.FromMilliseconds(4400), 31, processingTime)
                ],
                syncStrategy
            ),
            new StoryLine(
                [
                    new StoryRequest(TimeSpan.FromMilliseconds(5200), 40, processingTime),
                    new StoryRequest(TimeSpan.FromMilliseconds(4800), 41, processingTime)
                ],
                syncStrategy
            ),
            new StoryLine(
                [
                    new StoryRequest(TimeSpan.FromMilliseconds(8400), 50, processingTime),
                    new StoryRequest(TimeSpan.FromMilliseconds(5200), 51, processingTime)
                ],
                syncStrategy
            ),
        ];
    }

    protected IReadOnlyList<StoryLine>? StoryLines { get; private set; }

    protected async Task OnActorStoryLineCompleted(int actorId)
    {
        lock (this._completedActorIds)
        {
            if (!this._completedActorIds.Add(actorId))
            {
                return;
            }

            if (this._completedActorIds.Count < this.StoryLines.Count)
            {
                return;
            }
            this._completedActorIds.Clear();
        }

        await Task.Delay(TimeSpan.FromSeconds(6));

        this.StoryLines = this.StoryLines.Select(s=>s.ToNewIteration()).ToList();

        await this.InvokeAsync(this.StateHasChanged);
    }
}
