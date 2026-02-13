using EnumVisitorGenerator;

namespace WebDemoStandalone.Metadata;

[VisitorGenerator]
public enum SyncDemoPageMode
{
    NoSync,
    UseFirst,
    UseLast,
    Queue,
    DeBounce,
    Agg
}

public static class SyncDemoPageModeExtension
{
    private readonly struct NameSwitcher : ISyncDemoPageModeVisitor<string>
    {
        public string CaseNoSync() => "No Synchronization";

        public string CaseUseFirst() => "Use First";

        public string CaseUseLast() => "Use Last";

        public string CaseQueue() => "Queue";

        public string CaseDeBounce() => "De-Bounce (2 sec)";

        public string CaseAgg() => "Aggregate";
    }

    public static string GetName(this SyncDemoPageMode mode)
    {
        var nameSwitcher = new NameSwitcher();
        return mode.Accept<string, NameSwitcher>(ref nameSwitcher);
    }
}


