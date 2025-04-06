using System.Runtime.InteropServices.JavaScript;
using FlowSync;

namespace FlowSyncTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            IFlowSyncStrategy<int> syncStrategy = new UseLastCoalescingSyncStrategy<int>();

            var job0 = Job(0).Sync(syncStrategy).Start(true);
            await Task.Delay(1);
            var job1 = Job(1).Sync(syncStrategy).Start(true);
            await Task.Delay(1);
            var job2 = Job(2).Sync(syncStrategy).Start(true);
            await Task.Delay(1);
            var job3 = Job(3).Sync(syncStrategy).Start(true);

            AssertToBe(await job0, 3);
            AssertToBe(await job1, 3);
            AssertToBe(await job2, 3);
            AssertToBe(await job3, 3);
        }

        public static async FlowSyncTask<int> Job(int arg)
        {
            var ctx = await FlowSyncTask.GetCancellationContext();
            try
            {
                await Task.Delay(100, ctx.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Cancelled for {arg}");
            }

            return arg;
        }


        private static void AssertToBe<T>(T o1, T o2)
        {
            if (!Equals(o1, o2))
            {
                throw new Exception("Not equal");
            }
        }

    }
}
