using FlowSync;
using WebDemoStandalone.Controls.ReqResp.Models;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespCatalog
{
    private readonly TimeSpan _estimatedDuration = TimeSpan.FromSeconds(10);
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
            new ActorStorySyncStrategy.Regular(strategy),
            coordinator,
            CreateUseEvents(),
            this._estimatedDuration

        );
    }

    private ActorStoryGroup InitStoryLineForUseFirst(IReplayCoordinator coordinator)
    {
        var strategy = new UseFirstCoalescingSyncStrategy<int>();

        return this.CreateStoryLine(
            new ActorStorySyncStrategy.Regular(strategy),
            coordinator,
            CreateUseEvents(),
            this._estimatedDuration
        );
    }

    private ActorStoryGroup InitStoryLineForUseLast(IReplayCoordinator coordinator)
    {
        var strategy = new UseLastCoalescingSyncStrategy<int>();

        return this.CreateStoryLine(
            new ActorStorySyncStrategy.Regular(strategy),
            coordinator,
            CreateUseEvents(),
            this._estimatedDuration
        );
    }

    private ActorStoryGroup InitStoryLineForDebounce(IReplayCoordinator coordinator)
    {
        var strategy = new DeBounceCoalescingSyncStrategy<int>(TimeSpan.FromMilliseconds(1500));

        return this.CreateStoryLine(
            new ActorStorySyncStrategy.Regular(strategy),
            coordinator,
            CreateDebounceEvents(),
            this._estimatedDuration

        );
    }

    private ActorStoryGroup InitStoryLineForQueue(IReplayCoordinator coordinator)
    {
        var strategy = new QueueCoalescingSyncStrategy<int>();

        // Fewer requests and shorter work still show queue behavior.
        return this.CreateStoryLine(
            new ActorStorySyncStrategy.Regular(strategy),
            coordinator,
            CreateQueueEvents(),
            this._estimatedDuration

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
        return this.CreateStoryLine(
            new ActorStorySyncStrategy.Aggregate(strategy),
            coordinator,
            CreateAggEvents(),
            this._estimatedDuration
        );
    }

    private ActorStoryGroup CreateStoryLine(
        ActorStorySyncStrategy actorStorySyncStrategy,
        IReplayCoordinator coordinator,
        IReadOnlyList<IReadOnlyList<StoryEvent>> actors,
        TimeSpan estimatedDuration)
    {
        var stories = actors
            .Select(actor => new ActorStory(
                    new StoryLine(actor, coordinator),
                    actorStorySyncStrategy
                )
            )
            .ToList();

        return new ActorStoryGroup(stories, estimatedDuration);
    }

    private static IReadOnlyList<IReadOnlyList<StoryEvent>> CreateUseEvents()
    {
        return
        [
            [E(1000, 11, 4000), E(1500, 12, 3000), ],
            [E(2000, 21, 3000), ],
            [E(3500, 31, 1500), E(3000, 32, 1500), ],
        ];
    }

    private static IReadOnlyList<IReadOnlyList<StoryEvent>> CreateDebounceEvents()
    {
        return
        [
            [E(1000, 11, 4000), ],
            [E(2000, 21, 3000), ],
            [E(5500, 31, 2500), ],
        ];
    }

    private static IReadOnlyList<IReadOnlyList<StoryEvent>> CreateQueueEvents()
    {
        return
        [
            [E(1000, 11, 3000), ],
            [E(2000, 21, 2000), ],
            [E(3000, 31, 2000), ],
        ];
    }

    private static IReadOnlyList<IReadOnlyList<StoryEvent>> CreateAggEvents()
    {
        return
        [
            [E(1000, 1, 4000), ],
            [E(2000, 3, 3000), ],
            [E(5500, 7, 2500), ],
        ];
    }

    private static StoryEvent E(int delayMs, int value, int processingMs)
        => new(TimeSpan.FromMilliseconds(delayMs), value, TimeSpan.FromMilliseconds(processingMs));
}
