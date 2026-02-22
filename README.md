# FlowSync

FlowSync is a lightweight **async coalescing** library for .NET. It lets multiple callers share a single in-flight operation while choosing a **strategy** for how competing calls are handled.

## Demo Site: [FlowSync Demo](https://0x1000000.github.io/FlowSync/)

## Video Intro: [YouTube Video](https://www.youtube.com/watch?v=wwSU83Qpjts)

## Medium Article: [5 Common Async Coalescing Patterns](https://itnext.io/5-common-async-coalescing-patterns-db7b1cac1507?source=friends_link&sk=7d181a06c15d308485cbf6c205955907)

## Contents

- [Core Idea](#core-idea)
- [Quick Example](#quick-example)
- [Problem: Concurrent Async Stampede](#problem-concurrent-async-stampede)
- [Strategies](#strategies)
- [Install](#install)
- [Usage (FlowSyncTask)](#usage-flowsynctask)
- [Remarks](#remarks)
- [Usage (wrap a regular Task)](#usage-wrap-a-regular-task)
- [Usage (aggregate multiple calls into batches)](#usage-aggregate-multiple-calls-into-batches)
- [Agg Remarks](#agg-remarks)
- [Cookbook](#cookbook)
- [Recipe 1: Cancel stale heavy SQL query (`UseLast`)](#recipe-1-cancel-stale-heavy-sql-query-uselast)
- [Recipe 2: Combine `GetUserInfo` requests into one batch (single I/O operation), then distribute results to callers (`Agg`)](#recipe-2-combine-getuserinfo-requests-into-one-batch-then-distribute-results-to-callers-agg)

## Core Idea

Instead of manually wiring `TaskCompletionSource`, `CancellationTokenSource`, queues, timers, and locking logic for every use case, FlowSync separates:

- Business logic (what the operation does)
- Concurrency semantics (how overlapping calls behave)

This separation is the key design principle of the library.

In practice:

- Coalesce concurrent calls per `groupKey`.
- Choose how to resolve contention: use-first, use-last, queue, debounce, or aggregate-and-batch.
- Optional debouncing/buffering for bursty workloads.
- Keep methods as normal async operations that return `FlowSyncTask<T>`.
- The method itself contains no synchronization logic; it simply respects the cancellation context provided by FlowSync.

Then, at the call site, you choose the strategy.

## Quick Example

```csharp
using FlowSync;

static readonly IFlowSyncStrategy<string> Strategy = new UseLastCoalescingSyncStrategy<string>();

public async FlowSyncTask<string> SearchCoreAsync(string query)
{
    var ctx = await FlowSyncTask.GetCancellationContext();
    await Task.Delay(150, ctx.CancellationToken); // real I/O goes here; old irrelevant requests are canceled.
    return $"result:{query}";
}

public async Task<string> SearchAsync(string query) =>
    await this.SearchCoreAsync(query)
        .CoalesceInGroupUsing(Strategy, groupKey: "search");
```

`SearchCoreAsync` holds business logic. `CoalesceInGroupUsing(...)` applies concurrency semantics.
`FlowSyncTask` is lazy: the coalesced operation starts when you `await` it (or call `Start()` / `StartAsTask()`).

## Problem: Concurrent Async Stampede

In modern applications, multiple asynchronous requests often target the same logical resource concurrently.

Examples:

- UI typing triggers multiple search requests
- Multiple workflows request the same data
- Distributed services request the same cache entry

Without coordination, this leads to:

- redundant execution
- race conditions
- stale or out-of-order results
- wasted CPU and I/O

## Strategies

Each coalescing pattern is implemented as a strategy. All strategies follow the same abstraction but differ in semantics:

- Should previous calls be ignored?
- Should they be canceled?
- Should execution be delayed?
- Should calls be queued?
- Should inputs be aggregated?

FlowSync answers these questions with five interchangeable strategies:

- `UseFirstCoalescingSyncStrategy<T>`: first caller runs, later callers join and observe the same result.
- `UseLastCoalescingSyncStrategy<T>`: later callers replace earlier ones; earlier calls are canceled.
- `QueueCoalescingSyncStrategy<T>`: callers are queued and executed sequentially (spooler-like).
- `DeBounceCoalescingSyncStrategy<T>`: debounces rapid-fire calls into fewer executions.
- `AggCoalescingSyncStrategy<T, TArg, TAcc>`: buffers incoming arguments for a time window, aggregates them into an accumulator, then runs one execution per batch. If new arguments arrive while a batch is running, they are collected for the next batch.

The operation does not change. Only the synchronization policy changes (aggregation is a small exception). This makes concurrency behavior explicit and configurable instead of implicit and scattered across code.

## Install

```bash
dotnet add package FlowSync
```

## Usage (FlowSyncTask)

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

### Remarks

1. `FlowSyncTask.GetCancellationContext()` returns a combined cancellation context:
`CancellationToken` is canceled for either external explicit cancellation or strategy-enforced cancellation, and `IsCancelledLocally` is `true` only for strategy-enforced cancellation (for example in overlapping `UseLast` calls).
2. `CoalesceInGroupUsing(...)` returns a lazy awaiter. The underlying work does not start until it is awaited, `Start()` is called, or `StartAsTask()` is called.

## Usage (wrap a regular Task)

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

## Usage (aggregate multiple calls into batches)

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

### Agg Remarks

1. `seedFactory` signature is `Func<TAcc?, int, TAcc>`:
the first argument is the previous accumulator (or `null` for the first batch), and the second argument is the batch index.
2. Use `seedFactory: (acc, _) => acc ?? ...` for rolling accumulation across batches.
3. Use `seedFactory: (_, _) => ...` to reset and start a fresh accumulator for each next batch.
4. A new batch appears when new overlapping requests arrive after the current buffer window has already been consumed (typically while the current batch is already running). For a given `groupKey`, batches are processed sequentially in one logical pipeline (no parallel batch execution inside the same group).

## Cookbook

### Recipe 1: Cancel stale heavy SQL query (`UseLast`)

When the same logical request is triggered repeatedly, keep only the latest call and cancel the older one.

```csharp
using FlowSync;
using Microsoft.Data.SqlClient;

static readonly IFlowSyncStrategy<int> HeavyQueryStrategy =
    new UseLastCoalescingSyncStrategy<int>();

public async FlowSyncTask<int> RunHeavyQueryCoreAsync(string connectionString)
{
    var ctx = await FlowSyncTask.GetCancellationContext();

    await using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync(ctx.CancellationToken);

    await using var cmd = conn.CreateCommand();
    // Simulate a heavy SQL operation.
    cmd.CommandText = "WAITFOR DELAY '00:01:00'; SELECT 1;";
    return (int)(await cmd.ExecuteScalarAsync(ctx.CancellationToken) ?? 0);
}

public async Task<int> RunHeavyQueryAsync(string connectionString) =>
    await RunHeavyQueryCoreAsync(connectionString)
        .CoalesceInGroupUsing(HeavyQueryStrategy, groupKey: "heavy-query");
```

For the same `groupKey`, a newer call cancels the previous in-flight one.

### Recipe 2: Combine `GetUserInfo` requests into one batch, then distribute results to callers (`Agg`)

Collect requested user IDs for 500ms, combine them into one batch, run one EF query, then distribute the shared result dictionary to all callers in that batch.
This improves performance under bursty traffic: instead of many small concurrent DB queries, the strategy collapses them into a single query, significantly reducing database and system load.

```csharp
using FlowSync;
using Microsoft.EntityFrameworkCore;

public sealed record UserInfo(int Id, string Name, string Email);

static readonly IFlowSyncAggStrategy<IReadOnlyDictionary<int, UserInfo>, int, HashSet<int>> GetUsersAggStrategy =
    new AggCoalescingSyncStrategy<IReadOnlyDictionary<int, UserInfo>, int, HashSet<int>>(
        seedFactory: (_, _) => new HashSet<int>(),
        aggregator: (acc, userId) =>
        {
            acc.Add(userId);
            return acc;
        },
        bufferTime: TimeSpan.FromMilliseconds(500)
    );

static readonly FlowSyncAggTask<IReadOnlyDictionary<int, UserInfo>, HashSet<int>> GetUsersBatchTask =
    FlowSyncAggTask.Create<IReadOnlyDictionary<int, UserInfo>, HashSet<int>>(async (ids, ct) =>
    {
        await using var db = new AppDbContext();

        var users = await db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserInfo(u.Id, u.Name, u.Email))
            .ToListAsync(ct);

        return users.ToDictionary(u => u.Id, u => u);
    });

public async Task<UserInfo?> GetUserInfoAsync(int userId)
{
    var shared = await GetUsersBatchTask
        .CoalesceInGroupUsing(GetUsersAggStrategy, userId, groupKey: "users");

    return shared.TryGetValue(userId, out var user) ? user : null;
}
```

All callers in the same batch receive the same dictionary instance and read their own entry by `userId`.
The key benefit is query coalescing: one batched query per window instead of N per-caller queries.
This improvement is transparent for callers and does not affect their business logic.
