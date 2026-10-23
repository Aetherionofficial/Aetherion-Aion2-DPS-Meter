using System.Net;
using AionMeter.App.Services;

namespace AionMeter.App.Tests;

public sealed class LatencyTests
{
    private static readonly LatencyTarget Server = new(IPAddress.Parse("192.0.2.1"), 13328, false);

    [Theory]
    [InlineData(59, "#65D98A")]
    [InlineData(60, "#E7C95B")]
    [InlineData(120, "#E7C95B")]
    [InlineData(121, "#F0A253")]
    [InlineData(200, "#F0A253")]
    [InlineData(201, "#EB6B74")]
    public void Thresholds(long value, string color)
    {
        var display = new LatencyHistory().Add(Server, value, DateTimeOffset.UnixEpoch);
        Assert.Equal($"{value} ms", display.Text);
        Assert.Equal(color, display.Color);
    }

    [Fact]
    public void Rolling_average_timeout_minute_range_and_target_changes()
    {
        var history = new LatencyHistory();
        var time = DateTimeOffset.UnixEpoch;
        foreach (var sample in new long[] { 999, 20, 30, 40, 50, 60 })
            history.Add(Server, sample, time += TimeSpan.FromSeconds(2));
        var average = history.Add(Server, 70, time += TimeSpan.FromSeconds(2));
        Assert.Equal("50 ms", average.Text);
        Assert.Contains("Min: 20 ms", average.Tooltip);
        Assert.Contains("Max: 999 ms", average.Tooltip);
        var timeout = history.Add(Server, null, time += TimeSpan.FromSeconds(2));
        Assert.Equal("--", timeout.Text); Assert.Equal("#EB6B74", timeout.Color);
        var expired = history.Add(Server, 42, time += TimeSpan.FromMinutes(1));
        Assert.Equal("42 ms", expired.Text);
        Assert.Contains("Min: 42 ms · Max: 42 ms", expired.Tooltip);
        var fallback = history.Add(Server with { Approximate = true }, 90, time);
        Assert.Equal("90 ms", fallback.Text); Assert.Contains("Näherungswert", fallback.Tooltip);
    }

    [Fact]
    public async Task Background_probe_is_cancellable_without_network_or_ui_dispatcher()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = 0;
        using var monitor = new LatencyMonitor(_ => Interlocked.Increment(ref callbacks), () => Server,
            async (_, cancellation) => { started.SetResult(); await Task.Delay(Timeout.Infinite, cancellation); return 1; });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        monitor.Dispose();
        await monitor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, callbacks);
    }
}
