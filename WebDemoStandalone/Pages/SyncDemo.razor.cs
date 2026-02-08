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
            this.StartsAndResult1.Clear();
            this.StartsAndResult2.Clear();
        }
    }

    private SyncDemoPageMode PageMode { get; set; }

    private string Header { get; set; } = string.Empty;

    private IFlowSyncStrategy<int> FlowSyncStrategy { get; set; } = NoCoalescingSyncStrategy<int>.Instance;

    private void OnResult1Change(int result) => this.StartsAndResult1.Result = result;
    private void OnResult2Change(int result) => this.StartsAndResult2.Result = result;

    private void OnStarted1(int index) => this.StartsAndResult1.Clear(index);

    private void OnStarted2(int index) => this.StartsAndResult2.Clear(index);

    private StartsAndResult StartsAndResult1 { get; } = new StartsAndResult();
    private StartsAndResult StartsAndResult2 { get; } = new StartsAndResult();

    void IDisposable.Dispose() => this.FlowSyncStrategy.Dispose();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseNoSync()
        => new NoCoalescingCancellableSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseUseFirst()
        => new UseFirstCoalescingSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseUseLast()
        => new UseLastCoalescingSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseQueue() => new QueueCoalescingSyncStrategy<int>();

    IFlowSyncStrategy<int> ISyncDemoPageModeVisitor<IFlowSyncStrategy<int>>.CaseDeBounce()
        => new DeBounceCoalescingSyncStrategy<int>(TimeSpan.FromSeconds(2));

    private class StartsAndResult
    {
        public int? Start = null;
        public int? Result = null;

        public void Clear(int? start = null)
        {
            this.Start = start;
            this.Result = null;
        }
    }
}
