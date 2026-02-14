using EnumVisitorGenerator;
using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespActorView
{
    private static readonly string[] ActorEmojiOptions =
        [ReqRespEmoji.Man, ReqRespEmoji.Woman, ReqRespEmoji.ManMediumDark, ReqRespEmoji.WomanMediumDark];

    protected string ActorEmoji { get; private set; } = "";

    [Parameter] public string RequestValue { get; set; } = ReqRespEmoji.Unknown;

    [Parameter] public string ResponseValue { get; set; } = ReqRespEmoji.Na;

    [Parameter] public string ServerState { get; set; } = ReqRespEmoji.Sleep;

    [Parameter] public string ServerRequest { get; set; } = string.Empty;

    [Parameter] public bool IsRequestVisible { get; set; }

    [Parameter] public bool IsResponseVisible { get; set; }

    protected override void OnInitialized()
    {
        this.ActorEmoji = ActorEmojiOptions[Random.Shared.Next(ActorEmojiOptions.Length)];
    }
}
