using FlowSync;
using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespActor
{
    private ActorStory? _actorStory;
    private int _renderScheduled;
    private int _actorPulseToken;

    private string _requestValue = ReqRespEmoji.Values.Unknown;
    private string _responseValue = ReqRespEmoji.Values.Na;
    private string _serverState = ReqRespEmoji.Statuses.Sleep;
    private string _serverRequestedValue = string.Empty;
    private bool _isRequestVisible;
    private bool _isResponseVisible;
    private bool _isActorHighlighted;

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

    protected string ServerRequestedValue
    {
        get => this._serverRequestedValue;
        private set => this.SetAndScheduleRender(ref this._serverRequestedValue, value);
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

    protected bool IsActorHighlighted
    {
        get => this._isActorHighlighted;
        private set => this.SetAndScheduleRender(ref this._isActorHighlighted, value);
    }

    [Parameter]
    public ActorStory ActorStory
    {
        get => this._actorStory!;
        set
        {
            if (this._actorStory != value)
            {
                this._actorStory = value;
                _ = this.StartStoryLine(value);
            }
        }
    }

    [Parameter] public int ActorIndex { get; set; }

    private async Task StartStoryLine(ActorStory actorStory)
    {
        Interlocked.Increment(ref this._actorPulseToken);
        this.IsActorHighlighted = false;
        this.SetInitialState();

        await foreach (var request in actorStory.StoryLine.Play())
        {
            if (request.IsRestart)
            {
                this.SetInitialState();
                continue;
            }
            _ = this.PulseActorSymbolAsync();
            this.IsResponseVisible = false;
            this.IsRequestVisible = true;
            this.RequestValue = request.Value.ToString();
            this.ResponseValue = ReqRespEmoji.Statuses.Pending;
            this.ServerRequestedValue = string.Empty;
            this.ServerState = ReqRespEmoji.Statuses.Pending;
            if (actorStory.SyncStrategy is ActorStorySyncStrategy.Regular { Strategy: UseFirstCoalescingSyncStrategy<int> })
            {
                //Liskov substitution principle violation :(
                //Think of how to notify that a request is rejected
                this.ServerState = ReqRespEmoji.Statuses.Stop;
            }

            var result = await this.RunRequestUsingSyncStrategy(actorStory, request);

            this.IsRequestVisible = false;
            this.IsResponseVisible = true;

            this.ServerState = result != request.Value ? ReqRespEmoji.Statuses.Redirected : ReqRespEmoji.Statuses.Completed;
            this.ServerRequestedValue = string.Empty;
            this.ResponseValue = result.ToString();

            request.CompletionNotifier?.Invoke();
        }
    }

    private async Task<int> RunRequestUsingSyncStrategy(ActorStory actorStory, StoryRequest request)
    {
        return actorStory.SyncStrategy switch
        {
            ActorStorySyncStrategy.Regular regular => await this.Process(request.Value, request.ProcessingTime)
                .CoalesceInDefaultGroupUsing(regular.Strategy),
            ActorStorySyncStrategy.Aggregate aggregate => await FlowSyncAggTask
                .Create<int, List<int>>((values, ct) => this.Process(values.Sum(), request.ProcessingTime, ct).StartAsTask())
                .CoalesceInDefaultGroupUsing(aggregate.Strategy, request.Value),
            _ => throw new InvalidOperationException("Unknown actor story sync strategy")
        };
    }

    private async FlowSyncTask<int> Process(
        int requestedValue,
        TimeSpan processingTime,
        CancellationToken explicitToken = default)
    {
        this.ServerRequestedValue = requestedValue.ToString();
        this.ServerState = ReqRespEmoji.Statuses.InProgress;

        CancellationToken cancellationToken;
        if (explicitToken == default)
        {
            var cc = await FlowSyncTask.GetCancellationContext();
            cancellationToken = cc.CancellationToken;
        }
        else
        {
            cancellationToken = explicitToken;
        }

        try
        {
            await Task.Delay(processingTime, cancellationToken);
            this.ServerState = ReqRespEmoji.Statuses.Finished;
            this.ServerRequestedValue = string.Empty;
        }
        catch (OperationCanceledException)
        {
            this.ServerState = ReqRespEmoji.Statuses.Canceled;
        }

        return requestedValue;
    }

    private void SetInitialState()
    {
        this.RequestValue = ReqRespEmoji.Values.Unknown;
        this.ResponseValue = ReqRespEmoji.Values.Unknown;
        this.IsResponseVisible = false;
        this.IsRequestVisible = false;
        this.ServerState = ReqRespEmoji.Statuses.Sleep;
        this.ServerRequestedValue = string.Empty;
    }

    private async Task PulseActorSymbolAsync()
    {
        var pulseToken = Interlocked.Increment(ref this._actorPulseToken);
        this.IsActorHighlighted = true;

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        if (pulseToken == Volatile.Read(ref this._actorPulseToken))
        {
            this.IsActorHighlighted = false;
        }
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
