using System.Collections.Concurrent;
using FlowSync;
using FlowSyncTest.Utils;
using NUnit.Framework;

namespace FlowSyncTest;

[TestFixture]
public class FlowSyncTestUseFirstTest
{
    [Test]
    public async Task Basic()
    {
        var total = 7;

        var orderedSemaphore = new OrderedSemaphore(OrderedSemaphore.ContinuationRunMode.ForceNewThread);
        var cancellationLog = new ConcurrentDictionary<int, bool>();

        var syncStrategy = new UseFirstCoalescingSyncStrategy<int>();

        var result = await Task.WhenAll(
            Enumerable.Range(0, total)
                .Reverse()
                .Select(
                    index => Task.Run(
                        () => this.ThreadMethod(orderedSemaphore, cancellationLog, index, syncStrategy)
                    )
                )
        );

        Assert.That(result.Sum(), Is.EqualTo(42 * total));

        Assert.That(cancellationLog.Count, Is.EqualTo(1));
        Assert.That(cancellationLog[0], Is.False);
    }

    private async Task<int> ThreadMethod(
        OrderedSemaphore orderedSemaphore,
        ConcurrentDictionary<int, bool> cancellationLog,
        int index,
        IFlowSyncStrategy<int> syncStrategy)
    {
        await orderedSemaphore.WaitAsync(index);

        var sync = await this.GetSyncMethod(cancellationLog, index).Sync(syncStrategy);
        return sync;
    }

    private async FlowSyncTask<int> GetSyncMethod(
        ConcurrentDictionary<int, bool> cancellationLog,
        int index)
    {
        var cancellationContext = await FlowSyncTask.GetCancellationContext();

        await Task.Delay(10);//Let other thread enter the critical section

        cancellationLog[index] = cancellationContext.CancellationToken.IsCancellationRequested;

        return index + 42;
    }
}