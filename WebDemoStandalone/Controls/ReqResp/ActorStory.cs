using FlowSync;

namespace WebDemoStandalone.Controls.ReqResp;

public abstract record ActorStorySyncStrategy
{
    public sealed record Regular(IFlowSyncStrategy<int> Strategy) : ActorStorySyncStrategy;
    public sealed record Aggregate(IFlowSyncAggStrategy<int, int, List<int>> Strategy) : ActorStorySyncStrategy;
}

public record ActorStory(StoryLine StoryLine, ActorStorySyncStrategy SyncStrategy);
