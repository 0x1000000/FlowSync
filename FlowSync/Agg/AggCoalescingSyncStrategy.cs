using FlowSync.Utils;

namespace FlowSync;

public class AggCoalescingSyncStrategy<T, TArg, TAcc> : IFlowSyncAggStrategy<T, TArg, TAcc>
{
    private enum EntryStatus
    {
        Buffering,
        InProgress,
        NewItemsWhileInProgress,
        Completed
    }

    private record struct Ctx(AggCoalescingSyncStrategy<T, TArg, TAcc> This, IFlowSyncAggStarter<T, TAcc> Starter, TArg Arg);

    private readonly record struct Entry(
        FlowSyncTaskAwaiter<T> Remote,
        FlowSyncTaskAwaiter<T>? CurrentAwaiter,
        IFlowSyncAggStarter<T, TAcc> Starter,
        TAcc Acc,
        EntryStatus Status)
    {
        public void CancelUnsafe()
        {
            this.Remote.Cancel(true);
            this.CurrentAwaiter?.Cancel(true);
        }
    }

    private readonly AtomicUpdateDictionary<object, Entry> _storage = new();

    private readonly Func<TAcc?, int, TAcc>? _seed;
    private readonly Func<TAcc, TArg, TAcc> _aggregator;
    private readonly TimeSpan _bufferTime;

    public AggCoalescingSyncStrategy(Func<TAcc?, int, TAcc>? seedFactory, Func<TAcc, TArg, TAcc> aggregator, TimeSpan bufferTime)
    {
        if (bufferTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferTime), "Should be finite");
        }

        this._seed = seedFactory;
        this._aggregator = aggregator;
        this._bufferTime = bufferTime;
    }

    public FlowSyncTaskAwaiter<T> EnterSyncSection(IFlowSyncAggStarter<T, TAcc> flowStarter, TArg arg, object? groupKey = null)
    {
        groupKey ??= AtomicUpdateDictionary.DefaultKey;
        var entry = this._storage.AddOrUpdate(groupKey, new Ctx(this, flowStarter, arg), AddNewEntry, UpdateEntry);
        return entry.Remote;
    }

    private static Entry AddNewEntry(object key, Ctx ctx)
    {
        var remote = CurrentCycle(ctx.This, key).CreateWithoutCoalescing();

        remote.LazyOnCompleted(() =>
        {
            ctx.This._storage.TryScheduleRemoval(key, e => e.Remote == remote);
        });

        var initialAcc = ctx.This._seed != null ? ctx.This._seed(default!, 0) : default!;
        return new Entry(
            remote,
            null,
            ctx.Starter,
            ctx.This._aggregator(initialAcc, ctx.Arg),
            EntryStatus.Buffering
        );
    }

    private static Entry UpdateEntry(object key, Ctx ctx, Entry currentEntry)
    {
        if (currentEntry.Status == EntryStatus.Completed)
        {
            //It is not yet removed, but it is already completed, so we can just add a new entry instead of updating the existing one.
            return AddNewEntry(key, ctx);
        }

        var newAcc = ctx.This._aggregator(currentEntry.Acc, ctx.Arg);
        return currentEntry with
        {
            Acc = newAcc,
            Starter = ctx.Starter,
            Status = currentEntry.Status == EntryStatus.InProgress ? EntryStatus.NewItemsWhileInProgress : currentEntry.Status
        };
    }

    private static async FlowSyncTask<T> CurrentCycle(AggCoalescingSyncStrategy<T, TArg, TAcc> @this, object groupKey)
    {
        var cancellationToken = (await FlowSyncTask.GetCancellationContext()).CancellationToken;

        if (@this._bufferTime != TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(@this._bufferTime, cancellationToken);
            }
            catch (Exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    @this.Cancel(groupKey);
                }
                else
                {
                    throw;
                }
            }
        }

        @this._storage.TryUpdate(
            groupKey,
            @this,
            static (_, _, oldEntry) => oldEntry with
            {
                CurrentAwaiter = oldEntry.Starter.CreateAwaiter(oldEntry.Acc),
                Status = EntryStatus.InProgress
            },
            out var newEntry
        );

        if (cancellationToken.IsCancellationRequested)
        {
            @this.Cancel(groupKey);
            return default!; //It is ignored;
        }

        T result = default!;

        int batchIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            result = await newEntry.CurrentAwaiter!;
            batchIndex++;

            @this._storage.TryUpdate(
                groupKey,
                (batchIndex, @this),
                static (_, ctx, oldEntry) =>
                {
                    var (batchIndex, @this) = ctx;
                    if (oldEntry.Status == EntryStatus.NewItemsWhileInProgress)
                    {
                        var acc = @this._seed == null ? oldEntry.Acc : @this._seed(oldEntry.Acc, batchIndex);
                        return oldEntry with
                        {
                            Acc = acc,
                            Status = EntryStatus.InProgress,
                            CurrentAwaiter = oldEntry.Starter.CreateAwaiter(acc)
                        };
                    }

                    return oldEntry with { Status = EntryStatus.Completed };
                },
                out newEntry
            );

            if (newEntry.Status == EntryStatus.Completed)
            {
                break;
            }
        }

        return result;
    }

    public void Cancel(object groupKey) => this._storage.TryRead(groupKey, this, static (_, _, e) => e.CancelUnsafe());

    public void CancelAll() => this._storage.ReadAll(this, static (_, _, e) => e.CancelUnsafe());

    public void Dispose() => this._storage.Dispose();
}
