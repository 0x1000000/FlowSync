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
            var mode = Enum.TryParse<SyncDemoPageMode>(value, true, out var parsedResult) ? parsedResult : SyncDemoPageMode.NoSync;

            this.Header = mode.GetName();
            this.Description = mode.GetDescription();

            this.FlowSyncStrategy.CancelAll();
            this.FlowSyncStrategy.Dispose();
            this.FlowSyncStrategy = mode.Accept(this);
        }
    }

    public string Header { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public IFlowSyncStrategy<int> FlowSyncStrategy { get; set; } = NoCoalescingSyncStrategy<int>.Instance;

    public IFlowSyncStrategy<int> CaseNoSync() => new NoCoalescingCancellableSyncStrategy<int>();

    public IFlowSyncStrategy<int> CaseUseFirst() => new UseFirstCoalescingSyncStrategy<int>();

    public IFlowSyncStrategy<int> CaseUseLast() => new UseLastCoalescingSyncStrategy<int>();

    public IFlowSyncStrategy<int> CaseQueue() => new QueueCoalescingSyncStrategy<int>();

    public IFlowSyncStrategy<int> CaseDeBounce() => new DeBounceCoalescingSyncStrategy<int>(2000/*2 Sec*/);

    public void Dispose()
    {
        this.FlowSyncStrategy.Dispose();
    }
}