namespace WebDemoStandalone.Controls.ReqResp;

public sealed record ActorStoryGroup(
    IReadOnlyList<ActorStory> Stories,
    TimeSpan EstimatedDuration
);
