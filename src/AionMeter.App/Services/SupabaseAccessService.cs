using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace AionMeter.App.Services;

public sealed record AccountLicenseStatus(bool Authenticated, DateTimeOffset? ExpiresAt, bool IsPartner, bool IsRevoked, string LicenseType)
{
    public bool HasActiveLicense => Authenticated && LicenseType != "none" && !IsRevoked && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);
}

public sealed class AccountAccessBlockedException(string message) : InvalidOperationException(message);

/// <summary>Discord login and one-time Supabase license redemption.</summary>
public static class SupabaseAccessService
{
    public const string ProjectUrl = "https://aafdjzdirqpanuueveng.supabase.co";
    // Publishable keys are intended for clients. Database writes are restricted to the authenticated RPC.
    private const string PublishableKey = "sb_publishable_68xFqusKPvOo7wHoClrH5A_K2DuWBax";
    public const string RedirectUri = "http://127.0.0.1:43781/auth/callback/";
    private const string SessionPathName = "account-session.bin";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly string SessionPath = Path.Combine(AppSettings.AppDataDir, SessionPathName);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string? _accessToken;
    public static AccountLicenseStatus? CurrentStatus { get; private set; }

    public static async Task<AccountLicenseStatus> SignInWithDiscordAsync(CancellationToken cancellationToken = default)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        var authorize = new Uri($"{ProjectUrl}/auth/v1/authorize?provider=discord" +
            $"&redirect_to={Uri.EscapeDataString(RedirectUri)}&code_challenge={challenge}&code_challenge_method=s256");
        Process.Start(new ProcessStartInfo(authorize.ToString()) { UseShellExecute = true });

        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
        var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);
        var code = query["code"];
        var oauthError = query["error_description"] ?? query["error"];
        var responseText = code is null
            ? "<h2>Aetherion-Anmeldung fehlgeschlagen</h2><p>Du kannst dieses Browserfenster schließen und es erneut versuchen.</p>"
            : "<h2>Aetherion-Anmeldung abgeschlossen</h2><p>Du kannst dieses Browserfenster schließen und zur App zurückkehren.</p>";
        var bytes = Encoding.UTF8.GetBytes($"<!doctype html><meta charset='utf-8'><title>Aetherion</title><body style='font:16px Segoe UI;background:#12151e;color:#fff;padding:3em'>{responseText}</body>");
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.Close();

        if (code is null) throw new InvalidOperationException(oauthError ?? "Discord hat keinen Anmeldecode zurückgegeben.");
        var session = await ExchangeCodeAsync(code, verifier, cancellationToken);
        PersistRefreshToken(session.RefreshToken);
        _accessToken = session.AccessToken;
        return await GetLicenseStatusAsync(session.AccessToken, cancellationToken);
    }

    public static async Task<AccountLicenseStatus?> CheckSavedSessionAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = ReadRefreshToken();
        if (refreshToken is null) return null;
        // Let temporary network / storage errors reach the caller. A null result means there is no saved
        // session; it must not be confused with a failed request and treated as an expired entitlement.
        var session = await RefreshSessionAsync(refreshToken, cancellationToken);
        PersistRefreshToken(session.RefreshToken);
        _accessToken = session.AccessToken;
        return await GetLicenseStatusAsync(session.AccessToken, cancellationToken);
    }

    public static async Task<AccountLicenseStatus> RedeemLicenseKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Bitte gib deinen Lizenz-Key ein.");
        var refreshToken = ReadRefreshToken() ?? throw new InvalidOperationException("Melde dich zuerst mit Discord an.");
        var session = await RefreshSessionAsync(refreshToken, cancellationToken);
        PersistRefreshToken(session.RefreshToken);
        _accessToken = session.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/rest/v1/rpc/redeem_aetherion_license_key")
        {
            Content = JsonContent.Create(new { p_license_key = key.Trim() }, options: JsonOptions),
        };
        request.Headers.Add("apikey", PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(ExplainRedemptionError(body));
        return await GetLicenseStatusAsync(session.AccessToken, cancellationToken);
    }

    public static void SignOut()
    {
        _accessToken = null;
        CurrentStatus = null;
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static string FormatRemaining(DateTimeOffset? expiry)
    {
        if (expiry is null) return "Kein aktiver Lizenz-Key";
        var remaining = expiry.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return $"Abgelaufen am {expiry.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
        var days = (int)Math.Ceiling(remaining.TotalDays);
        return $"Noch {days} {(days == 1 ? "Tag" : "Tage")} Zugriff · gültig bis {expiry.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
    }

    public static string FormatAccountLicense(AccountLicenseStatus? status)
    {
        if (status is null) return "Discord-Konto nicht angemeldet.";
        if (status.IsRevoked) return "Zugang gesperrt. Bitte eröffne ein Ticket im Discord.";
        if (!status.HasActiveLicense) return FormatRemaining(status.ExpiresAt);
        if (status.IsPartner) return "Partner-Zugang · Lifetime";
        if (status.ExpiresAt is null) return "Lifetime-Zugang";
        return FormatRemaining(status.ExpiresAt);
    }

    public static string FormatOverlayLicense(AccountLicenseStatus? status)
    {
        if (status is null || !status.Authenticated || !status.HasActiveLicense) return string.Empty;
        if (status.IsPartner) return "✦ PARTNER";
        if (status.ExpiresAt is null) return "LIFETIME";
        var left = status.ExpiresAt.Value - DateTimeOffset.UtcNow;
        if (left <= TimeSpan.Zero) return "ABGELAUFEN";
        if (left.TotalDays >= 1) return $"{Math.Ceiling(left.TotalDays):0} TAGE";
        return $"{Math.Max(1, (int)Math.Ceiling(left.TotalHours)):0} STD";
    }

    private static async Task<AuthTokenResponse> ExchangeCodeAsync(string code, string verifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/auth/v1/token?grant_type=pkce")
        {
            Content = JsonContent.Create(new { auth_code = code, code_verifier = verifier }, options: JsonOptions),
        };
        request.Headers.Add("apikey", PublishableKey);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(ReadError(body));
        return JsonSerializer.Deserialize<AuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Supabase hat eine leere Sitzung zurückgegeben.");
    }

    private static async Task<AuthTokenResponse> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/auth/v1/token?grant_type=refresh_token")
        {
            Content = JsonContent.Create(new { refresh_token = refreshToken }, options: JsonOptions),
        };
        request.Headers.Add("apikey", PublishableKey);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is 400 or 401) SignOut();
            throw new InvalidOperationException(ReadError(body));
        }
        return JsonSerializer.Deserialize<AuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Supabase hat eine leere Sitzung zurückgegeben.");
    }

    private static async Task<AccountLicenseStatus> GetLicenseStatusAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{ProjectUrl}/rest/v1/aetherion_entitlements?select=expires_at,is_partner,is_revoked,license_type&limit=1");
        request.Headers.Add("apikey", PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(ReadError(body));
        var rows = JsonSerializer.Deserialize<List<EntitlementRow>>(body, JsonOptions) ?? [];
        var row = rows.FirstOrDefault();
        var status = new AccountLicenseStatus(true, row?.ExpiresAt, row?.IsPartner ?? false,
            row?.IsRevoked ?? false, row?.LicenseType ?? "none");
        if (status.HasActiveLicense)
            await CheckAccountInstallationAsync(accessToken, cancellationToken);
        return CurrentStatus = status;
    }

    private static async Task CheckAccountInstallationAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/functions/v1/aetherion-license-check")
        {
            Content = JsonContent.Create(new { installationId = LicenseService.InstallationId }, options: JsonOptions),
        };
        request.Headers.Add("apikey", PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden)
            throw new AccountAccessBlockedException("ACCOUNT_INSTALLATION_BLOCKED");
        throw new InvalidOperationException(ReadError(body));
    }

    private static string? ReadRefreshToken()
    {
        try
        {
            return File.Exists(SessionPath)
                ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(SessionPath), null, DataProtectionScope.CurrentUser))
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            SignOut();
            return null;
        }
    }

    private static void PersistRefreshToken(string token)
    {
        Directory.CreateDirectory(AppSettings.AppDataDir);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SessionPath, protectedBytes);
    }

    private static string ExplainRedemptionError(string body)
    {
        var message = ReadError(body);
        if (message.Contains("KEY_INVALID_OR_REDEEMED", StringComparison.OrdinalIgnoreCase))
            return "Dieser Key ist ungültig oder wurde bereits eingelöst.";
        if (message.Contains("AUTH_REQUIRED", StringComparison.OrdinalIgnoreCase))
            return "Deine Discord-Anmeldung ist abgelaufen. Melde dich erneut an.";
        if (message.Contains("PERMANENT_LICENSE_ALREADY_ACTIVE", StringComparison.OrdinalIgnoreCase))
            return "Dein Konto hat bereits Lifetime- oder Partner-Zugang. Dafür kann kein weiterer Key eingelöst werden.";
        return message;
    }

    private static string ReadError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("msg", out var msg) ? msg.GetString() ?? body
                : json.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? body
                : body;
        }
        catch (JsonException) { return body; }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record AuthTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);
    private sealed record EntitlementRow(
        [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
        [property: JsonPropertyName("is_partner")] bool IsPartner,
        [property: JsonPropertyName("is_revoked")] bool IsRevoked,
        [property: JsonPropertyName("license_type")] string LicenseType);
}

