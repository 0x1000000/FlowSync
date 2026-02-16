# FlowSync

FlowSync is a lightweight **async coalescing** library for .NET. It lets multiple callers share a single in-flight operation while choosing a **strategy** for how competing calls are handled.

## Demo Site: https://0x1000000.github.io/FlowSync/
## Video Intro: https://www.youtube.com/watch?v=wwSU83Qpjts


**Key Ideas**
- Coalesce concurrent calls per `groupKey`.
- Choose how to resolve contention: use-first, use-last, queue, debounce, or aggregate-and-batch.
- Optional debouncing/buffering for bursty workloads.

**Strategies**
- `UseFirstCoalescingSyncStrategy<T>`: first caller runs, later callers join and observe the same result.
- `UseLastCoalescingSyncStrategy<T>`: later callers replace earlier ones; earlier calls are canceled.
- `QueueCoalescingSyncStrategy<T>`: callers are queued and executed sequentially (spooler-like).
- `DeBounceCoalescingSyncStrategy<T>`: debounces rapid-fire calls into fewer executions.
- `AggCoalescingSyncStrategy<T, TArg, TAcc>`: buffers incoming arguments for a time window, aggregates them into an accumulator, then runs one execution per batch. If new arguments arrive while a batch is running, they are collected for the next batch.

**Install**
```bash
dotnet add package FlowSync
```

**Usage (FlowSyncTask)**
This variant is for methods that return `FlowSyncTask<T>` directly.  
Each invocation enters a strategy-managed pipeline (grouped by `groupKey`), so overlapping calls may be shared, replaced, queued, or canceled depending on strategy rules.
```csharp
using FlowSync;

// Strategy keeps per-group coalescing state across calls, so share one instance.
static readonly IFlowSyncStrategy<int> Strategy = new UseLastCoalescingSyncStrategy<int>();

// FlowSyncTask<T> is a custom awaitable type (not Task<T> or ValueTask<T>).
public async FlowSyncTask<int> FetchAsync(int id)
{
    var ctx = await FlowSyncTask.GetCancellationContext();
    // ctx.CancellationToken covers both:
    // 1) explicit external cancellation
    // 2) strategy-enforced cancellation due to coalescing/overlap
    // ctx.IsCancelledLocally is true only for case (2).
    
    // Do work here and respect ctx.CancellationToken if needed.
    await Task.Delay(Random.Shared.Next(50, 501), ctx.CancellationToken);
    return id + 42;
}

public async Task<int> CallerAsync(int id)
{
    // CallerAsync can be invoked concurrently (for example from different threads).
    // For the same groupKey, each call awaits strategy resolution and completes when
    // the coalesced pipeline finishes (or is canceled); a given invocation may never start its own FetchAsync execution.
    return await FetchAsync(id).CoalesceInGroupUsing(Strategy, groupKey: id);
}

public void CancelFetch(int id)
{
    Strategy.Cancel(id);
}
```
**Remarks**
1. `FlowSyncTask.GetCancellationContext()` returns a combined cancellation context:
`CancellationToken` is canceled for either external explicit cancellation or strategy-enforced cancellation, and `IsCancelledLocally` is `true` only for strategy-enforced cancellation (for example in overlapping `UseLast` calls).
2. `CoalesceInGroupUsing(...)` returns a lazy awaiter. The underlying work does not start until it is awaited, `Start()` is called, or `StartAsTask()` is called.

**Usage (wrap a regular Task)**
Use this when your existing code already returns `Task<T>` and you do not want to rewrite method signatures.  
`FlowSyncTask.Create(...)` adapts the regular task into the same coalescing pipeline, so strategy behavior is identical to the `FlowSyncTask<T>` approach.  
This is usually the easiest migration path for existing codebases.

```csharp
using FlowSync;

static readonly IFlowSyncStrategy<int> Strategy = new UseFirstCoalescingSyncStrategy<int>();

public Task<int> CallerAsync(int id)
{
    // StartAsTask() explicitly starts the lazy awaiter and returns Task<int>.
    return FlowSyncTask
        .Create(ct => WorkAsync(id, ct))
        .CoalesceInGroupUsing(Strategy, groupKey: id)
        .StartAsTask();
}

static async Task<int> WorkAsync(int id, CancellationToken ct)
{
    await Task.Delay(Random.Shared.Next(50, 501), ct);
    return id + 42;
}
```

**Usage (aggregate multiple calls into batches)**
This mode collects many small inputs into batches and executes fewer larger operations.  
Arguments are buffered for `bufferTime`, merged into an accumulator, and processed as one run per group.  
If new calls arrive while a batch is running, they are accumulated for the next batch cycle.

```csharp
using FlowSync;

static readonly IFlowSyncAggStrategy<int, int, List<int>> AggStrategy =
    new AggCoalescingSyncStrategy<int, int, List<int>>(
        seedFactory: (acc, _) => acc ?? [],
        aggregator: (acc, next) =>
        {
            acc.Add(next);
            return acc;
        },
        bufferTime: TimeSpan.FromMilliseconds(200)
    );

static readonly FlowSyncAggTask<int, List<int>> BatchedWork =
    FlowSyncAggTask.Create<int, List<int>>(async (ids, ct) =>
    {
        await Task.Delay(100, ct);
        return ids.Sum();
    });

public Task<int> CallerAsync(int id)
{
    // Calls made within the buffer window share one aggregated execution.
    // Calls arriving while execution is in progress are aggregated into the next batch.
    return BatchedWork.CoalesceInGroupUsing(AggStrategy, id, groupKey: "orders").StartAsTask();
}
```

**Agg remarks**
1. `seedFactory` signature is `Func<TAcc?, int, TAcc>`:
the first argument is the previous accumulator (or `null` for the first batch), and the second argument is the batch index.
2. Use `seedFactory: (acc, _) => acc ?? ...` for rolling accumulation across batches.
3. Use `seedFactory: (_, _) => ...` to reset and start a fresh accumulator for each next batch.
4. A new batch appears when new overlapping requests arrive after the current buffer window has already been consumed (typically while the current batch is already running). For a given `groupKey`, batches are processed sequentially in one logical pipeline (no parallel batch execution inside the same group).
