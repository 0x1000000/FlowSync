namespace WebDemoStandalone.Controls.ReqResp.Models;

public record StoryEvent(
    TimeSpan Delay,
    int Value,
    TimeSpan ProcessingTime
);
