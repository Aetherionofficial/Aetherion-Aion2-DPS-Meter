using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace AionMeter.App.Services;

public sealed record MeterLicense(string Product, string Machine, DateTimeOffset? ExpiresUtc, string Id,
    string? PartnerName = null, string? ReferralCode = null, int ReferralDiscountPercent = 0);
public sealed record RevocationList(string Product, DateTimeOffset UpdatedUtc, string[] RevokedIds);

/// <summary>Offline license verification. The app contains only the public signing key.</summary>
public static class LicenseService
{
    private const string PublicKey = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAzfXL/m+S2eG1OOnI9bO9
+ox+eQJGyarV7aPVlo1yjug2anK3aSxSPwJHf9QH6lt8SRImRWvjmn5+uLhf+T6J
bmKyVZWrIwpw/oXtPjmVGh7S0U5gtJhtBHmDGUOfRLPe71yXUrB3ebxV5+n5AWuZ
dcYDHAt0xBZM9bpEu/hryUtD1he4kZCHIg5DhV17y6rpNKbxN70Jff37XApt/PSZ
GfMNlXjybFphWWfMQ2QBCV2RmKflCOpXjMjeXT2v+BdmH0n6wPPOXkmV4pCC3478
mDKMSFWrmtM+4vqzUX0O5mYlu50ZnTTlYqYzzqRsHLj/aF8o5QX3Bb0CCGlFLgjP
wcjLoqdi65jGC3xR3YQ7Y2H3KBr1WHlnJat9WSiKSc+C2tL+bOvCxsOVgagWPg45
RiJHnWZ0sbX1+H2g+PkYjTA1uxNaLT5ieL7hZ7YGNCmcj4gAc0/AZHwxyJKcH9fL
IQUfKoPokQOHnXTUAE/RQ2bJnNsz1/0qxNjOegr1QyIxAgMBAAE=
-----END PUBLIC KEY-----
""";
    private static readonly string LicensePath = Path.Combine(AppSettings.AppDataDir, "license.key");
    private static readonly string RevocationCachePath = Path.Combine(AppSettings.AppDataDir, "revocation-feed.txt");
    // Signed revocation feed hosted in the owner's GitHub repository.
    public const string RevocationFeedUrl = "https://raw.githubusercontent.com/Aetherion/MostWanted-DPS/main/revoked-licenses.txt";

    public static string InstallationId
    {
        get
        {
            var machineGuid = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
            if (string.IsNullOrWhiteSpace(machineGuid)) machineGuid = Environment.MachineName;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Aetherion:" + machineGuid)))[..24];
        }
    }

    public static MeterLicense? LoadValid()
    {
        try
        {
            if (!File.Exists(LicensePath)) return null;
            return Validate(File.ReadAllText(LicensePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public static async Task RefreshRevocationsAsync()
    {
        if (string.IsNullOrWhiteSpace(RevocationFeedUrl)) return;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var signedFeed = await client.GetStringAsync(RevocationFeedUrl);
            if (!TryReadSignedFeed(signedFeed, out var list) || list is null || list.Product != "Aetherion") return;
            var cached = LoadRevocations();
            if (cached is not null && cached.UpdatedUtc > list.UpdatedUtc) return;
            Directory.CreateDirectory(AppSettings.AppDataDir);
            File.WriteAllText(RevocationCachePath, signedFeed.Trim());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            // Use the last verified cache when the owner feed is temporarily unavailable.
        }
    }

    public static MeterLicense? Validate(string text)
    {
        try
        {
            var parts = text.Trim().Split('.');
            if (parts.Length != 2) return null;
            var payload = FromBase64Url(parts[0]);
            var signature = FromBase64Url(parts[1]);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKey);
            if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) return null;
            var license = JsonSerializer.Deserialize<MeterLicense>(payload);
            if (license is not { Product: "Aetherion", Machine: var machine } || machine != InstallationId) return null;
            if (license.ExpiresUtc is { } expires && expires <= DateTimeOffset.UtcNow) return null;
            if (license.ExpiresUtc is null && string.IsNullOrWhiteSpace(license.PartnerName)) return null;
            if (IsRevoked(license.Id)) return null;
            return license;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or ArgumentException)
        { return null; }
    }

    public static MeterLicense? Activate(string text)
    {
        var valid = Validate(text);
        if (valid is null) return null;
        Directory.CreateDirectory(AppSettings.AppDataDir);
        File.WriteAllText(LicensePath, text.Trim());
        return valid;
    }

    public static RevocationList? LoadRevocations()
    {
        try
        {
            if (!File.Exists(RevocationCachePath)) return null;
            return TryReadSignedFeed(File.ReadAllText(RevocationCachePath), out var list) ? list : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public static string SignRevocations(RevocationList list, string privateKeyPath)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(list);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
        return Base64Url(payload) + "." + Base64Url(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static bool TryReadSignedFeed(string text, out RevocationList? list)
    {
        list = null;
        try
        {
            var parts = text.Trim().Split('.');
            if (parts.Length != 2) return false;
            var payload = FromBase64Url(parts[0]);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKey);
            if (!rsa.VerifyData(payload, FromBase64Url(parts[1]), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) return false;
            list = JsonSerializer.Deserialize<RevocationList>(payload);
            return list is { Product: "Aetherion", RevokedIds: not null };
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or ArgumentException)
        { return false; }
    }

    private static bool IsRevoked(string id) => LoadRevocations()?.RevokedIds.Contains(id, StringComparer.OrdinalIgnoreCase) == true;

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
}
