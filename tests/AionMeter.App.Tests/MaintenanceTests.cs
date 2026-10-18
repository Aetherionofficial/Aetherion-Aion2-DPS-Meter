using System.Net;
using System.Net.Http;
using System.Text.Json;
using AionMeter.App.Services;

namespace AionMeter.App.Tests;

public sealed class MaintenanceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "{\"enabled\":false,\"message\":\"Bereit\",\"revision\":0}";
        public HttpStatusCode Code = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent(Body) });
    }

    [Fact]
    public void Maintenance_error_is_a_user_message_instead_of_raw_json()
    {
        var state = MaintenanceStatus.FromError("{\"error\":\"app_maintenance\",\"maintenance\":{\"enabled\":true,\"message\":\"Bitte später erneut versuchen.\",\"revision\":1}}");
        Assert.NotNull(state);
        Assert.Equal("Bitte später erneut versuchen.", state.Message);
        Assert.Null(MaintenanceStatus.FromError("{\"error\":\"auth_required\"}"));
        Assert.Null(MaintenanceStatus.FromError("{\"error\":\"app_maintenance\",\"maintenance\":{}}"));
    }

    private sealed class Capture : AionMeter.Core.Capture.IEventSource
    {
        public bool Disposed;
        public event Action<AionMeter.Core.Events.GameEvent>? EventDecoded { add { } remove { } }
        public event Action<AionMeter.Core.Capture.CaptureStatus>? StatusChanged { add { } remove { } }
        public AionMeter.Core.Capture.CaptureStatus Status => AionMeter.Core.Capture.CaptureStatus.Stopped;
        public void Start() { }
        public void Stop() { }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Maintenance_stops_sources_and_prevents_queued_restarts_or_demo_replay()
    {
        var source = new Capture(); var starts = 0;
        using var meter = new MeterService(new AppSettings { Transient = true, SaveHistory = false, DownloadIcons = false }, _ => { starts++; return source; });
        meter.StartCapture();
        meter.PauseForMaintenance();
        Assert.True(source.Disposed);
        meter.StartCapture(); meter.RestartCapture(); meter.Restart(); meter.StartDemo();
        meter.Replay("this-file-must-never-be-opened.pcap");
        Assert.Equal(1, starts); Assert.False(meter.DemoRunning);
    }

    [Fact]
    public async Task Maintenance_remains_blocked_on_network_failure_and_only_explicit_release_unblocks()
    {
        var handler = new Handler();
        var service = new AppMaintenanceService(new HttpClient(handler));
        Assert.False(service.CanUseApp);
        handler.Body = "{\"enabled\":true,\"message\":\"Wartung\",\"revision\":1}";
        Assert.True(await service.RefreshAsync()); Assert.False(service.CanUseApp);
        handler.Code = HttpStatusCode.ServiceUnavailable;
        Assert.False(await service.RefreshAsync()); Assert.False(service.CanUseApp);
        handler.Code = HttpStatusCode.OK;
        handler.Body = "{\"enabled\":false,\"message\":\"Bereit\",\"revision\":2}";
        Assert.True(await service.RefreshAsync()); Assert.True(service.CanUseApp);
    }

    [Fact]
    public async Task Short_outage_has_grace_period_but_cannot_bypass_status_forever()
    {
        var clock = new Clock(); var handler = new Handler();
        var service = new AppMaintenanceService(new HttpClient(handler), clock);
        Assert.True(await service.RefreshAsync()); Assert.True(service.CanUseApp);
        handler.Code = HttpStatusCode.BadGateway;
        clock.Now += TimeSpan.FromSeconds(90);
        Assert.False(await service.RefreshAsync()); Assert.True(service.CanUseApp);
        clock.Now += TimeSpan.FromSeconds(31);
        Assert.False(service.CanUseApp);
        handler.Code = HttpStatusCode.OK;
        Assert.True(await service.RefreshAsync()); Assert.True(service.CanUseApp);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"enabled\":false}")]
    [InlineData("{\"enabled\":false,\"message\":\"\",\"revision\":0}")]
    [InlineData("{\"enabled\":false,\"message\":\"OK\",\"revision\":-1}")]
    [InlineData("{\"enabled\":false,\"message\":\"OK\",\"revision\":999999999999999999999999999999}")]
    [InlineData("not json")]
    public async Task Malformed_status_never_releases_a_block(string body)
    {
        var handler = new Handler { Body = body };
        var service = new AppMaintenanceService(new HttpClient(handler));
        service.Observe(new MaintenanceStatus(true, "Wartung", 3));
        Assert.False(await service.RefreshAsync()); Assert.False(service.CanUseApp);
    }

    [Fact]
    public async Task Older_responses_cannot_undo_newer_maintenance()
    {
        var handler = new Handler(); var service = new AppMaintenanceService(new HttpClient(handler));
        service.Observe(new MaintenanceStatus(true, "Wartung", 4));
        Assert.False(await service.RefreshAsync()); Assert.False(service.CanUseApp);
    }
}
