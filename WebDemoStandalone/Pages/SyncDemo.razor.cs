using FlowSync;
using Microsoft.AspNetCore.Components;
using WebDemoStandalone.Metadata;

namespace WebDemoStandalone.Pages;

public partial class SyncDemo : ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>, IDisposable
{
    [Parameter]
    public string Mode
    {
        set
        {
            var mode = Enum.TryParse<SyncDemoPageMode>(value, true, out var parsedResult)
                ? parsedResult
                : SyncDemoPageMode.NoSync;

            this.PageMode = mode;
            this.Header = mode.GetName();

            this.FlowSyncStrategy.CancelAll();
            this.FlowSyncStrategy.Dispose();
            this.FlowSyncStrategy = mode.Accept(this);
            this.LastResult = null;
        }
    }

    private SyncDemoPageMode PageMode { get; set; }

    private string Header { get; set; } = string.Empty;

    private int? LastResult { get; set; }

    private IFlowSyncStrategy<int> FlowSyncStrategy { get; set; } = NoCoalescingSyncStrategy<int>.Instance;

    private void OnResultChange(int result) => this.LastResult = result;

    void IDisposable.Dispose() => this.FlowSyncStrategy.Dispose();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseNoSync()
        => new NoCoalescingCancellableSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseUseFirst()
        => new UseFirstCoalescingSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseUseLast()
        => new UseLastCoalescingSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseQueue() => new QueueCoalescingSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseDeBounce()
        => new DeBounceCoalescingSyncStrategy<int>(2000 /*2 Sec*/);
}
