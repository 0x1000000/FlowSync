using System.Text;
using NUnit.Framework;

namespace FlowSyncTest.Utils;

[TestFixture]
public class OrderedSemaphoreTest
{
    [Test]
    public async Task TestOrderedSemaphore_Single()
    {
        var orderedSemaphore = new OrderedSemaphore();

        await orderedSemaphore.WaitAsync(0);
    }



    [Test]
    public async Task TestOrderedSemaphore()
    {
        var str = "ABCDEFGHIJK";

        for (int i = 0; i < 100; i++)
        {
            var logger = new StringBuilder();

            var orderedSemaphore = new OrderedSemaphore();

            await Task.WhenAll(str.Reverse().Select((_, index) => Task.Run(async () =>
                {
                    await Task.Delay(1);
                    await this.TestTimerParallel(orderedSemaphore, index, logger, str);
                }
            )));

            Assert.That(logger.ToString(), Is.EqualTo(str));
        }
    }

    private async Task TestTimerParallel(OrderedSemaphore orderedSemaphore, int index, StringBuilder logger, string value)
    {
        await orderedSemaphore.WaitAsync(index);

        await orderedSemaphore.WaitAsync(index);

        logger.Append(value[index]);
    }

}
