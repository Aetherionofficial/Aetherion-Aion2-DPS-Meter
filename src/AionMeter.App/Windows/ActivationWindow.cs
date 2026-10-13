using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

public sealed class ActivationWindow : Window
{
    private readonly TextBox _key = new();
    public MeterLicense? License { get; private set; }

    public ActivationWindow()
    {
        Title = "Aetherion — Lizenz aktivieren";
        Width = 540;
        Height = 370;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(18, 21, 30));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        var panel = new StackPanel { Margin = new Thickness(26) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "AETHERION", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(236, 190, 93)) });
        panel.Children.Add(new TextBlock { Text = "Gib deine Installations-ID an den Eigentümer weiter, um einen Key zu erhalten.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) });
        var idRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var id = new TextBox { Text = LicenseService.InstallationId, IsReadOnly = true, Padding = new Thickness(8), FontFamily = new FontFamily("Consolas") };
        idRow.Children.Add(id);
        var copy = new Button { Content = "Kopieren", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 4, 10, 4) };
        copy.Click += (_, _) => Clipboard.SetText(id.Text);
        DockPanel.SetDock(copy, Dock.Right);
        idRow.Children.Add(copy);
        panel.Children.Add(idRow);
        panel.Children.Add(new TextBlock { Text = "Lizenz-Key", Margin = new Thickness(0, 0, 0, 6) });
        _key.AcceptsReturn = true;
        _key.TextWrapping = TextWrapping.Wrap;
        _key.Height = 86;
        _key.Padding = new Thickness(8);
        _key.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        panel.Children.Add(_key);
        panel.Children.Add(new TextBlock { Text = "Abo-Laufzeit startet bei Ausstellung des Keys. Freigegebene Partner können eine Lifetime-Lizenz erhalten.", Foreground = Brushes.LightGray, Margin = new Thickness(0, 8, 0, 16), TextWrapping = TextWrapping.Wrap });
        var activate = new Button { Content = "Key aktivieren", Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 130 };
        activate.Click += (_, _) =>
        {
            License = LicenseService.Activate(_key.Text);
            if (License is null)
            {
                MessageBox.Show(this, "Der Key ist ungültig, abgelaufen oder gehört zu einer anderen Installation.", "Key nicht gültig", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        };
        panel.Children.Add(activate);
    }
}
