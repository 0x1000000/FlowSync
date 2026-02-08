using System.Diagnostics.CodeAnalysis;

namespace FlowSync.Utils;

/// <summary>
/// A thread-safe dictionary that allows atomic updates of values using custom update logic. It ensures consistent and synchronized reads and writes by locking per key, enabling safe concurrent access and modification of values without global locks.
/// </summary>
internal sealed class AtomicUpdateDictionary<TKey, TValue> : IDisposable where TKey : notnull
{
    private class Entry
    {
        public Entry(TValue value)
        {
            this.Value = value;
        }

        public TValue Value { get; set; }

        public int ReadingCounter { get; set; }

        public Predicate<TValue>? TailRemove { get; set; }
    }

    private readonly Dictionary<TKey, Entry> _dictionary = new();

    private readonly ReaderWriterLockSlim _globalLock = new(LockRecursionPolicy.SupportsRecursion);
    
    public (TValue, TRes) AddOrUpdate<TArg, TRes>(
        TKey key,
        TArg arg,
        Func<TKey, TArg, (TValue, TRes)> addValueFactory,
        Func<TKey, TArg, TValue, (TValue, TRes)> updateValueFactory)
    {
        while (true)
        {
            //Update (Read Lock)
            this._globalLock.EnterReadLock();

            bool found = false;
            TRes result = default!;
            TValue value = default!;

            Predicate<TValue>? tailRemove = null;

            try
            {
                if (this._dictionary.TryGetValue(key, out var entry))
                {
                    lock (entry)
                    {
                        entry.ReadingCounter++;
                        try
                        {
                            (value, result) = updateValueFactory(key, arg, entry.Value);
                            entry.Value = value;
                            found = true;
                        }
                        finally
                        {
                            entry.ReadingCounter--;
                            if (entry.ReadingCounter == 0)
                            {
                                tailRemove = entry.TailRemove;
                                entry.TailRemove = null;
                            }
                        }
                    }
                }
            }
            finally
            {
                this._globalLock.ExitReadLock();

            }
            if (tailRemove != null)
            {
                this.TryScheduleRemoval(key, tailRemove);
            }

            if (found)
            {
                return (value ,result);
            }

            //Add (Write Lock)
            this._globalLock.EnterWriteLock();
            try
            {
                if (this._dictionary.ContainsKey(key))
                {
                    //Item was added by somebody else
                    continue;
                }

                (value, result) = addValueFactory(key, arg);
                this._dictionary.Add(key, new Entry(value));
                return (value, result);
            }
            finally
            {
                this._globalLock.ExitWriteLock();
            }
        }
    }

    public TValue AddOrUpdate<TArg>(
        TKey key,
        TArg arg,
        Func<TKey, TArg, TValue> addValueFactory,
        Func<TKey, TArg, TValue, TValue> updateValueFactory)
        => this.AddOrUpdate(
                key,
                (OriginalArgs: arg, Add: addValueFactory, Update: updateValueFactory),
                static (key, exArgs) => (exArgs.Add(key, exArgs.OriginalArgs), default(object?)),
                static (key, exArgs, value) => (exArgs.Update(key, exArgs.OriginalArgs, value), default)
            )
            .Item1;

    public bool TryUpdate<TArg>(TKey key, TArg arg, Func<TKey, TArg, TValue, TValue> updateValueFactory, [NotNullWhen(true)] out TValue? newValue)
    {
        bool response;
        Predicate<TValue>? tailRemove = null;

        //Update (Read Lock)
        this._globalLock.EnterReadLock();
        try
        {
            if (this._dictionary.TryGetValue(key, out var entry))
            {
                lock (entry)
                {
                    entry.ReadingCounter++;
                    try
                    {
                        newValue = updateValueFactory(key, arg, entry.Value)!;
                        entry.Value = newValue;
                        response = true;
                    }
                    finally
                    {
                        entry.ReadingCounter--;
                        if (entry.ReadingCounter == 0)
                        {
                            tailRemove = entry.TailRemove;
                            entry.TailRemove = null;
                        }
                    }
                }
            }
            else
            {
                newValue = default!;
                response = false;
            }
        }
        finally
        {
            this._globalLock.ExitReadLock();
        }

        if (tailRemove != null)
        {
            this.TryScheduleRemoval(key, tailRemove);
        }

        return response;
    }

    public bool TryRead<TArg>(TKey key, TArg arg, Action<TKey, TArg, TValue> processor)
    {
        return this.TryRead<(TArg OriginalArgs, Action<TKey, TArg, TValue> Processor), object?>(
            key,
            (arg, processor),
            static (key, arg, v) =>
            {
                arg.Processor(key, arg.OriginalArgs, v);
                return null;
            },
            out _
        );
    }

    public bool TryRead<TArg,TRes>(TKey key, TArg arg, Func<TKey, TArg, TValue, TRes> processor, [NotNullWhen(true)] out TRes? result)
    {
        bool response;
        Predicate<TValue>? tailRemove = null;

        //Update (Read Lock)
        this._globalLock.EnterReadLock();
        try
        {
            if (this._dictionary.TryGetValue(key, out var entry))
            {
                lock (entry)
                {
                    entry.ReadingCounter++;
                    try
                    {
                        result = processor(key, arg, entry.Value);
                        response = true;
                    }
                    finally
                    {
                        entry.ReadingCounter--;
                        if (entry.ReadingCounter == 0)
                        {
                            tailRemove = entry.TailRemove;
                            entry.TailRemove = null;
                        }
                    }
                }
            }
            else
            {
                result = default;
                response = false;
            }

        }
        finally
        {
            this._globalLock.ExitReadLock();
        }

        if (tailRemove != null)
        {
            this.TryScheduleRemoval(key, tailRemove);
        }

        return response;
    }

    public void ReadAll<TArg>(TArg arg, Action<TKey, TArg, TValue> processor)
    {
        this._globalLock.EnterWriteLock();
        try
        {
            foreach (var entry in this._dictionary)
            {
                lock (entry.Value)
                {
                    processor(entry.Key, arg, entry.Value.Value);
                }
            }
        }
        finally
        {
            this._globalLock.ExitWriteLock();
        }
    }

    public bool TryScheduleRemoval(TKey key, Predicate<TValue> predicate)
    {
        this._globalLock.EnterReadLock();
        try
        {
            if (this._dictionary.TryGetValue(key, out var entry))
            {
                lock (entry)
                {
                    if (entry.ReadingCounter > 0)
                    {
                        entry.TailRemove = predicate;
                        return false;
                    }
                }
            }
        }
        finally
        {
            this._globalLock.ExitReadLock();
        }
        
        this._globalLock.EnterWriteLock();
        try
        {
            if (this._dictionary.TryGetValue(key, out var entry))
            {
                lock (entry)
                {
                    if (predicate(entry.Value))
                    {
                        this._dictionary.Remove(key);
                    }
                }
            }
            return true;
        }
        finally
        {
            this._globalLock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        this._globalLock.Dispose();
    }
}

internal class AtomicUpdateDictionary
{
    public static readonly object DefaultKey = new();
}

