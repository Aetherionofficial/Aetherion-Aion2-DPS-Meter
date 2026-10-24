using System.Text.Json;
using System.Threading.Channels;

namespace AionMeter.Core.Protocol;

/// <summary>Decoded frame body, not a complete Ethernet/TCP packet. IDs inside unknown bodies have no confirmed meaning.</summary>
public sealed record UndecodedPacket(long TimeMs, string Opcode, int Length, string Hex, int? MapId, string? Zone, string Reason);

/// <summary>Local JSONL diagnostics. Live callers never wait for disk; offline tools may opt into backpressure.</summary>
public sealed class UndecodedPacketLog : IDisposable
{
    private readonly Channel<UndecodedPacket> _queue = Channel.CreateBounded<UndecodedPacket>(
        new BoundedChannelOptions(128) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly bool _waitForCapacity;
    private readonly Task _writer;
    private long _dropped;
    private Exception? _error;
    public long Dropped => Interlocked.Read(ref _dropped);
    public Exception? Error => Volatile.Read(ref _error);

    public UndecodedPacketLog(string path, bool waitForCapacity = false)
    {
        var fullPath = Path.GetFullPath(path);
        _waitForCapacity = waitForCapacity;
        _writer = Task.Run(async () =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                using var stream = new StreamWriter(fullPath, append: true) { AutoFlush = true };
                await foreach (var packet in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
                    await stream.WriteLineAsync(JsonSerializer.Serialize(packet)).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Volatile.Write(ref _error, error);
                _queue.Writer.TryComplete();
                while (_queue.Reader.TryRead(out _)) Interlocked.Increment(ref _dropped);
            }
        });
    }

    public void Write(UndecodedPacket packet)
    {
        if (!_waitForCapacity)
        {
            if (!_queue.Writer.TryWrite(packet)) Interlocked.Increment(ref _dropped);
            return;
        }
        try { _queue.Writer.WriteAsync(packet).AsTask().GetAwaiter().GetResult(); }
        catch (ChannelClosedException) { Interlocked.Increment(ref _dropped); }
    }

    public void Dispose() { _queue.Writer.TryComplete(); _writer.GetAwaiter().GetResult(); }
}
