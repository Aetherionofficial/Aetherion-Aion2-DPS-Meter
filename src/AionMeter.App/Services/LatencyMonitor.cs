using System.Net;
using System.Net.NetworkInformation;
using AionMeter.Core.Capture;

namespace AionMeter.App.Services;

public sealed record LatencyTarget(IPAddress Address, int Port, bool Approximate);
public sealed record LatencyDisplay(string Text, string Color, string Tooltip)
{
    public static LatencyDisplay Waiting { get; } = new("--", "#EB6B74", "Latenz zum Gameserver · noch keine Messung");
}

/// <summary>Presentation-only network telemetry; never accesses the combat parser or sends captured packets.</summary>
public sealed class LatencyHistory
{
    private readonly Queue<(DateTimeOffset At, long? Ms)> _samples = new();
    private LatencyTarget? _target;

    public LatencyDisplay Add(LatencyTarget target, long? milliseconds, DateTimeOffset now)
    {
        if (target != _target) { _samples.Clear(); _target = target; }
        while (_samples.TryPeek(out var first) && now - first.At >= TimeSpan.FromMinutes(1)) _samples.Dequeue();
        _samples.Enqueue((now, milliseconds));
        var recent = _samples.TakeLast(5).Where(s => s.Ms.HasValue).Select(s => s.Ms!.Value).ToArray();
        var minute = _samples.Where(s => s.Ms.HasValue).Select(s => s.Ms!.Value).ToArray();
        var average = milliseconds.HasValue && recent.Length > 0 ? (long?)Math.Round(recent.Average()) : null;
        var color = average switch { null => "#EB6B74", < 60 => "#65D98A", <= 120 => "#E7C95B", <= 200 => "#F0A253", _ => "#EB6B74" };
        var range = minute.Length == 0 ? "Min/Max: --" : $"Min: {minute.Min()} ms · Max: {minute.Max()} ms";
        var source = target.Approximate ? "Näherungswert · Ersatzhost" : "AION2-Server";
        return new(average is { } value ? $"{value} ms" : "--", color,
            $"Latenz zum Gameserver\n{source}: {target.Address}{(target.Port > 0 ? $":" + target.Port : "")} (ICMP)\n{range} · letzte Minute\nDurchschnitt der letzten 5 Messungen; Ausfälle werden nicht als 0 gezählt.");
    }
}

public sealed class LatencyMonitor : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    public Task Completion { get; }

    public LatencyMonitor(Action<LatencyDisplay> changed,
        Func<LatencyTarget>? target = null,
        Func<LatencyTarget, CancellationToken, Task<long?>>? probe = null)
    {
        Completion = Task.Run(async () =>
        {
            var history = new LatencyHistory();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            try
            {
                do
                {
                    var endpoint = (target ?? FindTarget)();
                    long? latency = null;
                    try { latency = await (probe ?? Probe)(endpoint, _stop.Token); }
                    catch (Exception ex) when (ex is PingException or System.Net.Sockets.SocketException or InvalidOperationException) { }
                    _stop.Token.ThrowIfCancellationRequested();
                    changed(history.Add(endpoint, latency, DateTimeOffset.UtcNow));
                } while (await timer.WaitForNextTickAsync(_stop.Token));
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }

    public static LatencyTarget FindTarget()
    {
        // Same process-owned TCP table and game port used by LiveCapture; no guessing from unrelated traffic.
        var connections = GameProcessLocator.FindConnections(GameProcessLocator.FindGameProcessIds())
            .Where(c => c.RemotePort == PacketPipeline.GamePort && !IPAddress.IsLoopback(c.RemoteIp))
            .Select(c => new LatencyTarget(c.RemoteIp, c.RemotePort, false)).Distinct().ToArray();
        return connections.Length == 1 ? connections[0] : new(IPAddress.Parse("1.1.1.1"), 0, true);
    }

    private static async Task<long?> Probe(LatencyTarget target, CancellationToken cancellation)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(target.Address, TimeSpan.FromMilliseconds(1200), cancellationToken: cancellation);
        return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
    }

    public void Dispose() => _stop.Cancel();
}
