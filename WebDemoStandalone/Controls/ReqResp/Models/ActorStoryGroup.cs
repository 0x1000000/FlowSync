namespace WebDemoStandalone.Controls.ReqResp.Models;

public sealed record ActorStoryGroup(
    IReadOnlyList<ActorStory> Stories,
    TimeSpan EstimatedDuration
);
