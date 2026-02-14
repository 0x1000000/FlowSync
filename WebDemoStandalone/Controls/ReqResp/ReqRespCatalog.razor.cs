using FlowSync;

namespace WebDemoStandalone.Controls.ReqResp;

public partial class ReqRespCatalog
{
    public IFlowSyncStrategy<int> NoCoalescingSyncStrategy { get; } = NoCoalescingSyncStrategy<int>.Instance;

    public IFlowSyncStrategy<int> UseFirstCoalescingSyncStrategy { get; } = new UseFirstCoalescingSyncStrategy<int>();

    public IFlowSyncStrategy<int> UseLastCoalescingSyncStrategy { get; } = new UseLastCoalescingSyncStrategy<int>();

    public IFlowSyncStrategy<int> DeBounceCoalescingSyncStrategy { get; } = new DeBounceCoalescingSyncStrategy<int>(TimeSpan.FromSeconds(4));

    public IFlowSyncStrategy<int> QueueCoalescingSyncStrategy { get; } = new QueueCoalescingSyncStrategy<int>();

}
