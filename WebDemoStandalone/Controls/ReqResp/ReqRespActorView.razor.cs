using Microsoft.AspNetCore.Components;
using WebDemoStandalone.Controls.ReqResp.Models;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespActorView
{
    private static readonly string[] ActorEmojiOptions =
        ReqRespEmoji.Actors.All;

    protected string ActorEmoji { get; private set; } = "";

    [Parameter] public string RequestValue { get; set; } = ReqRespEmoji.Values.Unknown;

    [Parameter] public string ResponseValue { get; set; } = ReqRespEmoji.Values.Na;

    [Parameter] public string ServerState { get; set; } = ReqRespEmoji.Statuses.Sleep;

    [Parameter] public string ServerRequestedValue { get; set; } = string.Empty;

    [Parameter] public bool IsActorHighlighted { get; set; }

    [Parameter] public bool IsRequestVisible { get; set; }

    [Parameter] public bool IsResponseVisible { get; set; }

    protected string ServerRequestedValueDisplay => this.ServerRequestedValue;

    protected string RequestValueHint => this.RequestValue != ReqRespEmoji.Values.Unknown ? "Request has been send" : "No requests";

    protected string ResponseValueHint => this.ResponseValue switch
    {
        ReqRespEmoji.Values.Unknown => "No response yet",
        ReqRespEmoji.Statuses.Pending => "Response is pending",
        _ => "Response is ready"
    };

    protected string ArrowStackHint => (this.IsRequestVisible, this.IsResponseVisible) switch
    {
        (true, false) => "Request is moving to server",
        (false, true) => "Response is moving to actor",
        (true, true) => "Request and response are both visible",
        _ => "No active request or response movement"
    };

    protected string ServerStateHint => this.ServerState switch
    {
        ReqRespEmoji.Statuses.Sleep => "Server is idle",
        ReqRespEmoji.Statuses.Pending => "Server queued request",
        ReqRespEmoji.Statuses.InProgress => "Server is processing request",
        ReqRespEmoji.Statuses.Canceled => "Server request was canceled",
        ReqRespEmoji.Statuses.Completed => "Server completed request",
        ReqRespEmoji.Statuses.Redirected => "Server redirected result",
        ReqRespEmoji.Statuses.Stop => "Server rejected request",
        _ => "Server state is unknown"
    };

    protected string ServerRequestedValueHint
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(this.ServerRequestedValue))
            {
                return "Server processing the requested value";
            }

            return this.ServerState == ReqRespEmoji.Statuses.Pending
                ? "Request received; no processing value yet (may be coalesced)"
                : "No processing value (request may be received or coalesced)";
        }
    }

    protected override void OnInitialized()
    {
        this.ActorEmoji = ActorEmojiOptions[Random.Shared.Next(ActorEmojiOptions.Length)];
    }
}
