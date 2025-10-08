using System.Collections.Concurrent;
using FlowSync;
using FlowSyncTest.Utils;
using NUnit.Framework;

namespace FlowSyncTest;

[TestFixture]
public class FlowSyncTestUseFirstTest
{
    [Test]
    public Task Basic() => this.Basic(useRegularTask: false);

    [Test]
    public Task BasicRegularTask() => this.Basic(useRegularTask: true);

    private async Task Basic(bool useRegularTask)
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
                        () => this.ThreadMethod(orderedSemaphore, cancellationLog, index, syncStrategy, useRegularTask)
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
        IFlowSyncStrategy<int> syncStrategy,
        bool useRegularTask)
    {
        await orderedSemaphore.WaitAsync(index);

        var sync = await (useRegularTask
            ? FlowSyncTask.Create(ct => this.GetSyncMethodAsTask(ct, cancellationLog, index))
            : this.GetSyncMethod(cancellationLog, index)).CoalesceUsing(syncStrategy);
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