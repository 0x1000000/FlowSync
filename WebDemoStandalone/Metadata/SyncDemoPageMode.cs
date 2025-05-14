using EnumVisitorGenerator;

namespace WebDemoStandalone.Metadata;

[VisitorGenerator]
public enum SyncDemoPageMode
{
    NoSync,
    UseFirst,
    UseLast,
    Queue,
    DeBounce
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
    }

    private readonly struct DescriptionSwitcher : ISyncDemoPageModeVisitor<string>
    {
        public string CaseNoSync() => "No Synchronization Description";

        public string CaseUseFirst() => "Use First Description";

        public string CaseUseLast() => "Use Last Description";

        public string CaseQueue() => "Queue Description";

        public string CaseDeBounce() => "Queue De-Bounce (2 sec)";
    }

    public static string GetName(this SyncDemoPageMode mode)
    {
        var nameSwitcher = new NameSwitcher();
        return mode.Accept<string, NameSwitcher>(ref nameSwitcher);
    }

    public static string GetDescription(this SyncDemoPageMode mode)
    {
        var nameSwitcher = new DescriptionSwitcher();
        return mode.Accept<string, DescriptionSwitcher>(ref nameSwitcher);
    }
}


