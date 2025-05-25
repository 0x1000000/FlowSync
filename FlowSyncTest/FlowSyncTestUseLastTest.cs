using System.Collections.Concurrent;
using FlowSync;
using FlowSyncTest.Utils;
using NUnit.Framework;

namespace FlowSyncTest;

[TestFixture]
public class FlowSyncTestUseLastTest
{
    [Test]
    public async Task Basic()
    {
        var total = 7;

        var orderedSemaphore = new OrderedSemaphore(OrderedSemaphore.ContinuationRunMode.ForceNewThread);
        var cancellationLog = new ConcurrentDictionary<int, bool>();
        var timeLines = FakeTimer.CreateTimeLines(total);

        var syncStrategy = new UseLastCoalescingSyncStrategy<int>();

        var result = await Task.WhenAll(
            Enumerable.Range(0, total)
                .Reverse()
                .Select(
                    index => Task.Run(
                        () => this.ThreadMethod(orderedSemaphore, cancellationLog, timeLines[index], index, syncStrategy)
                    )
                )
        );

        Assert.That(result.Sum(), Is.EqualTo((42 + total-1) * total));

        for (int i = 0; i < total - 1; i++)
        {
            Assert.That(cancellationLog[i], Is.True);
        }
        Assert.That(cancellationLog[total-1], Is.False);
    }

    private async Task<int> ThreadMethod(
        OrderedSemaphore orderedSemaphore,
        ConcurrentDictionary<int, bool> cancellationLog,
        FakeTimer.FakeTimeLine timeLine,
        int index,
        IFlowSyncStrategy<int> syncStrategy)
    {
        await orderedSemaphore.WaitAsync(index);

        var flowSyncTaskAwaiter = this.GetSyncMethod(cancellationLog, timeLine, index).Sync(syncStrategy).Start();

        Thread.Sleep(10);

        var sync = await flowSyncTaskAwaiter;
        return sync;
    }

    private async FlowSyncTask<int> GetSyncMethod(
        ConcurrentDictionary<int, bool> cancellationLog,
        FakeTimer.FakeTimeLine timeLine,
        int index)
    {
        await using (timeLine)
        {
            var cancellationContext = await FlowSyncTask.GetCancellationContext();

            await timeLine.FakeDelay(100 + index);

            cancellationLog[index] = cancellationContext.CancellationToken.IsCancellationRequested;

            return index + 42;
        }
    }
}
