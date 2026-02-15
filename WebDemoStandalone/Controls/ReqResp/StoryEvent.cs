namespace WebDemoStandalone.Controls.ReqResp;

public record StoryEvent(
    TimeSpan Delay,
    int Value,
    TimeSpan ProcessingTime
);
