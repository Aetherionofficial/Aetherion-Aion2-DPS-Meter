using System.Runtime.InteropServices;

namespace AionMeter.App.Services;

internal static class VoiceAnnouncer
{
    public static void Speak(string text)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (type is null) return;
                dynamic voice = Activator.CreateInstance(type)!;
                voice.Speak(text, 1); // SVSFlagsAsync: keep the UI responsive.
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                Log.Info("Voice announcement unavailable: " + ex.Message);
            }
        });
    }
}
