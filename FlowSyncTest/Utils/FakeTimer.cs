namespace FlowSyncTest.Utils;

public sealed class FakeTimer(int number)
{
    private record struct Entry(int Id, int Remaining, TaskCompletionSource? CompletionSource);

    private readonly Dictionary<int, Entry> _entries = new(number);

    public static IReadOnlyList<FakeTimeLine> CreateTimeLines(int number)
    {
        var initialTimeLines = new List<FakeTimeLine>(number);

        var t = new FakeTimer(number);

        for (var i = 0; i < number; i++)
        {
            initialTimeLines.Add(new FakeTimeLine(i, t));
        }

        return initialTimeLines;
    }

    private Task Delay(int timeLineId, int delayMs)
    {
        lock (this._entries)
        {
            if (!this._entries.TryGetValue(timeLineId, out var entry))
            {
                entry = new Entry(
                    timeLineId,
                    delayMs,
                    new TaskCompletionSource()
                );
                this._entries.Add(timeLineId, entry);
            }
            else
            {
                throw new Exception("Cannot execute several delays is row");
            }

            if (entry.CompletionSource == null)
            {
                throw new Exception("This Time Line Was Disposed");
            }

            if (this._entries.Count > number)
            {
                throw new Exception("Cannot be");
            }

            this.TryRunClosestUnsafe(true);

            return entry.CompletionSource.Task;
        }
    }

    private void TryRunClosestUnsafe(bool notClosing)
    {
        if (this._entries.Count == number)
        {
            Entry? closestEntry = null;

            foreach (var eEntry in this._entries.Values)
            {
                if (eEntry.CompletionSource != null /*Not Completed Timeline*/)
                {
                    if (!closestEntry.HasValue)
                    {
                        closestEntry = eEntry;
                    }
                    else if (closestEntry.Value.Remaining > eEntry.Remaining)
                    {
                        closestEntry = eEntry;
                    }
                }
            }

            if (!closestEntry.HasValue && notClosing /*If delay then there should be at least one active timeline*/)
            {
                throw new Exception("Cannot be");
            }

            if (closestEntry.HasValue)
            {
                foreach (var (eTimeLineId, eEntry) in this._entries)
                {
                    this._entries[eTimeLineId] = eEntry with { Remaining = eEntry.Remaining - closestEntry.Value.Remaining };
                }

                this._entries.Remove(closestEntry.Value.Id);
                closestEntry.Value.CompletionSource!.SetResult();
            }
        }
    }

    private void CloseTimeLine(int timeLineId)
    {
        lock (this._entries)
        {
            if (!this._entries.TryGetValue(timeLineId, out var entry))
            {
                entry = new Entry(
                    timeLineId,
                    0,
                    null
                );
                this._entries.Add(timeLineId, entry);

                this.TryRunClosestUnsafe(false);
            }
            else if (entry.CompletionSource != null)
            {
                throw new Exception("Cannot dispose a timeline with an awaiter");
            }
        }
    }

    public sealed class FakeTimeLine : IAsyncDisposable
    {
        private readonly int _timeLineId;

        private readonly FakeTimer _fakeTimer;

        internal FakeTimeLine(int timeLineId, FakeTimer fakeTimer)
        {
            this._timeLineId = timeLineId;
            this._fakeTimer = fakeTimer;
        }

        public Task FakeDelay(int delayMs) => this._fakeTimer.Delay(this._timeLineId, delayMs);

        public ValueTask DisposeAsync()
        {
            this._fakeTimer.CloseTimeLine(this._timeLineId);
            return ValueTask.CompletedTask;
        }
    }
}
