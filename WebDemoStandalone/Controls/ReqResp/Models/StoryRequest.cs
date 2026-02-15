namespace WebDemoStandalone.Controls.ReqResp.Models;

public record StoryRequest(int Value, TimeSpan ProcessingTime, Action? CompletionNotifier)
{
    public bool IsRestart => this == Restart;

    public static StoryRequest Restart { get; } = new (0, TimeSpan.Zero, null);
}
