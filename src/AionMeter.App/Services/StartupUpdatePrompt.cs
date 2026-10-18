using System.Windows;
using AionMeter.App.Windows;

namespace AionMeter.App.Services;

/// <summary>Update access is available before login and independently of maintenance.</summary>
public static class StartupUpdatePrompt
{
    public static Version? LastPromptedVersion { get; private set; }

    public static async Task<bool> ShowAsync(Window? owner = null, bool manual = false)
    {
        var settings = AppSettings.Load();
        UiText.Use(settings.Language);
        using var updater = new Updater(settings, allowAutomaticDownloads: false);
        var result = await updater.CheckAsync();
        if (owner is not null && !owner.IsVisible) return true;
        var release = manual ? updater.Latest : updater.Available;
        if (result == UpdateCheckResult.Available && release is not null)
        {
            LastPromptedVersion = release.Version;
            var window = new UpdateWindow(updater, release, Updater.IsInstalled);
            if (owner is not null) window.Owner = owner;
            window.ShowDialog();
            return !window.UpdateStarted;
        }
        if (manual)
            MessageBox.Show(owner!, result == UpdateCheckResult.Failed
                ? "Die Update-Prüfung ist fehlgeschlagen. Bitte versuche es später erneut."
                : "Du verwendest bereits die aktuelle App-Version.", "Aetherion · Updates", MessageBoxButton.OK,
                result == UpdateCheckResult.Failed ? MessageBoxImage.Warning : MessageBoxImage.Information);
        return true;
    }
}
