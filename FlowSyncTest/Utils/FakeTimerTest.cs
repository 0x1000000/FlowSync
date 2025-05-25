using System.Text;
using NUnit.Framework;

namespace FlowSyncTest.Utils;

[TestFixture]
public class FakeTimerTest
{
    [Test]
    public async Task TestTimer()
    {
        var logger = new StringBuilder();

        var timeLines = FakeTimer.CreateTimeLines(2);

        await Task.WhenAll(
            Task.Run(() => this.TestTimerParallelA(logger, timeLines[0])),
            Task.Run(() => this.TestTimerParallelB(logger, timeLines[1]))
        );

        Console.WriteLine(logger.ToString());
        Assert.That(logger.ToString(), Is.EqualTo("B1 A1 A2 B2 A3 B3 "));
    }

    private async Task TestTimerParallelA(StringBuilder logger, FakeTimer.FakeTimeLine fakeTimeline)
    {
        await using (fakeTimeline)
        {
            await fakeTimeline.FakeDelay(100);

            logger.Append("A1 ");

            await fakeTimeline.FakeDelay(100);

            logger.Append("A2 ");

            await fakeTimeline.FakeDelay(100);

            logger.Append("A3 ");
        }
    }

    private async Task TestTimerParallelB(StringBuilder logger, FakeTimer.FakeTimeLine fakeTimeline)
    {
        await using (fakeTimeline)
        {
            await fakeTimeline.FakeDelay(80);

            logger.Append("B1 ");

            await fakeTimeline.FakeDelay(200);

            logger.Append("B2 ");

            await fakeTimeline.FakeDelay(110);

            logger.Append("B3 ");
        }
    }

}