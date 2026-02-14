using FlowSync;

namespace WebDemoStandalone.Controls.ReqResp;

public record StoryLine(IReadOnlyList<StoryRequest> Requests, IFlowSyncStrategy<int> SyncStrategy, int Iteration = 0)
{
    public StoryLine ToNewIteration()
    {
        unchecked
        {
            return this with { Iteration = this.Iteration + 1 };
        }
    }
}

public record StoryRequest(
    TimeSpan Delay,
    int Value,
    TimeSpan ProcessingTime
);






