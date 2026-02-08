# FlowSync

FlowSync is a lightweight **async coalescing** library for .NET. It lets multiple callers share a single in-flight operation while choosing a **strategy** for how competing calls are handled.

**Key Ideas**
- Coalesce concurrent calls per `resourceId`.
- Choose how to resolve contention: use-first, use-last, or queue.
- Optional debouncing for bursty workloads.

**Strategies**
- `UseFirstCoalescingSyncStrategy<T>`: first caller runs, later callers join and observe the same result.
- `UseLastCoalescingSyncStrategy<T>`: later callers replace earlier ones; earlier calls are canceled.
- `QueueCoalescingSyncStrategy<T>`: callers are queued and executed sequentially (spooler-like).
- `DeBounceCoalescingSyncStrategy<T>`: debounces rapid-fire calls into fewer executions.

**Install**
```bash
dotnet add package FlowSync
```

**Usage (FlowSyncTask)**
```csharp
using FlowSync;

static readonly IFlowSyncStrategy<int> Strategy = new UseLastCoalescingSyncStrategy<int>();

public async FlowSyncTask<int> FetchAsync(int id)
{
    var ctx = await FlowSyncTask.GetCancellationContext();
    // Do work here and respect ctx.CancellationToken if needed.
    await Task.Delay(50, ctx.CancellationToken);
    return id + 42;
}

public async Task<int> CallerAsync(int id)
{
    var awaiter = FetchAsync(id).CoalesceUsing(Strategy, resourceId: id).Start();
    return await awaiter;
}
```

**Usage (wrap a regular Task)**
```csharp
using FlowSync;

static readonly IFlowSyncStrategy<int> Strategy = new UseFirstCoalescingSyncStrategy<int>();

public Task<int> CallerAsync(int id)
{
    return FlowSyncTask
        .Create(ct => WorkAsync(id, ct))
        .CoalesceUsing(Strategy, resourceId: id);
}

static async Task<int> WorkAsync(int id, CancellationToken ct)
{
    await Task.Delay(50, ct);
    return id + 42;
}
```
