using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AionMeter.App.Windows;

public sealed class FarmGuideWindow : Window
{
    private static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(17, 19, 21));
    private static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(255, 155, 74));
    private readonly StackPanel _content = new();

    public FarmGuideWindow()
    {
        Title = "Aetherion — Farm-Guide";
        Width = 720;
        Height = 680;
        MinWidth = 560;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(10, 11, 13));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        var root = new DockPanel { Margin = new Thickness(22) };
        Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "FARM-GUIDE", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Gold });
        header.Children.Add(new TextBlock { Text = "Kinah, Pet-Seelen und sinnvolle Farm-Routen", Foreground = Brushes.LightGray, Margin = new Thickness(0, 4, 0, 0) });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _content };
        root.Children.Add(scroll);

        AddCard("Kinah: erst sichere Quellen", "Erledige zuerst Duties/Command-Aufträge und deine verfügbaren Dungeon-Abschlüsse. Für handelbare Einnahmen vergleiche vor dem Farmen die Marktpreise für Sammelmaterialien und verkaufbare Dungeon-Drops. Beachte den täglichen Kina-Zähler im Spiel: endloser Mob-Grind zahlt sich nach dem Limit deutlich schlechter aus.");
        AddCard("Pet-Farm: Elyos", "High-Level-Runde: Eternal Isle (etwa Level 49). Dort liegen mehrere Kerubar-, Kerubian- und Kerubiel-Spawns nah beieinander. Für eine niedrigere Route: Contaminated Sanctum (etwa Level 36) oder Ellun River Marsh (Level 13–14). Farm die Monster, die zu den Pet-Seelen passen, die deinem Genus noch fehlen.");
        AddCard("Pet-Farm: Asmodier", "High-Level-Runde: Immortal Isle North (etwa Level 49–50) mit mehreren Kuru- und weiteren Pet-Mobs in einem Gebiet. Für mittlere Level: Spirit Isle West (etwa Level 46) oder Tranein Highland (etwa Level 20). Prüfe vorher im Pet-Menü, welche Genus-Seelen dir tatsächlich noch Fortschritt geben.");
        AddCard("So kombinierst du Kinah und Pets", "Nutze Pet-Farmen als Mob-Grind, während dein tägliches Kina-Limit noch offen ist. Danach wechselst du zu Dungeons, Duties oder Sammeln, statt weiter nur auf rohe Mob-Kin zu hoffen. Die beste Route hängt von Fraktion, Level und aktuellen Marktpreisen ab; die Spots sind Community-Daten und können sich mit Patches ändern.");
        var source = new Button { Content = "Pet-Spawnkarte und vollständige Spotliste öffnen", Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 12), Background = Surface, Foreground = Gold, BorderBrush = Gold };
        source.Click += (_, _) => Process.Start(new ProcessStartInfo("https://raidrelay.com/aion-2-pets-genus-guide") { UseShellExecute = true });
        _content.Children.Add(source);
        var kinah = new Button { Content = "Aktuelle Kinah-Quellen ansehen", Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left, Background = Surface, Foreground = Gold, BorderBrush = Gold };
        kinah.Click += (_, _) => Process.Start(new ProcessStartInfo("https://metabot.gg/en/aion-2/guides/kinah-farming-guide") { UseShellExecute = true });
        _content.Children.Add(kinah);
        _content.Children.Add(new TextBlock { Text = "Quellen zuletzt geprüft: Oktober 2026. Aetherion und diese Empfehlungen sind nicht mit NCSOFT verbunden.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0, 14, 0, 0) });
    }

    private void AddCard(string title, string body)
    {
        var card = new Border { Background = Surface, BorderBrush = new SolidColorBrush(Color.FromRgb(48, 39, 31)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 10) };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, Foreground = Gold, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, LineHeight = 21 });
        card.Child = panel;
        _content.Children.Add(card);
    }
}
