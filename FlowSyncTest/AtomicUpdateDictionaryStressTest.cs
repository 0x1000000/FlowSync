using FlowSync.Utils;

namespace FlowSyncTest;

internal class AtomicUpdateDictionaryStressTest
{
    private const int Total = 200;

    private static readonly Random R = new(Guid.NewGuid().GetHashCode());

    private static int _addCounter;
    private static int _removeCounter;

    static async Task PerformTest(string[] args)
    {
        var atomicUpdateDictionary = new AtomicUpdateDictionary<int, string>();

        await Task.WhenAll(
            new[]
            {
                Task.Run(() => ThreadBody("A", atomicUpdateDictionary)),
                Task.Run(() => ThreadBody("B", atomicUpdateDictionary)),
                Task.Run(() => ThreadBody("C", atomicUpdateDictionary)),

                Task.Run(() => ThreadBody("D", atomicUpdateDictionary)),
                Task.Run(() => ThreadBody("E", atomicUpdateDictionary)),
                Task.Run(() => ThreadBody("F", atomicUpdateDictionary)),

                Task.Run(() => ThreadBody("G", atomicUpdateDictionary)),
            }
        );

        int fullyRemoved = 0;
        for (int i = 0; i < Total; i++)
        {
            if (!atomicUpdateDictionary.TryRead(
                    i,
                    default(object?),
                    static (k, _, v) => { Console.WriteLine($"{k} - {v}"); }
                ))
            {
                fullyRemoved++;
            }
        }

        if (_addCounter - _removeCounter + fullyRemoved != Total)
        {
            Console.WriteLine($"Error: fullyRemoved:{fullyRemoved} - A:{_addCounter}, R:{_removeCounter}:=> {_addCounter - _removeCounter + fullyRemoved}");
        }
        else
        {
            Console.WriteLine("Ok");
        }
    }

    static void ThreadBody(string id, AtomicUpdateDictionary<int, string> array)
    {
        for (int i = 0; i < Total; i++)
        {
            var index = i;

            array.AddOrUpdate(
                index,
                default(object?),
                static (_,_) =>
                {
                    Interlocked.Increment(ref _addCounter);
                    return "";
                },
                static (_, _, old) => old
            );

            if (!array.TryUpdate(
                    index,
                    id,
                    static (_, id, v) =>
                    {
                        if (!v.Contains(id))
                        {
                            return v + id;
                        }

                        throw new Exception("Should not be");
                    },
                    out _
                ))
            {
                Console.WriteLine("Error");
            }

            if (R.Next(0, 10) == 5)
            {
                array.TryRead(index, array, static (index, array, _) =>
                {
                    //Recursion
                    array.TryRead(
                        index,
                        array,
                        static (index, array, _) =>
                        {
                            array.TryScheduleRemoval(
                                index,
                                _ =>
                                {
                                    Interlocked.Increment(ref _removeCounter);
                                    return true;
                                }
                            );
                        }
                    );
                });
            }
        }

    }
}

