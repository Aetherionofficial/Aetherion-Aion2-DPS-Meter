using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AionMeter.App.Services;

/// <summary>Publishes an opt-in local Rich Presence to the Discord desktop client. No game data is sent to Aetherion.</summary>
public sealed class DiscordPresenceService : IDisposable
{
    // Public Discord application ID supplied by AetherionOfficial.
    private const string ApplicationId = "1559644433648197642";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private string? _lastState;
    private long _lastConnectAttempt;

    public async Task UpdateAsync(bool enabled, string? className, int level, string? faction)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!enabled)
            {
                await ClearAndCloseAsync().ConfigureAwait(false);
                return;
            }

            var details = !string.IsNullOrWhiteSpace(className) && level > 0
                ? $"{className} · Lv. {level}"
                : "AION 2 · DPS Meter active";
            var state = string.IsNullOrWhiteSpace(faction) ? "Faction not set" : faction;
            var key = details + "\n" + state;
            if (key == _lastState && _pipe?.IsConnected == true) return;

            if (!await EnsureConnectedAsync().ConfigureAwait(false)) return;
            var activity = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = Environment.ProcessId,
                    activity = new
                    {
                        details,
                        state,
                        assets = new
                        {
                            large_image = "download",
                            large_text = "Aetherion DPS Meter"
                        }
                    }
                },
                nonce = Guid.NewGuid().ToString("N")
            };
            await WriteFrameAsync(_pipe!, 1, JsonSerializer.SerializeToUtf8Bytes(activity)).ConfigureAwait(false);
            _lastState = key;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or JsonException)
        {
            Log.Info($"Discord Rich Presence is unavailable: {ex.Message}");
            ClosePipe();
        }
        finally { _gate.Release(); }
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_pipe?.IsConnected == true) return true;
        ClosePipe();
        var now = Environment.TickCount64;
        if (now - _lastConnectAttempt < 10_000) return false;
        _lastConnectAttempt = now;

        for (var i = 0; i < 10; i++)
        {
            var candidate = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await candidate.ConnectAsync(150).ConfigureAwait(false);
                await WriteFrameAsync(candidate, 0, JsonSerializer.SerializeToUtf8Bytes(new { v = 1, client_id = ApplicationId })).ConfigureAwait(false);
                var ready = await ReadFrameAsync(candidate).ConfigureAwait(false);
                if (ready.Opcode != 1 || !Encoding.UTF8.GetString(ready.Payload).Contains("READY", StringComparison.Ordinal))
                    throw new IOException("Discord IPC handshake was not ready.");
                _pipe = candidate;
                _lastState = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or ObjectDisposedException)
            {
                candidate.Dispose();
            }
        }
        return false;
    }

    private async Task ClearAndCloseAsync()
    {
        if (_pipe?.IsConnected == true)
        {
            try
            {
                var clear = new { cmd = "SET_ACTIVITY", args = new { pid = Environment.ProcessId, activity = (object?)null }, nonce = Guid.NewGuid().ToString("N") };
                await WriteFrameAsync(_pipe, 1, JsonSerializer.SerializeToUtf8Bytes(clear)).ConfigureAwait(false);
            }
            catch (IOException) { }
        }
        ClosePipe();
    }

    private void ClosePipe()
    {
        _pipe?.Dispose();
        _pipe = null;
        _lastState = null;
    }

    private static async Task WriteFrameAsync(Stream stream, int opcode, byte[] payload)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), payload.Length);
        await stream.WriteAsync(header).ConfigureAwait(false);
        await stream.WriteAsync(payload).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<(int Opcode, byte[] Payload)> ReadFrameAsync(Stream stream)
    {
        var header = new byte[8];
        await ReadExactlyAsync(stream, header).ConfigureAwait(false);
        var opcode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (length is < 0 or > 1_048_576) throw new IOException("Invalid Discord IPC frame size.");
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload).ConfigureAwait(false);
        return (opcode, payload);
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset)).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    public void Dispose()
    {
        ClosePipe(); // Closing the IPC session clears the activity in Discord.
    }
}
