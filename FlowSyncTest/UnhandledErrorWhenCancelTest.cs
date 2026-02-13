using FlowSync;
using NUnit.Framework;

namespace FlowSyncTest;

[TestFixture]
public class UnhandledErrorWhenCancelTest
{
    [Test]
    [TestCase(StrategyId.NoCoalescingCancellable)]
    [TestCase(StrategyId.UseFirst)]
    [TestCase(StrategyId.UseLast)]
    [TestCase(StrategyId.Debounce)]
    [TestCase(StrategyId.Queue)]
    public async Task DoAsync(StrategyId strategyId)
    {
        var strategy = strategyId.Create<int>();

        await Task.WhenAny(Job().CoalesceInDefaultGroupUsing(strategy).StartAsTask(), Task.Delay(1));
        await Task.WhenAny(Job().CoalesceInDefaultGroupUsing(strategy).StartAsTask(), Task.Delay(1));

        strategy.CancelAll();
        strategy.Dispose();

        static async FlowSyncTask<int> Job()
        {
            var ctx = await FlowSyncTask.GetCancellationContext();

            await Task.Delay(50);

            return 0;
        }

        //There should not be unhandled exceptions as Job keeps working;
        await Task.Delay(100);
    }
}

//[TestFixture]