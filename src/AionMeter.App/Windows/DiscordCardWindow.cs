using System.IO;
using System.Windows;
using System.Windows.Controls;
using AionMeter.App.Services;
using AionMeter.Core.Combat;
using AionMeter.Core.Storage;
using Microsoft.Win32;

namespace AionMeter.App.Windows;

public sealed class DiscordCardWindow : Window
{
    private readonly FightRecord _record;
    private readonly string? _boss;
    private readonly Image _preview = new() { Stretch = System.Windows.Media.Stretch.Uniform, MaxWidth = 1080 };
    private readonly CheckBox _anonymous = new() { Content = "Spielernamen anonymisieren", Foreground = System.Windows.Media.Brushes.WhiteSmoke, IsChecked = true, Margin = new Thickness(0, 8, 18, 8) };
    private readonly ComboBox _page = new() { Width = 120, Margin = new Thickness(0, 4, 16, 4) };
    private readonly TextBlock _status = new() { Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap };
    private IReadOnlyList<DiscordCardPage> _pages = [];
    public DiscordCardWindow(FightRecord record, string? bossName = null)
    {
        _record = record; _boss = bossName;
        Title = "Aetherion · Ergebniskarte für Discord"; Width = 850; Height = 820; MinWidth = 560; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("Bg0"); Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("Text");
        var root = new DockPanel { Margin = new Thickness(16) }; Content = root;
        var bar = new WrapPanel(); DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        _anonymous.IsEnabled = !PrivacyNames.Enabled;
        _anonymous.ToolTip = "Streamer-Modus erzwingt Anonymisierung. Notizen und Serverdaten werden nie exportiert.";
        _anonymous.Checked += (_, _) => Refresh(); _anonymous.Unchecked += (_, _) => Refresh();
        bar.Children.Add(_anonymous); bar.Children.Add(_page);
        var save = new Button { Content = "PNG speichern…", Style = (Style)Application.Current.FindResource("PrimaryButton"), Padding = new Thickness(15, 8, 15, 8) };
        save.Click += (_, _) => Export(); bar.Children.Add(save);
        _page.SelectionChanged += (_, _) => RenderPage();
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(new ScrollViewer { Content = _preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        PrivacyNames.Changed += PrivacyChanged;
        Closed += (_, _) => PrivacyNames.Changed -= PrivacyChanged;
        Refresh();
    }
    private void PrivacyChanged()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(PrivacyChanged); return; }
        if (PrivacyNames.Enabled) _anonymous.IsChecked = true;
        _anonymous.IsEnabled = !PrivacyNames.Enabled; Refresh();
    }
    private void Refresh()
    {
        _pages = DiscordCardData.Create(_record, _anonymous.IsChecked == true || PrivacyNames.Enabled, _boss);
        _page.ItemsSource = Enumerable.Range(1, _pages.Count).Select(i => $"Seite {i}").ToArray(); _page.SelectedIndex = 0;
        _status.Text = $"{_pages.Count} PNG-Datei(en) · Vorschau entspricht dem Export. Bei großen Gruppen werden alle Spieler auf mehrere Karten verteilt. Es wird nichts automatisch an Discord oder den Spielchat gesendet.";
        RenderPage();
    }
    private void RenderPage()
    { if (_page.SelectedIndex >= 0 && _page.SelectedIndex < _pages.Count) _preview.Source = DiscordCardRenderer.Render(_pages[_page.SelectedIndex]); }
    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "PNG-Bild|*.png", FileName = "Aetherion-Ergebnis.png", AddExtension = true, OverwritePrompt = _pages.Count == 1 };
        if (dialog.ShowDialog(this) != true) return;
        var directory = Path.GetDirectoryName(dialog.FileName)!; var stem = Path.GetFileNameWithoutExtension(dialog.FileName);
        var paths = Enumerable.Range(0, _pages.Count).Select(i => _pages.Count == 1 ? dialog.FileName : Path.Combine(directory, $"{stem}-{i + 1:00}.png")).ToArray();
        if (_pages.Count > 1 && paths.Any(File.Exists) && MessageBox.Show(this, "Einige Seiten existieren bereits. Alle entsprechenden PNG-Dateien überschreiben?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            for (var i = 0; i < _pages.Count; i++) DiscordCardRenderer.Save(DiscordCardRenderer.Render(_pages[i]), paths[i]);
            _status.Text = $"{paths.Length} PNG-Datei(en) gespeichert. Du kannst sie jetzt selbst in Discord hochladen.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.NotSupportedException)
        { _status.Text = "Export fehlgeschlagen: " + ex.Message + " Bereits gespeicherte Seiten bleiben erhalten."; }
    }
}
