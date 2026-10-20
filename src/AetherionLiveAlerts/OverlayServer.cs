using System.Net;
using System.Text;

namespace AetherionLiveAlerts;

public sealed class OverlayServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<string> _content;
    private CancellationTokenSource? _stop;
    private Task? _loop;
    public bool IsRunning => _listener.IsListening;

    public OverlayServer(Func<string> content) => _content = content;

    public void Start(int port)
    {
        if (IsRunning) return;
        _listener.Prefixes.Add($"http://localhost:{Math.Clamp(port, 1024, 65535)}/");
        _listener.Start();
        _stop = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_stop.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _stop?.Cancel();
        if (IsRunning) _listener.Stop();
        if (_loop is not null) try { await _loop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _listener.Close();
        _stop?.Dispose();
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && IsRunning)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().WaitAsync(token); }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            _ = RespondAsync(context);
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        try
        {
            var html = _content();
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        finally { context.Response.Close(); }
    }
}
