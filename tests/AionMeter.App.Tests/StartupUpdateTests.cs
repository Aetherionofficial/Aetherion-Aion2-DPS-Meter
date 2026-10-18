using System.Net;
using System.Net.Http;
using AionMeter.App.Services;
using AionMeter.Core.Updates;

namespace AionMeter.App.Tests;

public sealed class StartupUpdateTests
{
    private sealed class FeedHandler : HttpMessageHandler
    {
        public int Requests;
        public HttpStatusCode Code = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent("""
                {"tag_name":"v99.0.0","html_url":"https://github.com/Aetherionofficial/Aetherion-Aion2-DPS-Meter/releases/tag/v99.0.0","body":"Test update","draft":false,"prerelease":false,"assets":[]}
                """) });
        }
    }

    [Fact]
    public async Task Startup_can_check_before_meter_login_and_background_timer_without_downloading()
    {
        var handler = new FeedHandler();
        using var feed = new UpdateFeed("Startup-Test", handler);
        using var updater = new Updater(new AppSettings { Transient = true, CheckUpdates = false }, feed, allowAutomaticDownloads: false);
        Assert.Equal(UpdateCheckResult.Available, await updater.CheckAsync());
        Assert.Equal(new Version(99, 0, 0), updater.Available!.Version);
        Assert.Equal(1, handler.Requests);
        Assert.Null(updater.Downloaded);
    }

    [Fact]
    public async Task Skipped_version_is_not_prompted_at_start_but_remains_visible_to_manual_check()
    {
        using var feed = new UpdateFeed("Startup-Test", new FeedHandler());
        using var updater = new Updater(new AppSettings { Transient = true, SkippedUpdate = "99.0.0" }, feed, allowAutomaticDownloads: false);
        Assert.Equal(UpdateCheckResult.Available, await updater.CheckAsync());
        Assert.Null(updater.Available);
        Assert.Equal(new Version(99, 0, 0), updater.Latest!.Version);
    }

    [Fact]
    public async Task Failed_update_check_does_not_throw_or_report_a_new_version()
    {
        using var feed = new UpdateFeed("Startup-Test", new FeedHandler { Code = HttpStatusCode.ServiceUnavailable });
        using var updater = new Updater(new AppSettings { Transient = true }, feed, allowAutomaticDownloads: false);
        Assert.Equal(UpdateCheckResult.Failed, await updater.CheckAsync());
        Assert.Null(updater.Available);
    }
}
