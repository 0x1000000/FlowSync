using FlowSync;
using NUnit.Framework;

namespace FlowSyncTest;

public class AggCoalescingSyncStrategyTest
{
    [Test]
    public async Task BasicTest()
    {
        var strategy = new AggCoalescingSyncStrategy<int, int, List<int>>(
            (acc, _) => acc ?? [],
            (acc, next) =>
            {
                acc.Add(next);
                return acc;
            },
            TimeSpan.FromMilliseconds(200)
        );

        var jobCtx = new JobCtx();

        var flowSyncAggTask = FlowSyncAggTask.Create<int, List<int>>((acc, ct) => Job(acc, 200, null, jobCtx, ct));

        List<FlowSyncTaskAwaiter<int>> awaiters = [];
        for (var i = 1; i <= 10; i++)
        {
            awaiters.Add(flowSyncAggTask.CoalesceInDefaultGroupUsing(strategy, i).Start());
            await Task.Delay(1);
        }

        foreach (var awaiter in awaiters)
        {
            var res = await awaiter;
            Assert.That(res, Is.EqualTo(55));
        }

        Assert.That(jobCtx.RunCounter, Is.EqualTo(1));
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task NoBufferTime(bool rollingBuffer)
    {
        var strategy = new AggCoalescingSyncStrategy<int, int, List<int>>(
            (acc, _) => rollingBuffer ? acc ?? [] : [],
            (acc, next) =>
            {
                acc.Add(next);
                return acc;
            },
            TimeSpan.Zero
        );

        var jobCtx = new JobCtx();

        TaskCompletionSource tcs = new();

        var flowSyncAggTask = FlowSyncAggTask.Create<int, List<int>>((acc, ct) => Job(acc, 10, ()=> tcs.Task, jobCtx, ct));

        List<FlowSyncTaskAwaiter<int>> awaiters = [];
        for (var i = 1; i <= 5; i++)
        {
            awaiters.Add(flowSyncAggTask.CoalesceInDefaultGroupUsing(strategy, i).Start());
        }
        await Task.Delay(1);
        for (var i = 6; i <= 10; i++)
        {
            awaiters.Add(flowSyncAggTask.CoalesceInDefaultGroupUsing(strategy, i).Start());
        }
        tcs.SetResult();

        HashSet<int> results = [];
        foreach (var awaiter in awaiters)
        {
            results.Add(await awaiter);
        }

        Assert.That(results.Count, Is.EqualTo(1));
        if (rollingBuffer)
        {
            Assert.That(results.Sum(), Is.EqualTo(55));
        }
        else
        {
            Assert.That(results.Sum(), Is.LessThan(55));
        }

        Assert.That(jobCtx.RunCounter, Is.GreaterThanOrEqualTo(2));
        Assert.That(jobCtx.WasCancelled, Is.False);
    }

    [Test]
    [TestCase(200)]
    [TestCase(0)]
    public async Task CancelTest(int bufferMs)
    {
        var strategy = new AggCoalescingSyncStrategy<int, int, List<int>>(
            (acc, _) => acc ?? [],
            (acc, next) =>
            {
                acc.Add(next);
                return acc;
            },
            TimeSpan.FromMilliseconds(bufferMs)
        );

        var jobCtx = new JobCtx();

        var flowSyncAggTask = FlowSyncAggTask.Create<int, List<int>>((acc, ct) => Job(acc, 1000, null, jobCtx, ct));

        List<FlowSyncTaskAwaiter<int>> awaiters = [];
        for (var i = 1; i <= 10; i++)
        {
            awaiters.Add(flowSyncAggTask.CoalesceInDefaultGroupUsing(strategy, i).Start());
            await Task.Delay(1);
        }

        strategy.CancelAll();

        foreach (var awaiter in awaiters)
        {
            Assert.ThrowsAsync<OperationCanceledException>(async () => await awaiter);
        }

        if (bufferMs == 0)
        {
            Assert.That(jobCtx.RunCounter, Is.EqualTo(1));
            Assert.That(jobCtx.WasCancelled, Is.EqualTo(true));
        }
        else
        {
            Assert.That(jobCtx.RunCounter, Is.EqualTo(0));
        }
    }

    private static async Task<int> Job(List<int> ids, int? delayMs, Func<Task>? waiter, JobCtx jobCtx, CancellationToken ct)
    {
        jobCtx.RunCounter++;
        try
        {
            if (waiter != null)
            {
                await waiter();
            }
            if (delayMs.HasValue)
            {
                await Task.Delay(delayMs.Value, ct);
            }
        }
        catch (OperationCanceledException)
        {
            jobCtx.WasCancelled = true;
        }

        return ids.Sum();
    }

    private class JobCtx
    {
        public int RunCounter;

        public bool WasCancelled;
    }
}
