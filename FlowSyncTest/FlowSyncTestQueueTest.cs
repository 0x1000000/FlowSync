using System.Collections.Concurrent;
using FlowSync;
using FlowSyncTest.Utils;
using NUnit.Framework;

namespace FlowSyncTest;

[TestFixture]
public class FlowSyncTestQueueTest
{
    [Test]
    public Task Basic() => Basic(useRegularTask: false);

    [Test]
    public Task BasicRegularTask() => Basic(useRegularTask: true);

    private async Task Basic(bool useRegularTask)
    {
        var total = 7;

        var orderedSemaphore = new OrderedSemaphore(OrderedSemaphore.ContinuationRunMode.ForceNewThread);
        var cancellationLog = new ConcurrentDictionary<int, bool>();

        var syncStrategy = new QueueCoalescingSyncStrategy<int>();

        var result = await Task.WhenAll(
            Enumerable.Range(0, total)
                .Reverse()
                .Select(
                    index => Task.Run(
                        () => this.ThreadMethod(orderedSemaphore, cancellationLog, index, syncStrategy, useRegularTask)
                    )
                )
        );

        Assert.That(result.Sum(), Is.EqualTo((42+total-1) * total));

        Assert.That(cancellationLog.Count, Is.EqualTo(total));
        for (int i = 0; i < total; i++)
        {
            Assert.That(cancellationLog[i], Is.False);
        }
    }

    private async Task<int> ThreadMethod(
        OrderedSemaphore orderedSemaphore,
        ConcurrentDictionary<int, bool> cancellationLog,
        int index,
        IFlowSyncStrategy<int> syncStrategy,
        bool useRegularTask)
    {
        await orderedSemaphore.WaitAsync(index);

        int result;
        if (useRegularTask)
        {
            result = await FlowSyncTask.Create(ct => this.GetSyncMethodAsTask(ct, cancellationLog, index)).CoalesceUsing(syncStrategy);
        }
        else
        {
            result = await this.GetSyncMethod(cancellationLog, index).CoalesceUsing(syncStrategy);
        }

        
        return result;
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

    private async Task<int> GetSyncMethodAsTask(
        CancellationToken cancellationToken,
        ConcurrentDictionary<int, bool> cancellationLog,
        int index)
    {
        await Task.Delay(10);//Let other thread enter the critical section

        cancellationLog[index] = cancellationToken.IsCancellationRequested;

        return index + 42;
    }
}
