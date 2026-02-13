using FlowSync.Utils;

namespace FlowSync;

/// <summary>
/// Coalescing strategy that queues calls per group and processes them sequentially.
/// </summary>
public class QueueCoalescingSyncStrategy<T> : IFlowSyncStrategy<T>
{
    private readonly AtomicUpdateDictionary<object, AwaiterQueue> _storage = new();

    public FlowSyncTaskAwaiter<T> EnterSyncSection(
        IFlowSyncFactory<T> flowFactory,
        object? groupKey)
    {
        return this._storage.AddOrUpdate(
                key: groupKey ?? AtomicUpdateDictionary.DefaultKey,
                arg: (self: this, flowStarter: flowFactory),
                addValueFactory: static (k, args) =>
                    args.self.SubscribeRemoval(k, new AwaiterQueue(args.flowStarter.CreateAwaiter())),
                updateValueFactory: static (k, args, queue) =>
                {
                    var flowSyncTaskAwaiter = args.flowStarter.CreateAwaiter();
                    if (!queue.Enqueue(flowSyncTaskAwaiter))
                    {
                        queue = args.self.SubscribeRemoval(k, new AwaiterQueue(flowSyncTaskAwaiter));
                    }

                    return queue;
                }
            )
            .Awaiter;
    }

    private AwaiterQueue SubscribeRemoval(object key, AwaiterQueue queue)
    {
        queue.Awaiter.OnCompleted(
            () => this._storage.TryScheduleRemoval(key, currentQueue => currentQueue == queue)
        );
        return queue;
    }

    private class AwaiterQueue
    {
        private readonly Queue<FlowSyncTaskAwaiter<T>> _queue;

        public readonly FlowSyncTaskAwaiter<T> Awaiter;

        public AwaiterQueue(FlowSyncTaskAwaiter<T> first)
        {
            this._queue = new Queue<FlowSyncTaskAwaiter<T>>();
            this._queue.Enqueue(first);
            this.Awaiter = this.ProcessQueue().CoalesceInDefaultGroupUsing(new NoCoalescingCancellableSyncStrategy<T>());
        }

        public bool Enqueue(FlowSyncTaskAwaiter<T> e)
        {
            lock (this._queue)
            {
                if (this._queue.Count > 0)
                {
                    this._queue.Enqueue(e);
                    return true;
                }

                return false;
            }
        }

        public void Cancel()
        {
            lock (this._queue)
            {
                foreach (var awaiter in this._queue)
                {
                    awaiter.Cancel(true);
                }
            }
        }

        private async FlowSyncTask<T> ProcessQueue()
        {
            T result = default!;

            FlowSyncTaskAwaiter<T>? awaiter;

            do
            {
                lock (this._queue)
                {
                    if (!this._queue.TryPeek(out awaiter))
                    {
                        awaiter = null;
                        continue;
                    }
                }

                result = await awaiter;

                lock (this._queue)
                {
                    this._queue.Dequeue();
                }
            } while (awaiter != null);

            return result;
        }
    }

    public void Cancel(object groupKey) => this._storage.TryRead(groupKey, this, static (_, _, e) => e.Cancel());

    public void CancelAll() => this._storage.ReadAll(this, static (_, _, e) => e.Cancel());

    public void Dispose() => this._storage.Dispose();
}

