using System.Media;
using System.IO;

namespace AionMeter.App.Services;

/// <summary>A small branded two-note cue, independent of the Windows notification sound.</summary>
internal static class AlertChime
{
    private static readonly Dictionary<string, SoundPlayer> Players = new(StringComparer.OrdinalIgnoreCase);

    public static void Play(int volume, string style)
    {
        var key = $"{style}:{volume}";
        if (!Players.TryGetValue(key, out var player))
        {
            player = CreatePlayer(Math.Clamp(volume, 0, 100), style);
            Players[key] = player;
        }
        player.Play();
    }

    private static SoundPlayer CreatePlayer(int volume, string style)
    {
        const int sampleRate = 44100;
        var samples = new List<short>();
        if (style.Equals("urgent", StringComparison.OrdinalIgnoreCase))
        {
            AddTone(samples, 880, 100, sampleRate, volume);
            AddTone(samples, 660, 100, sampleRate, volume);
            AddTone(samples, 880, 160, sampleRate, volume);
        }
        else
        {
            AddTone(samples, 660, 115, sampleRate, volume);
            AddTone(samples, 880, 185, sampleRate, volume);
        }
        using var stream = new MemoryStream(44 + samples.Count * 2);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Count * 2);
            foreach (var sample in samples) writer.Write(sample);
        }
        stream.Position = 0;
        var player = new SoundPlayer(stream);
        player.Load();
        return player;
    }

    private static void AddTone(List<short> output, double frequency, int milliseconds, int sampleRate, int volume)
    {
        var count = milliseconds * sampleRate / 1000;
        for (var i = 0; i < count; i++)
        {
            var envelope = Math.Min(1, i / 500d) * Math.Min(1, (count - i) / 1800d);
            output.Add((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * 6500 * envelope * volume / 100d));
        }
        output.AddRange(new short[sampleRate / 1000 * 25]);
    }
}
