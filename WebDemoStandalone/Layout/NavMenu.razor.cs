using WebDemoStandalone.Metadata;

namespace WebDemoStandalone.Layout;

public partial class NavMenu : ISyncDemoPageModeVisitor<string>
{
    public static readonly IReadOnlyList<SyncDemoPageMode> Modes = new[]
    {
        SyncDemoPageMode.NoSync, SyncDemoPageMode.UseFirst, SyncDemoPageMode.UseLast, SyncDemoPageMode.DeBounce, SyncDemoPageMode.Queue
    };

    public string GetPageModeUrl(SyncDemoPageMode syncDemoPageMode) => syncDemoPageMode.Accept(this);

    string ISyncDemoPageModeVisitor<string>.CaseNoSync() => $"demo/{nameof(SyncDemoPageMode.NoSync)}";

    string ISyncDemoPageModeVisitor<string>.CaseUseFirst() => $"demo/{nameof(SyncDemoPageMode.UseFirst)}";

    string ISyncDemoPageModeVisitor<string>.CaseUseLast() => $"demo/{nameof(SyncDemoPageMode.UseLast)}";

    string ISyncDemoPageModeVisitor<string>.CaseQueue() => $"demo/{nameof(SyncDemoPageMode.Queue)}";

    string ISyncDemoPageModeVisitor<string>.CaseDeBounce() => $"demo/{nameof(SyncDemoPageMode.DeBounce)}";
}
