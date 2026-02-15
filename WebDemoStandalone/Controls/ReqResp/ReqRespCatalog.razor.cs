using FlowSync;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespCatalog
{
    private const int ActorCountPerStrategy = 3;
    private const int StrategyCount = 6;
    private const int TotalActorCount = ActorCountPerStrategy * StrategyCount;


    public ActorStoryGroup NoCoalescingSyncStrategy { get; }

    public ActorStoryGroup UseFirstCoalescingSyncStrategy { get; }

    public ActorStoryGroup UseLastCoalescingSyncStrategy { get; }

    public ActorStoryGroup DeBounceCoalescingSyncStrategy { get; }

    public ActorStoryGroup QueueCoalescingSyncStrategy { get; }

    public ActorStoryGroup AggCoalescingSyncStrategy { get; }

    public ReqRespCatalog()
    {
        var sharedCoordinator = new InfiniteReplayWithBarrier(TotalActorCount);

        this.NoCoalescingSyncStrategy = this.InitStoryLineForNoCoalescing(sharedCoordinator);
        this.UseFirstCoalescingSyncStrategy = this.InitStoryLineForUseFirst(sharedCoordinator);
        this.UseLastCoalescingSyncStrategy = this.InitStoryLineForUseLast(sharedCoordinator);
        this.DeBounceCoalescingSyncStrategy = this.InitStoryLineForDebounce(sharedCoordinator);
        this.QueueCoalescingSyncStrategy = this.InitStoryLineForQueue(sharedCoordinator);
        this.AggCoalescingSyncStrategy = this.InitStoryLineForAgg(sharedCoordinator);
    }

    private ActorStoryGroup InitStoryLineForNoCoalescing(IReplayCoordinator coordinator)
    {
        var strategy = new NoCoalescingSyncStrategy<int>();


        return this.CreateStoryLine(
            strategy,
            coordinator,
            [E(1000, 11, 4000), E(1500, 12, 3000), E(4000, 13, 3000), E(1500, 14, 1500),],
            [E(2000, 21, 3000), E(6000, 22, 5500),],
            [E(3500, 31, 1500), E(3000, 32, 1500), E(5500, 33, 1500),],
            TimeSpan.FromSeconds(20)

        );
    }

    private ActorStoryGroup InitStoryLineForUseFirst(IReplayCoordinator coordinator)
    {
        var strategy = new UseFirstCoalescingSyncStrategy<int>();

        return this.CreateStoryLine(
            strategy,
            coordinator,
            [E(1000, 11, 4000), E(1500, 12, 3000), E(4000, 13, 3000), E(1500, 14, 1500),],
            [E(2000, 21, 3000), E(6000, 22, 5500),],
            [E(3500, 31, 1500), E(3000, 32, 1500), E(5500, 33, 1500),],
            TimeSpan.FromSeconds(20)
        );
    }

    private ActorStoryGroup InitStoryLineForUseLast(IReplayCoordinator coordinator)
    {
        var strategy = new UseLastCoalescingSyncStrategy<int>();

        return this.CreateStoryLine(
            strategy,
            coordinator,
            [E(1000, 11, 4000), E(1500, 12, 3000), E(4000, 13, 3000), E(1500, 14, 1500),],
            [E(2000, 21, 3000), E(6000, 22, 5500),],
            [E(3500, 31, 1500), E(3000, 32, 1500), E(5500, 33, 1500),],
            TimeSpan.FromSeconds(20)
        );
    }

    private ActorStoryGroup InitStoryLineForDebounce(IReplayCoordinator coordinator)
    {
        var strategy = new DeBounceCoalescingSyncStrategy<int>(TimeSpan.FromMilliseconds(1500));

        return this.CreateStoryLine(
            strategy,
            coordinator,
            [E(1000, 11, 5500), E(1000, 12, 1500), E(4000, 13, 3500),],
            [E(2000, 21, 4500), E(1500, 22, 1000),],
            [E(2500, 31, 4000), E(9000, 32, 1000),],
            TimeSpan.FromSeconds(20)

        );
    }

    private ActorStoryGroup InitStoryLineForQueue(IReplayCoordinator coordinator)
    {
        var strategy = new QueueCoalescingSyncStrategy<int>();

        // Fewer requests and shorter work still show queue behavior.
        return this.CreateStoryLine(
            strategy,
            coordinator,
            [E(1000, 11, 2000), E(4000, 12, 3000),],
            [E(2000, 21, 1500), E(2000, 22, 2000), E(6500, 23, 2500),],
            [E(4000, 31, 4000),],
            TimeSpan.FromSeconds(20)

        );
    }

    private ActorStoryGroup InitStoryLineForAgg(IReplayCoordinator coordinator)
    {
        var strategy = new AggCoalescingSyncStrategy<int, int, List<int>>(
            seedFactory: (acc, _) => acc ?? [],
            aggregator: (acc, value) =>
            {
                acc.Add(value);
                return acc;
            },
            bufferTime: TimeSpan.FromMilliseconds(1500)
        );

        // Aggregation-focused timeline: staggered bursts merged by buffer windows.
        return this.CreateAggStoryLine(
            strategy,
            coordinator,
            [E(1000, 1, 5500), E(1000, 1, 1500), E(4000, 1, 3500),],
            [E(2000, 2, 4500), E(1500, 2, 1000),],
            [E(2500, 3, 4000), E(9000, 3, 1000),],
            TimeSpan.FromSeconds(20)

        );
    }

    private ActorStoryGroup CreateStoryLine(
        IFlowSyncStrategy<int> syncStrategy,
        IReplayCoordinator coordinator,
        IReadOnlyList<StoryEvent> actor1,
        IReadOnlyList<StoryEvent> actor2,
        IReadOnlyList<StoryEvent> actor3,
        TimeSpan estimatedDuration)
    {
        IReadOnlyList<ActorStory> stories =
        [
            new ActorStory(new StoryLine(actor1, coordinator), new ActorStorySyncStrategy.Regular(syncStrategy)),
            new ActorStory(new StoryLine(actor2, coordinator), new ActorStorySyncStrategy.Regular(syncStrategy)),
            new ActorStory(new StoryLine(actor3, coordinator), new ActorStorySyncStrategy.Regular(syncStrategy)),
        ];

        return new ActorStoryGroup(stories, estimatedDuration);
    }

    private ActorStoryGroup CreateAggStoryLine(
        IFlowSyncAggStrategy<int, int, List<int>> syncStrategy,
        IReplayCoordinator coordinator,
        IReadOnlyList<StoryEvent> actor1,
        IReadOnlyList<StoryEvent> actor2,
        IReadOnlyList<StoryEvent> actor3,
        TimeSpan estimatedDuration)
    {
        IReadOnlyList<ActorStory> stories =
        [
            new ActorStory(new StoryLine(actor1, coordinator), new ActorStorySyncStrategy.Aggregate(syncStrategy)),
            new ActorStory(new StoryLine(actor2, coordinator), new ActorStorySyncStrategy.Aggregate(syncStrategy)),
            new ActorStory(new StoryLine(actor3, coordinator), new ActorStorySyncStrategy.Aggregate(syncStrategy)),
        ];

        return new ActorStoryGroup(stories, estimatedDuration);
    }

    private static StoryEvent E(int delayMs, int value, int processingMs)
        => new(TimeSpan.FromMilliseconds(delayMs), value, TimeSpan.FromMilliseconds(processingMs));
}
