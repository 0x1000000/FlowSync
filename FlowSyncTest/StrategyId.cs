using EnumVisitorGenerator;
using FlowSync;

namespace FlowSyncTest;

[VisitorGenerator]
public enum StrategyId
{
    NoCoalescingCancellable,
    UseFirst,
    UseLast,
    Debounce,
    Queue
}


public static class StrategyIdExtensions
{
    public static IFlowSyncStrategy<T> Create<T>(this StrategyId strategyId)
    {
        var s = new FlowSyncStrategySwitcher<T>();
        return strategyId.Accept<IFlowSyncStrategy<T>, FlowSyncStrategySwitcher<T>>(ref s);
    }
}

public readonly struct FlowSyncStrategySwitcher<T> : IStrategyIdVisitor<IFlowSyncStrategy<T>>
{
    public IFlowSyncStrategy<T> CaseNoCoalescingCancellable() => new NoCoalescingSyncStrategy<T>();

    public IFlowSyncStrategy<T> CaseUseFirst() => new UseFirstCoalescingSyncStrategy<T>();

    public IFlowSyncStrategy<T> CaseUseLast() => new UseLastCoalescingSyncStrategy<T>();

    public IFlowSyncStrategy<T> CaseDebounce() => new DeBounceCoalescingSyncStrategy<T>(TimeSpan.FromMilliseconds(1));

    public IFlowSyncStrategy<T> CaseQueue() => new QueueCoalescingSyncStrategy<T>();
}