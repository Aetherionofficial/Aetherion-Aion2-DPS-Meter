using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AetherionLiveAlerts;

public sealed class WebhookReceiver : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _secret;
    private CancellationTokenSource? _stop;
    private Task? _loop;
    public event EventHandler<LiveEvent>? EventReceived;
    public bool IsRunning => _listener.IsListening;

    public WebhookReceiver(string secret) => _secret = secret;

    public void Start(string prefix)
    {
        if (IsRunning) return;
        if (!prefix.EndsWith('/')) prefix += "/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _stop = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_stop.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _stop?.Cancel();
        if (IsRunning) _listener.Stop();
        if (_loop is not null) try { await _loop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _listener.Close(); _stop?.Dispose();
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && IsRunning)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().WaitAsync(token); }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) { context.Response.StatusCode = 405; return; }
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (!Verify(context.Request.Headers["TikTok-Signature"], body)) { context.Response.StatusCode = 401; return; }
            if (Parse(body) is { } liveEvent) EventReceived?.Invoke(this, liveEvent);
            context.Response.StatusCode = 202;
        }
        catch (JsonException) { context.Response.StatusCode = 400; }
        finally { try { context.Response.Close(); } catch (InvalidOperationException) { } }
    }

    private bool Verify(string? header, string body)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        var values = header.Split(',').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);
        if (!values.TryGetValue("t", out var timestamp) || !values.TryGetValue("s", out var signature) || !long.TryParse(timestamp, out var seconds)) return false;
        if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + body))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    private static LiveEvent? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var name = Text(root, "event")?.ToLowerInvariant();
        var type = name switch
        {
            "live.gift" or "gift" => AlertEventType.Gift,
            "live.follow" or "follow" => AlertEventType.Follow,
            "live.like" or "like" => AlertEventType.Like,
            "live.share" or "share" => AlertEventType.Share,
            "live.subscribe" or "subscribe" => AlertEventType.Subscribe,
            "live.comment" or "comment" or "chat" => AlertEventType.Chat,
            _ => (AlertEventType?)null,
        };
        if (type is null) return null;
        var details = root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? JsonDocument.Parse(c.GetString() ?? "{}").RootElement : root;
        var user = Text(details, "user_name", "nickname", "uniqueId") ?? "Zuschauer";
        var message = Text(details, "message", "text") ?? "";
        var gift = Text(details, "gift_name", "gift") ?? "";
        var quantity = Int(details, "quantity", "count") ?? 1;
        var id = Text(root, "event_id", "id") ?? Guid.NewGuid().ToString("N");
        return new LiveEvent(type.Value, user, message, gift, Math.Max(1, quantity), id);
    }

    private static string? Text(JsonElement value, params string[] names) => names.Select(name => value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    private static int? Int(JsonElement value, params string[] names) => names.Select(name => value.TryGetProperty(name, out var p) && p.TryGetInt32(out var n) ? n : (int?)null).FirstOrDefault(v => v is not null);
}
