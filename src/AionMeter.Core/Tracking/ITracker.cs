using AionMeter.Core.Events;

namespace AionMeter.Core.Tracking;

/// <summary>Immutable point-in-time state; callers must never receive a tracker's mutable collections.</summary>
public interface ITrackerSnapshot { }

/// <summary>Implementations synchronize their state and never access GUI objects.</summary>
public interface ITracker
{
    void OnEvent(GameEvent gameEvent);
    ITrackerSnapshot GetSnapshot();
    void Reset();
}
