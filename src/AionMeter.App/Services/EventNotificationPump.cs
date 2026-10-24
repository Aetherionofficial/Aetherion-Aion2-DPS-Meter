using System.Windows.Threading;
using AionMeter.App.Windows;

namespace AionMeter.App.Services;

/// <summary>UI-thread snapshot consumer. The existing combat/overlay timer is deliberately untouched.</summary>
public sealed class EventNotificationPump : IDisposable
{
    private readonly MeterService _meter;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private long _seen;
    public EventNotificationPump(MeterService meter)
    {
        _meter = meter; _seen = meter.EventLog.GetSnapshot().Version;
        _timer.Tick += OnTick; _timer.Start();
    }
    private void OnTick(object? sender, EventArgs args)
    {
        var snapshot = _meter.EventLog.GetSnapshot();
        if (snapshot.Version == _seen) return;
        var fresh = snapshot.Events.Where(e => e.Sequence > _seen && (e.Notify || e.Sound)).ToArray();
        _seen = snapshot.Version;
        if (fresh.Any(e => e.Sound))
            try { System.Media.SystemSounds.Asterisk.Play(); } catch (System.ComponentModel.Win32Exception) { }
        var hints = fresh.Where(e => e.Notify).ToArray();
        if (hints.Length > 0)
        {
            var last = hints[^1];
            EventToastWindow.ShowMessage(hints.Length == 1 ? last.Type : $"{hints.Length} neue Ereignisse",
                $"{last.Zone ?? "Gebiet unbekannt"}\n{last.Details[..Math.Min(last.Details.Length, 260)]}");
        }
    }
    public void Dispose() { _timer.Stop(); _timer.Tick -= OnTick; EventToastWindow.Dismiss(); }
}
