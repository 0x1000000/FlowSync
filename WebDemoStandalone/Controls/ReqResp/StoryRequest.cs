namespace WebDemoStandalone.Controls.ReqResp;

public record StoryRequest(int Value, TimeSpan ProcessingTime, Action? CompletionNotifier)
{
    public bool IsRestart => this.Value == 0 && this.ProcessingTime == TimeSpan.Zero && this.CompletionNotifier == null;
}
