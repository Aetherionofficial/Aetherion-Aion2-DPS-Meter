using System.Net.Http;
using System.Text.Json;

namespace AionMeter.App.Services;

public sealed record MaintenanceStatus(bool Enabled, string Message, long Revision)
{
    public static MaintenanceStatus Parse(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var enabled = root.GetProperty("enabled").GetBoolean();
        var message = root.GetProperty("message").GetString();
        var revision = root.GetProperty("revision").GetInt64();
        if (string.IsNullOrWhiteSpace(message) || message.Length > 500 || revision < 0)
            throw new JsonException("Ungültiger Wartungsstatus.");
        return new(enabled, message, revision);
    }
}

public sealed class AppMaintenanceException(MaintenanceStatus status) : InvalidOperationException(status.Message)
{
    public MaintenanceStatus Status { get; } = status;
}

public sealed class AppMaintenanceService(HttpClient http, TimeProvider? clock = null)
{
    public static AppMaintenanceService Shared { get; } = new(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private DateTimeOffset? _lastSuccess;
    public MaintenanceStatus? Status { get; private set; }
    public bool CanUseApp => Status is { Enabled: false } && _lastSuccess is { } time && _clock.GetUtcNow() - time < TimeSpan.FromMinutes(2);

    public void Observe(MaintenanceStatus status)
    {
        if (Status is not null && status.Revision < Status.Revision) throw new InvalidOperationException("Veralteter Wartungsstatus.");
        Status = status;
        _lastSuccess = _clock.GetUtcNow();
    }

    public async Task<bool> RefreshAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SupabaseAccessService.ProjectUrl + "/functions/v1/aetherion-app-status");
            request.Headers.Add("apikey", SupabaseAccessService.PublishableKey);
            request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Observe(MaintenanceStatus.Parse(await response.Content.ReadAsStringAsync()));
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }
}
