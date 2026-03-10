# FlowSync

FlowSync is a lightweight async coalescing library for .NET.

It lets multiple callers share one in-flight operation and choose what should happen when calls overlap: keep the first call, keep the latest call, queue them, debounce them, or batch them.

## Resources

- Demo site: [FlowSync Demo](https://0x1000000.github.io/FlowSync/)
- Video intro: [YouTube Video](https://www.youtube.com/watch?v=wwSU83Qpjts)
- Article: [5 Common Async Coalescing Patterns](https://itnext.io/5-common-async-coalescing-patterns-db7b1cac1507?source=friends_link&sk=7d181a06c15d308485cbf6c205955907)

## What You Get

- Keep business logic separate from concurrency control
- Reuse the same operation with different overlap strategies
- Cancel stale work when newer calls matter more
- Batch bursty requests into fewer I/O operations

## Quick Example

```csharp
using FlowSync;

static readonly IFlowSyncStrategy<string> Strategy = new UseLastCoalescingSyncStrategy<string>();

public async FlowSyncTask<string> SearchCoreAsync(string query)
{
    var ctx = await FlowSyncTask.GetCancellationContext();
    await Task.Delay(150, ctx.CancellationToken); // real I/O goes here
    return $"result:{query}";
}

public async Task<string> SearchAsync(string query) =>
    await this.SearchCoreAsync(query)
        .CoalesceInGroupUsing(Strategy, groupKey: "search");
```

`SearchCoreAsync` contains the business logic. `CoalesceInGroupUsing(...)` applies the concurrency behavior.

`FlowSyncTask` is lazy, so the coalesced operation starts only when you `await` it or call `Start()` or `StartAsTask()`.

## Contents

- [Core Idea](#core-idea)
- [Why It Helps](#why-it-helps)
- [Strategies](#strategies)
- [Install](#install)
- [Usage with `FlowSyncTask`](#usage-with-flowsynctask)
- [Usage with Existing `Task<T>` Code](#usage-with-existing-taskt-code)
- [Batching Multiple Calls](#batching-multiple-calls)
- [Cookbook](#cookbook)

## Core Idea

Without FlowSync, async coordination often ends up mixed into business code through `TaskCompletionSource`, `CancellationTokenSource`, timers, queues, and locks.

FlowSync separates two concerns:

- Business logic: what the operation does
- Concurrency semantics: what should happen when calls overlap

In practice, that means:

- Concurrent calls are coalesced per `groupKey`
- You choose a strategy for contention resolution
- Debouncing and batching are available for bursty workloads
- Your method stays close to a normal async method
- The method itself does not need to manage synchronization logic

The operation stays focused on its real work. The call site decides how overlapping calls behave.

## Why It Helps

Modern applications often issue multiple async requests for the same logical resource at the same time.

Common examples:

- A search box triggers requests on every keystroke
- Several workflows ask for the same data concurrently
- Multiple services request the same cache entry

Without coordination, that usually leads to:

- Redundant execution
- Stale or out-of-order results
- Race conditions
- Wasted CPU and I/O

## Strategies

Each coalescing pattern is implemented as a strategy. The abstraction stays the same, but the overlap behavior changes.

These are explicit policy choices. The operation stays focused on business logic while the strategy defines how contention is handled.

FlowSync provides five interchangeable strategies:

- `UseFirstCoalescingSyncStrategy<T>`: the first caller runs, and later callers join the same result
- `UseLastCoalescingSyncStrategy<T>`: newer callers replace older ones, and older calls are canceled
- `QueueCoalescingSyncStrategy<T>`: callers are queued and executed sequentially
- `DeBounceCoalescingSyncStrategy<T>`: rapid-fire calls are collapsed into fewer executions
- `AggCoalescingSyncStrategy<T, TArg, TAcc>`: inputs are buffered, aggregated into an accumulator, and processed as a batch

Most of the time, the operation itself does not change. You swap the synchronization policy instead of rewriting the method.

## Install

```bash
dotnet add package FlowSync
```

## Usage with `FlowSyncTask`

Use this form when your method returns `FlowSyncTask<T>` directly.

Each invocation enters a strategy-managed pipeline for its `groupKey`, so overlapping calls may be shared, replaced, queued, or canceled depending on the chosen strategy.

```csharp
using FlowSync;

// Strategy keeps per-group coalescing state across calls, so share one instance.
static readonly IFlowSyncStrategy<int> Strategy = new UseLastCoalescingSyncStrategy<int>();

// FlowSyncTask<T> is a custom awaitable type, not Task<T> or ValueTask<T>.
public async FlowSyncTask<int> FetchAsync(int id)
{
    var ctx = await FlowSyncTask.GetCancellationContext();

    // ctx.CancellationToken covers both:
    // 1) explicit external cancellation
    // 2) strategy-enforced cancellation due to overlap
    // ctx.IsCancelledLocally is true only for case (2).
    await Task.Delay(Random.Shared.Next(50, 501), ctx.CancellationToken);
    return id + 42;
}

public async Task<int> CallerAsync(int id)
{
    return await FetchAsync(id).CoalesceInGroupUsing(Strategy, groupKey: id);
}

public void CancelFetch(int id)
{
    Strategy.Cancel(id);
}
```

What to remember:

1. `FlowSyncTask.GetCancellationContext()` returns a combined cancellation context. `CancellationToken` is canceled for either external cancellation or strategy-enforced cancellation, while `IsCancelledLocally` is `true` only for strategy-enforced cancellation.
2. `CoalesceInGroupUsing(...)` returns a lazy awaiter. Work starts only when it is awaited or explicitly started.

## Usage with Existing `Task<T>` Code

Use this when your code already returns `Task<T>` and you do not want to change method signatures yet.

`FlowSyncTask.Create(...)` adapts the regular task into the same coalescing pipeline, so the behavior matches the `FlowSyncTask<T>` approach.

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

This is usually the simplest migration path for an existing codebase.

## Batching Multiple Calls

Use the aggregation strategy when many small requests should be combined into fewer larger executions.

Arguments are buffered for `bufferTime`, merged into an accumulator, and processed as one run per group. If new calls arrive while a batch is already running, they are collected for the next batch.

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
    return BatchedWork.CoalesceInGroupUsing(AggStrategy, id, groupKey: "orders").StartAsTask();
}
```

What to remember:

1. `seedFactory` has the signature `Func<TAcc?, int, TAcc>`. The first argument is the previous accumulator or `null`, and the second argument is the batch index.
2. Use `seedFactory: (acc, _) => acc ?? ...` for rolling accumulation across batches.
3. Use `seedFactory: (_, _) => ...` to reset the accumulator for each batch.
4. For a given `groupKey`, batches are processed sequentially in one logical pipeline. Batches in the same group do not run in parallel.

## Cookbook

### Recipe 1: Cancel Stale SQL Work with `UseLast`

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

For the same `groupKey`, a newer call cancels the previous in-flight execution.

### Recipe 2: Batch `GetUserInfo` Requests with `Agg`

Collect user IDs for 500ms, run one EF query, then let each caller read its own result from the shared dictionary.

This is useful under bursty traffic because it replaces many small concurrent database calls with one batched query.

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
