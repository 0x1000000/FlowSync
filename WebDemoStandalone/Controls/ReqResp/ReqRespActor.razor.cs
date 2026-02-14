using FlowSync;
using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespActor
{
    private StoryLine? _storyLine;
    private int _renderScheduled;

    private string _requestValue = ReqRespEmoji.Unknown;
    private string _responseValue = ReqRespEmoji.Na;
    private string _serverState = ReqRespEmoji.Sleep;
    private string _serverRequest = string.Empty;
    private bool _isRequestVisible;
    private bool _isResponseVisible;

    protected string RequestValue
    {
        get => this._requestValue;
        private set => this.SetAndScheduleRender(ref this._requestValue, value);
    }

    protected string ResponseValue
    {
        get => this._responseValue;
        private set => this.SetAndScheduleRender(ref this._responseValue, value);
    }

    protected string ServerState
    {
        get => this._serverState;
        private set => this.SetAndScheduleRender(ref this._serverState, value);
    }

    protected string ServerRequest
    {
        get => this._serverRequest;
        private set => this.SetAndScheduleRender(ref this._serverRequest, value);
    }

    protected bool IsRequestVisible
    {
        get => this._isRequestVisible;
        private set => this.SetAndScheduleRender(ref this._isRequestVisible, value);
    }

    protected bool IsResponseVisible
    {
        get => this._isResponseVisible;
        private set => this.SetAndScheduleRender(ref this._isResponseVisible, value);
    }

    [Parameter]
    public StoryLine StoryLine
    {
        get => this._storyLine!;
        set
        {
            if (this._storyLine != value)
            {
                this._storyLine = value;
                _ = this.StartStoryLine(value);
            }
        }
    }

    [Parameter]
    public int ActorId { get; set; }

    [Parameter]
    public EventCallback<int> StoryLineFinished { get; set; }

    private async Task StartStoryLine(StoryLine storyLine)
    {
        this.ServerState = ReqRespEmoji.Sleep;
        this.IsResponseVisible = false;
        this.IsRequestVisible = false;
        this.RequestValue = ReqRespEmoji.Unknown;
        this.ResponseValue = ReqRespEmoji.Unknown;
        this.ServerRequest = string.Empty;

        foreach (var request in storyLine.Requests)
        {
            await Task.Delay(request.Delay);
            this.IsResponseVisible = false;
            this.IsRequestVisible = true;
            this.RequestValue = request.Value.ToString();
            this.ResponseValue = ReqRespEmoji.Unknown;
            this.ServerRequest = string.Empty;
            this.ServerState = ReqRespEmoji.Processing;

            var result = await this.Process(request.Value, request.ProcessingTime)
                .CoalesceInDefaultGroupUsing(storyLine.SyncStrategy);

            this.IsRequestVisible = false;
            this.IsResponseVisible = true;

            this.ServerState = result != request.Value ? ReqRespEmoji.Redirected : ReqRespEmoji.Completed;

            this.ResponseValue = result.ToString();
        }

        if (this.StoryLineFinished.HasDelegate)
        {
           await this.StoryLineFinished.InvokeAsync(this.ActorId);
        }
    }

    private async FlowSyncTask<int> Process(int requestedValue, TimeSpan processingTime)
    {
        this.ServerRequest = requestedValue.ToString();

        var cc = await FlowSyncTask.GetCancellationContext();

        try
        {
            await Task.Delay(processingTime, cc.CancellationToken);
        }
        catch (OperationCanceledException e)
        {
            this.ServerRequest = string.Empty;
            this.ServerState = ReqRespEmoji.Canceled;
        }

        this.ServerRequest = string.Empty;

        return requestedValue;
    }

    private void SetAndScheduleRender<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        this.ScheduleRender();
    }

    private void ScheduleRender()
    {
        if (Interlocked.Exchange(ref this._renderScheduled, 1) == 1)
        {
            return;
        }

        _ = this.InvokeAsync(() =>
            {
                Interlocked.Exchange(ref this._renderScheduled, 0);
                this.StateHasChanged();
            }
        );
    }
}
