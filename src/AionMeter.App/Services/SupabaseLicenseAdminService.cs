using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AionMeter.App.Services;

internal sealed record LicenseAdminEntry(
    string KeyHash,
    string Fingerprint,
    string LicenseType,
    int? DurationDays,
    DateTimeOffset IssuedAt,
    DateTimeOffset? RedeemedAt,
    string? AccountName,
    string? AccountId,
    DateTimeOffset? ExpiresAt,
    string Status,
    string? InstallationState,
    string? InstallationFingerprint)
{
    public string InstallationLabel => InstallationState == "active"
        ? $"Aktiv · {InstallationFingerprint ?? "verbunden"}"
        : "Nicht gebunden";
    public string PlanLabel => LicenseType switch
    {
        "trial_1d" => "Test · 1 Tag",
        "week_1w" => "7 Tage",
        "days_30" => "30 Tage",
        "days_90" => "90 Tage",
        "days_365" => "1 Jahr",
        "lifetime" => "Lifetime",
        "partner" => "Partner",
        _ => LicenseType,
    };

    public string StatusLabel => Status switch
    {
        "available" => "Unbenutzt",
        "active" => "Aktiv",
        "expired" => "Abgelaufen",
        "revoked" => "Gesperrt",
        _ => Status,
    };
}

internal sealed record IssuedAdminLicense(string Key, string Fingerprint, string LicenseType, int? DurationDays, DateTimeOffset IssuedAt);
internal sealed record LicenseAdminHistoryEntry(DateTimeOffset CreatedAt, string Action, string Fingerprint, string? AccountName, string? Details);

internal sealed class SupabaseLicenseAdminService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Endpoint = "/functions/v1/aetherion-license-admin";
    private readonly string _accessToken;

    public SupabaseLicenseAdminService(string accessToken) => _accessToken = accessToken;

    public async Task<(IReadOnlyList<LicenseAdminEntry> Keys, bool Truncated)> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, null, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<KeyListResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Das Lizenz-Dashboard hat keine Daten zurückgegeben.");
        return (result.Keys ?? [], result.Truncated);
    }

    public async Task<IReadOnlyList<LicenseAdminHistoryEntry>> HistoryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, null, cancellationToken, "?view=history");
        var result = await response.Content.ReadFromJsonAsync<HistoryResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Das Verlaufsprotokoll hat keine Daten zurückgegeben.");
        return result.Events ?? [];
    }

    public async Task<IReadOnlyList<IssuedAdminLicense>> CreateAsync(string licenseType, int count, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, new { action = "create", licenseType, count }, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IssuedListResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Die erzeugten Keys konnten nicht geladen werden.");
        return result.Issued ?? [];
    }

    public async Task RevokeAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, new { action = "revoke", keyHash }, cancellationToken);
    }

    public async Task RestoreAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, new { action = "restore", keyHash }, cancellationToken);
    }

    public async Task ResetDeviceAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, new { action = "reset_device", keyHash }, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, object? body, CancellationToken cancellationToken, string query = "")
    {
        using var request = new HttpRequestMessage(method, SupabaseAccessService.ProjectUrl + Endpoint + query);
        request.Headers.Add("apikey", SupabaseAccessService.PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await Http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return response;

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Dispose();
        throw new InvalidOperationException(ExplainError(text));
    }

    private static string ExplainError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var error = json.RootElement.TryGetProperty("error", out var value) ? value.GetString() : null;
            return error switch
            {
                "owner_access_required" => "Dieses Discord-Konto ist nicht als Lizenz-Administrator freigeschaltet.",
                "license_admin_not_configured" => "Die Admin-Discord-User-ID ist in Supabase noch nicht eingetragen.",
                "admin_backend_not_configured" => "Die Supabase-Adminfunktion ist noch nicht vollständig konfiguriert.",
                "auth_required" => "Die Discord-Sitzung ist abgelaufen. Starte das Dashboard erneut und melde dich wieder an.",
                "key_not_found_or_already_revoked" => "Dieser Key wurde bereits gesperrt oder existiert nicht mehr.",
                "key_not_found_or_not_revoked" => "Dieser Key existiert nicht oder ist bereits freigegeben.",
                "key_not_found_or_device_not_bound" => "Für diesen Key ist keine aktive Gerätebindung vorhanden.",
                "license_type_invalid" => "Dieser Key-Typ wird von der Datenbank nicht unterstützt.",
                _ => "Die Lizenzverwaltung antwortet mit einem Fehler. Prüfe Edge-Function- und Datenbankmigration in Supabase.",
            };
        }
        catch (JsonException) { return "Die Lizenzverwaltung konnte nicht erreicht werden. Prüfe deine Verbindung und die Supabase-Konfiguration."; }
    }

    private sealed record KeyListResponse(List<LicenseAdminEntry>? Keys, bool Truncated);
    private sealed record IssuedListResponse(List<IssuedAdminLicense>? Issued);
    private sealed record HistoryResponse(List<LicenseAdminHistoryEntry>? Events);
}
