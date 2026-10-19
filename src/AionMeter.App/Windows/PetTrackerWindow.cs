using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

/// <summary>Local pet collection tracker. Ownership is intentionally player-controlled because the game does not expose a collection API.</summary>
public sealed class PetTrackerWindow : Window
{
    private sealed record PetEntry(string Id, string Name, string Area, string Source, string Faction, string Level);

    private static readonly IReadOnlyList<PetEntry> Catalog =
    [
        new("kerubar", "Kerubar", "Eternal Isle", "Kerubar-Spawns", "Elyos", "ca. 49"),
        new("kerubian", "Kerubian", "Eternal Isle", "Kerubian-Spawns", "Elyos", "ca. 49"),
        new("kerubiel", "Kerubiel", "Eternal Isle", "Kerubiel-Spawns", "Elyos", "ca. 49"),
        new("contaminated-kerubar", "Kontaminierter Kerubar", "Contaminated Sanctum", "Elite-Mobs im Sanctum", "Elyos", "ca. 36"),
        new("kuru", "Kuru", "Immortal Isle North", "Kuru-Spawns", "Asmodier", "ca. 49–50"),
        new("spirit-isle", "Geistinsel-Begleiter", "Spirit Isle West", "Pet-Seelen der Insel-Mobs", "Asmodier", "ca. 46"),
        new("tranein", "Tranein-Begleiter", "Tranein Highland", "Pet-Seelen der Highland-Mobs", "Asmodier", "ca. 20"),
        new("ellun", "Ellun-Begleiter", "Ellun River Marsh", "Pet-Seelen der Sumpf-Mobs", "Elyos", "ca. 13–14"),
    ];

    private static readonly Brush Surface = Brush("#121216");
    private static readonly Brush SurfaceAlt = Brush("#1B191A");
    private static readonly Brush Border = Brush("#493B39");
    private static readonly Brush Flame = Brush("#F28A45");
    private static readonly Brush Muted = Brush("#A7A9AE");
    private readonly AppSettings _settings;
    private readonly TextBlock _progress = new();
    private readonly ProgressBar _bar = new();
    private readonly TextBox _search = new();
    private readonly StackPanel _list = new();
    private bool _completionNotified;

    public PetTrackerWindow(AppSettings settings)
    {
        _settings = settings;
        Title = "Aetherion · Pet-Tracker";
        Width = 760;
        Height = 700;
        MinWidth = 620;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#09090B");
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        Controls.DragAnywhere.Attach(this);

        var root = new DockPanel { Margin = new Thickness(22) };
        Content = root;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "AETHERION  ·  PET-TRACKER", Foreground = Flame, FontSize = 11, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock { Text = "Sammlung verfolgen und Fundorte direkt öffnen", FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) });
        header.Children.Add(new TextBlock { Text = "Die Markierungen werden lokal gespeichert. Gebiet öffnen springt direkt zur AION2-Karte.", Foreground = Muted, Margin = new Thickness(0, 4, 0, 0) });

        var summary = new Border { Background = Surface, BorderBrush = Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(summary, Dock.Top);
        root.Children.Add(summary);
        var summaryPanel = new StackPanel();
        summary.Child = summaryPanel;
        _progress.FontSize = 15;
        _progress.FontWeight = FontWeights.SemiBold;
        _progress.Foreground = Flame;
        summaryPanel.Children.Add(_progress);
        _bar.Minimum = 0;
        _bar.Maximum = Catalog.Count;
        _bar.Height = 8;
        _bar.Margin = new Thickness(0, 9, 0, 0);
        _bar.Foreground = Flame;
        _bar.Background = Border;
        summaryPanel.Children.Add(_bar);

        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(filters, Dock.Top);
        root.Children.Add(filters);
        _search.Width = 250;
        _search.Height = 34;
        _search.Padding = new Thickness(9, 5, 9, 5);
        _search.Background = SurfaceAlt;
        _search.Foreground = Brushes.White;
        _search.BorderBrush = Border;
        _search.ToolTip = "Pet oder Gebiet suchen";
        _search.TextChanged += (_, _) => RefreshList();
        filters.Children.Add(_search);
        var hint = new TextBlock { Text = "  Tipp: Gebiet anklicken, um die Karte zu öffnen.", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        filters.Children.Add(hint);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list };
        root.Children.Add(scroll);
        RefreshList();
    }

    private void RefreshList()
    {
        _list.Children.Clear();
        var query = _search.Text.Trim();
        var rows = Catalog.Where(p => query.Length == 0 || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Area.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Faction.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var pet in rows) _list.Children.Add(CreateRow(pet));
        var owned = Catalog.Count(p => _settings.OwnedPetIds.Contains(p.Id));
        _progress.Text = $"{owned} / {Catalog.Count} Pets gesammelt  ·  {Catalog.Count - owned} fehlen noch";
        _bar.Value = owned;
    }

    private Border CreateRow(PetEntry pet)
    {
        var row = new Border { Background = SurfaceAlt, BorderBrush = Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var owned = new CheckBox { IsChecked = _settings.OwnedPetIds.Contains(pet.Id), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Als gesammelt markieren" };
        owned.Checked += (_, _) => SetOwned(pet.Id, true);
        owned.Unchecked += (_, _) => SetOwned(pet.Id, false);
        Grid.SetColumn(owned, 0);
        grid.Children.Add(owned);
        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = pet.Name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock { Text = $"{pet.Area}  ·  {pet.Faction}  ·  Level {pet.Level}", Foreground = Flame, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        info.Children.Add(new TextBlock { Text = $"Quelle: {pet.Source}", Foreground = Muted, FontSize = 10, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        var area = new Button { Content = "Gebiet öffnen", Padding = new Thickness(10, 6, 10, 6), Background = Brush("#261914"), Foreground = Flame, BorderBrush = Brush("#78391F"), Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        area.Click += (_, _) => AppHost.Current.ShowAionMap(pet.Area);
        Grid.SetColumn(area, 2);
        grid.Children.Add(area);
        row.Child = grid;
        return row;
    }

    private void SetOwned(string id, bool value)
    {
        if (value) _settings.OwnedPetIds.Add(id);
        else _settings.OwnedPetIds.Remove(id);
        _settings.Save();
        RefreshList();
        var complete = Catalog.All(p => _settings.OwnedPetIds.Contains(p.Id));
        if (complete && !_completionNotified)
        {
            _completionNotified = true;
            AppHost.Current.NotifyPetCollectionComplete();
        }
        else if (!complete)
        {
            _completionNotified = false;
        }
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
