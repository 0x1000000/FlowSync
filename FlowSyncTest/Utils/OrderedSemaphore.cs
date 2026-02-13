using System.Runtime.CompilerServices;

namespace FlowSyncTest.Utils;

public class OrderedSemaphore(OrderedSemaphore.ContinuationRunMode continuationRunMode = OrderedSemaphore.ContinuationRunMode.PreferSingleThread)
{
    public enum ContinuationRunMode
    {
        PreferSingleThread,
        ForceNewThread,
    }

    private readonly Dictionary<int, OrderedSemaphoreAwaiter?> _queue = new();

    private bool _started;

    private void ContinuationThread()
    {
        lock (this._queue)
        {
            for (var i = 0; i < this._queue.Count; i++)
            {
                if (this._queue.TryGetValue(i, out var entry))
                {
                    if (entry != null)
                    {
                        entry.SetCompleted();
                        this._queue[i] = null;
                        if (continuationRunMode == ContinuationRunMode.ForceNewThread)
                        {
                            new Thread(this.ContinuationThread).Start();
                            return;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }

    public OrderedSemaphoreAwaiter WaitAsync(int index)
    {
        lock (this._queue)
        {
            if (this._queue.ContainsKey(index))
            {
                return OrderedSemaphoreAwaiter.Completed;
            }

            if (this._started || (!this._started && index == 0))
            {
                this._started = true;

                Task.Run(this.ContinuationThread);
            }

            var awaiter = new OrderedSemaphoreAwaiter();
            this._queue[index] = awaiter;
            return awaiter;
        }
    }

    public class OrderedSemaphoreAwaiter : INotifyCompletion
    {
        internal static readonly OrderedSemaphoreAwaiter Completed = new() { IsCompleted = true };

        private readonly ManualResetEventSlim _continuationRegistered = new(false);

        private readonly object _sync = new();

        private Exception? _error;

        private Action? _continuation;

        public bool IsCompleted { get; private set; }

        public void OnCompleted(Action continuation)
        {
            lock (this._sync)
            {
                if (this._error != null)
                {
                    continuation();
                    return;
                }

                if (this._continuation != null)
                {
                    this._continuation += continuation;
                }
                else
                {
                    this._continuation = continuation;
                }

                this._continuationRegistered.Set();
            }
        }

        public void GetResult()
        {
            lock (this._sync)
            {
                if (!this.IsCompleted)
                {
                    throw new Exception("Not Completed");
                }

                if (this._error != null)
                {
                    throw this._error;
                }
            }
        }

        internal void SetCompleted()
        {
            while (true)
            {
                lock (this._sync)
                {
                    if (this.IsCompleted)
                    {
                        this._error = new Exception("Cannot complete already completed awaiter");
                        return;
                    }

                    if (this._continuation != null)
                    {
                        this.IsCompleted = true;
                        this._continuation();
                        this._continuation = null;
                        return;
                    }
                }

                if (!this._continuationRegistered.Wait(100 /*ms*/))
                {
                    lock (this._sync)
                    {
                        if (this._continuation == null)
                        {
                            this._error = new TimeoutException("Could not wait till subscription");
                            this.IsCompleted = true;
                            return;
                        }
                    }
                }
            }
        }

        public OrderedSemaphoreAwaiter GetAwaiter() => this;
    }
}
