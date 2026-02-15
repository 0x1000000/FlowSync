namespace WebDemoStandalone.Controls.ReqResp.Models;

public record StoryLine(IEnumerable<StoryEvent> Events, IReplayCoordinator ReplayCoordinator)
{
    public async IAsyncEnumerable<StoryRequest> Play()
    {
        var restart = false;

        while (true)
        {
            if (restart)
            {
                yield return StoryRequest.Restart;
                restart = false;
            }

            StoryEvent? previousEvent = null;
            foreach (var storyEvent in this.Events)
            {
                if (previousEvent != null)
                {
                    yield return new(previousEvent.Value, previousEvent.ProcessingTime, null);
                }
                await Task.Delay(storyEvent.Delay);
                previousEvent = storyEvent;
            }

            if (previousEvent != null)
            {
                TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                yield return new(previousEvent.Value, previousEvent.ProcessingTime, () => tcs.TrySetResult());

                await tcs.Task;

                restart = true;
            }

            if (!await this.ReplayCoordinator.WaitAsync())
            {
                break;
            }
        }
    }
}
