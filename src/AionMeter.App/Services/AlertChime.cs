using System.Media;
using System.IO;

namespace AionMeter.App.Services;

/// <summary>A small branded two-note cue, independent of the Windows notification sound.</summary>
internal static class AlertChime
{
    private static readonly SoundPlayer Player = CreatePlayer();

    public static void Play() => Player.Play();

    private static SoundPlayer CreatePlayer()
    {
        const int sampleRate = 44100;
        var samples = new List<short>();
        AddTone(samples, 660, 115, sampleRate);
        AddTone(samples, 880, 185, sampleRate);
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

    private static void AddTone(List<short> output, double frequency, int milliseconds, int sampleRate)
    {
        var count = milliseconds * sampleRate / 1000;
        for (var i = 0; i < count; i++)
        {
            var envelope = Math.Min(1, i / 500d) * Math.Min(1, (count - i) / 1800d);
            output.Add((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * 6500 * envelope));
        }
        output.AddRange(new short[sampleRate / 1000 * 25]);
    }
}
