using AionMeter.Core.Events;

namespace AionMeter.Core.Tracking;

/// <summary>Additional observers only: CombatTracker retains its existing subscription and locks.</summary>
public sealed class TrackerDispatcher(Action<Exception>? reportError = null)
{
    private readonly object _gate = new();
    private readonly List<ITracker> _trackers = [];
    private readonly Queue<GameEvent> _pending = new();
    private bool _publishing;

    public IDisposable Register(ITracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        lock (_gate)
        {
            if (_trackers.Contains(tracker)) throw new ArgumentException("Der Tracker ist bereits registriert.", nameof(tracker));
            _trackers.Add(tracker);
        }
        return new Registration(this, tracker);
    }

    /// <summary>Concurrent publishers are serialized by arrival at this lock, not sorted by packet timestamps.
    /// Reentrant publications wait until every observer has received the current event.</summary>
    public void Publish(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        lock (_gate)
        {
            _pending.Enqueue(gameEvent);
            if (_publishing) return;
            _publishing = true;
            try
            {
                while (_pending.TryDequeue(out var next))
                    foreach (var tracker in _trackers.ToArray())
                        try { tracker.OnEvent(next); }
                        catch (Exception error) when (error is not OutOfMemoryException)
                        {
                            // An optional module must never interrupt the capture or another tracker.
                            try { reportError?.Invoke(error); }
                            catch (Exception reportFailure) when (reportFailure is not OutOfMemoryException) { }
                        }
            }
            finally { _publishing = false; }
        }
    }

    private sealed class Registration(TrackerDispatcher owner, ITracker tracker) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (owner._gate) owner._trackers.Remove(tracker);
        }
    }
}
