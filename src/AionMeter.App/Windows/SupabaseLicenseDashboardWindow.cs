using System.Collections.ObjectModel;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

internal sealed class SupabaseLicenseDashboardWindow : Window
{
    private static readonly Color Bg = Color.FromRgb(12, 15, 20);
    private static readonly Color CardBg = Color.FromRgb(19, 24, 31);
    private static readonly Color CardAlt = Color.FromRgb(25, 32, 40);
    private static readonly Color Line = Color.FromRgb(48, 58, 67);
    private static readonly Color Text = Color.FromRgb(238, 241, 244);
    private static readonly Color Muted = Color.FromRgb(150, 163, 174);
    private static readonly Color Ember = Color.FromRgb(255, 151, 66);
    private static readonly Color Green = Color.FromRgb(81, 211, 174);
    private readonly SupabaseLicenseAdminService _service;
    private readonly ObservableCollection<LicenseAdminEntry> _items = [];
    private readonly DataGrid _grid = new();
    private readonly DataGrid _historyGrid = new();
    private readonly TextBox _search = new();
    private readonly ComboBox _statusFilter = new();
    private readonly ComboBox _type = new();
    private readonly ComboBox _count = new();
    private readonly TextBox _issuedKeys = new();
    private readonly TextBlock _message = new();
    private readonly TextBlock _activeStat = new();
    private readonly TextBlock _availableStat = new();
    private readonly TextBlock _revokedStat = new();
    private readonly TextBlock _expiredStat = new();
    private readonly Button _revokeButton = new();
    private readonly Button _resetDeviceButton = new();

    private MaintenanceStatus? _maintenance;
    private bool _savingMaintenance;
    private readonly TextBlock _maintenanceState = new() { Text = "Status wird geladen …", FontWeight = FontWeights.Bold };
    private readonly TextBox _maintenanceMessage = new() { MaxLength = 500, MinWidth = 250 };
    private readonly Button _enableMaintenance = new();
    private readonly Button _disableMaintenance = new();

    public SupabaseLicenseDashboardWindow(string accessToken)
    {
        _service = new SupabaseLicenseAdminService(accessToken);
        Title = "Aetherion · Lizenz-Dashboard";
        Width = 1280;
        Height = 920;
        MinWidth = 1050;
        MinHeight = 780;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush(Bg);
        Foreground = Brush(Text);
        FontFamily = new FontFamily("Segoe UI");
        BuildLayout();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock { Text = "AETHERION  /  OWNER CONSOLE", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush(Ember) });
        titleStack.Children.Add(new TextBlock { Text = "Lizenz-Dashboard", FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 2) });
        titleStack.Children.Add(new TextBlock { Text = "Kundenlizenzen verwalten · Keys werden nur bei der Erzeugung angezeigt", FontSize = 13, Foreground = Brush(Muted) });
        header.Children.Add(titleStack);
        var refresh = Button("↻  Aktualisieren", false);
        refresh.Margin = new Thickness(0, 8, 0, 0);
        refresh.Click += async (_, _) => await RefreshAsync();
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        root.Children.Add(header);

        var stats = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 16) };
        stats.Children.Add(StatCard("AKTIVE KUNDEN", _activeStat, Green));
        stats.Children.Add(StatCard("UNBENUTZTE KEYS", _availableStat, Ember));
        stats.Children.Add(StatCard("GESPERRT", _revokedStat, Color.FromRgb(255, 108, 102)));
        stats.Children.Add(StatCard("ABGELAUFEN", _expiredStat, Muted));
        stats.Children.Add(StatCard("LAUFEN IN 7 TAGEN AB", _expiringStat, Color.FromRgb(255, 196, 90)));
        Grid.SetRow(stats, 1);
        root.Children.Add(stats);

        root.RowDefinitions.Insert(2, new RowDefinition { Height = GridLength.Auto });
        var maintenanceCard = Card();
        maintenanceCard.Margin = new Thickness(0, 0, 0, 16);
        var maintenancePanel = new Grid();
        maintenancePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        maintenancePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        maintenancePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var maintenanceTitle = new StackPanel();
        maintenanceTitle.Children.Add(new TextBlock { Text = "APP-WARTUNG", Foreground = Brush(Ember), FontWeight = FontWeights.Bold });
        maintenanceTitle.Children.Add(_maintenanceState);
        maintenancePanel.Children.Add(maintenanceTitle);
        var messagePanel = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
        messagePanel.Children.Add(new TextBlock { Text = "Hinweis für Kunden · maximal 500 Zeichen", Foreground = Brush(Muted), Margin = new Thickness(0, 0, 0, 5) });
        StyleInput(_maintenanceMessage);
        messagePanel.Children.Add(_maintenanceMessage);
        Grid.SetColumn(messagePanel, 1); maintenancePanel.Children.Add(messagePanel);
        var maintenanceButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _enableMaintenance.Content = "Wartung aktivieren / Text speichern";
        _disableMaintenance.Content = "App freigeben";
        StyleButton(_enableMaintenance, true); StyleButton(_disableMaintenance, false);
        _disableMaintenance.Margin = new Thickness(8, 0, 0, 0);
        _enableMaintenance.IsEnabled = _disableMaintenance.IsEnabled = false;
        _enableMaintenance.Click += async (_, _) => await SaveMaintenanceAsync(true);
        _disableMaintenance.Click += async (_, _) => await SaveMaintenanceAsync(false);
        maintenanceButtons.Children.Add(_enableMaintenance); maintenanceButtons.Children.Add(_disableMaintenance);
        Grid.SetColumn(maintenanceButtons, 2); maintenancePanel.Children.Add(maintenanceButtons);
        maintenanceCard.Child = maintenancePanel;
        Grid.SetRow(maintenanceCard, 2); root.Children.Add(maintenanceCard);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(318) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 3);
        root.Children.Add(body);

        var issueCard = Card();
        issueCard.Margin = new Thickness(0, 0, 16, 0);
        var issuePanel = new StackPanel();
        issuePanel.Children.Add(SectionHeading("Keys erzeugen", "Neuer Einmal-Key für einen Kunden"));
        AddLabel(issuePanel, "LIZENZTYP");
        _type.Items.Add(new LicenseChoice("30 Tage · Standard", "days_30"));
        _type.Items.Add(new LicenseChoice("7 Tage", "week_1w"));
        _type.Items.Add(new LicenseChoice("1 Tag · Test", "trial_1d"));
        _type.Items.Add(new LicenseChoice("90 Tage", "days_90"));
        _type.Items.Add(new LicenseChoice("1 Jahr", "days_365"));
        _type.Items.Add(new LicenseChoice("Lifetime", "lifetime"));
        _type.Items.Add(new LicenseChoice("Partner · Lifetime", "partner"));
        _type.SelectedIndex = 0;
        StyleInput(_type);
        issuePanel.Children.Add(_type);

        AddLabel(issuePanel, "ANZAHL · MAX. 50");
        foreach (var count in new[] { 1, 5, 10, 25, 50 }) _count.Items.Add(count.ToString());
        _count.SelectedIndex = 0;
        StyleInput(_count);
        issuePanel.Children.Add(_count);

        var issue = Button("＋  Keys erstellen", true);
        issue.Margin = new Thickness(0, 14, 0, 12);
        issue.HorizontalAlignment = HorizontalAlignment.Stretch;
        issue.Click += async (_, _) => await CreateKeysAsync();
        issuePanel.Children.Add(issue);

        var oneTime = new Border { Background = new SolidColorBrush(Color.FromArgb(28, 255, 151, 66)), BorderBrush = Brush(Color.FromRgb(108, 74, 47)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12) };
        oneTime.Child = new TextBlock { Text = "Der vollständige Key wird nicht gespeichert und kann später nicht erneut abgerufen werden. Nach dem Erstellen sofort kopieren.", TextWrapping = TextWrapping.Wrap, Foreground = Brush(Color.FromRgb(255, 196, 143)), FontSize = 12 };
        issuePanel.Children.Add(oneTime);

        AddLabel(issuePanel, "NEU ERZEUGTE KEYS");
        _issuedKeys.AcceptsReturn = true;
        _issuedKeys.IsReadOnly = true;
        _issuedKeys.TextWrapping = TextWrapping.Wrap;
        _issuedKeys.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _issuedKeys.Height = 175;
        _issuedKeys.ToolTip = "Keys werden nur einmal hier angezeigt.";
        StyleInput(_issuedKeys);
        issuePanel.Children.Add(_issuedKeys);
        var copy = Button("⧉  Keys kopieren", false);
        copy.Margin = new Thickness(0, 8, 0, 0);
        copy.HorizontalAlignment = HorizontalAlignment.Stretch;
        copy.Click += (_, _) => CopyKeys();
        issuePanel.Children.Add(copy);
        var clearIssued = Button("Anzeige leeren", false);
        clearIssued.Margin = new Thickness(0, 7, 0, 0);
        clearIssued.HorizontalAlignment = HorizontalAlignment.Stretch;
        clearIssued.Click += (_, _) =>
        {
            _issuedKeys.Clear();
            _message.Text = "Key-Anzeige geleert. Bereits kopierte Keys bleiben in der Zwischenablage.";
            _message.Foreground = Brush(Muted);
        };
        issuePanel.Children.Add(clearIssued);
        issueCard.Child = new ScrollViewer { Content = issuePanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(issueCard);

        var listCard = Card();
        Grid.SetColumn(listCard, 1);
        body.Children.Add(listCard);
        var listPanel = new Grid();
        listPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        listPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listCard.Child = listPanel;

        var listHeader = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        listHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        listHeader.Children.Add(SectionHeading("Lizenzbestand", "Eingelöste Keys zeigen Discord-Konto und aktuellen Zugang"));
        _revokeButton.Content = "Key sperren";
        StyleButton(_revokeButton, false);
        _revokeButton.Background = Brush(Color.FromRgb(78, 38, 40));
        _revokeButton.IsEnabled = false;
        _revokeButton.Margin = new Thickness(8, 3, 0, 0);
        _revokeButton.Click += async (_, _) => await ApplySelectedLicenseActionAsync();
        Grid.SetColumn(_revokeButton, 1);
        listHeader.Children.Add(_revokeButton);
        _resetDeviceButton.Content = "Gerät zurücksetzen";
        StyleButton(_resetDeviceButton, false);
        _resetDeviceButton.Background = Brush(Color.FromRgb(45, 55, 73));
        _resetDeviceButton.IsEnabled = false;
        _resetDeviceButton.Margin = new Thickness(8, 3, 0, 0);
        _resetDeviceButton.Click += async (_, _) => await ResetSelectedDeviceAsync();
        Grid.SetColumn(_resetDeviceButton, 2);
        listHeader.Children.Add(_resetDeviceButton);
        listPanel.Children.Add(listHeader);

        var filters = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        _search.Margin = new Thickness(0, 0, 8, 0);
        _search.Padding = new Thickness(10, 8, 10, 8);
        _search.FontSize = 13;
        _search.Background = Brush(CardAlt);
        _search.Foreground = Brush(Text);
        _search.BorderBrush = Brush(Line);
        _search.BorderThickness = new Thickness(1);
        _search.ToolTip = "Key, Status, Discord-Name oder Account-ID suchen";
        _search.TextChanged += (_, _) => ApplyFilter();
        filters.Children.Add(_search);
        _statusFilter.Items.Add(new LicenseChoice("Alle Lizenzen", "all"));
        _statusFilter.Items.Add(new LicenseChoice("Aktive", "active"));
        _statusFilter.Items.Add(new LicenseChoice("Laufen bald ab", "expiring"));
        _statusFilter.Items.Add(new LicenseChoice("Unbenutzte", "available"));
        _statusFilter.Items.Add(new LicenseChoice("Gesperrte", "revoked"));
        _statusFilter.Items.Add(new LicenseChoice("Abgelaufene", "expired"));
        _statusFilter.SelectedIndex = 0;
        StyleInput(_statusFilter);
        _statusFilter.SelectionChanged += (_, _) => ApplyFilter();
        Grid.SetColumn(_statusFilter, 1);
        filters.Children.Add(_statusFilter);
        Grid.SetRow(filters, 1);
        listPanel.Children.Add(filters);

        ConfigureGrid();
        ConfigureHistoryGrid();
        var tabs = new TabControl { Background = Brush(CardBg), Foreground = Brush(Text), BorderBrush = Brush(Line) };
        tabs.ItemContainerStyle = CreateDarkTabStyle();
        var licensesTab = new TabItem { Header = "Lizenzen", Content = _grid };
        var historyTab = new TabItem { Header = "Änderungsverlauf", Content = _historyGrid };
        tabs.Items.Add(licensesTab);
        tabs.Items.Add(historyTab);
        Grid.SetRow(tabs, 2);
        listPanel.Children.Add(tabs);
        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(_message);
        _message.Text = "Verbinde mit Supabase …";
        _message.Foreground = Brush(Muted);
        _message.FontSize = 12;
        listPanel.Children.Add(footer);
        Grid.SetRow(footer, 3);

        var globalMessage = new TextBlock { Text = "Nur für Aetherion-Administratoren · Änderungen werden sofort auf die Lizenzdatenbank angewendet.", Foreground = Brush(Muted), FontSize = 11, Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(globalMessage, 4);
        root.Children.Add(globalMessage);
    }

    private void ConfigureHistoryGrid()
    {
        _historyGrid.AutoGenerateColumns = false;
        _historyGrid.IsReadOnly = true;
        _historyGrid.SelectionMode = DataGridSelectionMode.Single;
        _historyGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _historyGrid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        _historyGrid.HorizontalGridLinesBrush = Brush(Line);
        _historyGrid.VerticalGridLinesBrush = Brush(Bg);
        _historyGrid.Background = Brush(CardBg);
        _historyGrid.Foreground = Brush(Text);
        _historyGrid.BorderBrush = Brush(Line);
        _historyGrid.RowBackground = Brush(CardBg);
        _historyGrid.AlternatingRowBackground = Brush(Color.FromRgb(22, 28, 35));
        _historyGrid.ColumnHeaderHeight = 34;
        _historyGrid.RowHeight = 40;
        _historyGrid.CanUserAddRows = false;
        StyleDataGrid(_historyGrid);
        AddHistoryColumn("ZEITPUNKT", "CreatedAt", 145, "dd.MM.yy HH:mm:ss");
        AddHistoryColumn("AKTION", "Action", 150);
        AddHistoryColumn("KEY-ID", "Fingerprint", 120);
        AddHistoryColumn("DISCORD-KONTO", "AccountName", 180);
        AddHistoryColumn("DETAILS", "Details", 250);
    }

    private void AddHistoryColumn(string title, string path, double width, string? format = null)
    {
        var binding = new Binding(path);
        if (format is not null) binding.StringFormat = format;
        _historyGrid.Columns.Add(new DataGridTextColumn { Header = title, Binding = binding, Width = new DataGridLength(width), ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center), new Setter(TextBlock.MarginProperty, new Thickness(7, 0, 6, 0)), new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis) } } });
    }

    private void ConfigureGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.IsReadOnly = true;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        _grid.HorizontalGridLinesBrush = Brush(Line);
        _grid.VerticalGridLinesBrush = Brush(Bg);
        _grid.Background = Brush(CardBg);
        _grid.Foreground = Brush(Text);
        _grid.BorderBrush = Brush(Line);
        _grid.RowBackground = Brush(CardBg);
        _grid.AlternatingRowBackground = Brush(Color.FromRgb(22, 28, 35));
        _grid.ColumnHeaderHeight = 34;
        _grid.RowHeight = 42;
        _grid.CanUserAddRows = false;
        StyleDataGrid(_grid);
        _grid.ItemsSource = _items;
        _grid.SelectionChanged += (_, _) => UpdateLicenseActionButton();

        AddColumn("Key-ID", "Fingerprint", 102);
        AddColumn("TYP", "PlanLabel", 122);
        AddColumn("STATUS", "StatusLabel", 92);
        AddColumn("DISCORD-KONTO", "AccountName", 190);
        AddColumn("GERÄT", "InstallationLabel", 105);
        AddColumn("AUSGESTELLT", "IssuedAt", 126, "dd.MM.yy HH:mm");
        AddColumn("GÜLTIG BIS", "ExpiresAt", 128, "dd.MM.yy HH:mm");
    }

    private void AddColumn(string title, string path, double width, string? format = null)
    {
        var binding = new Binding(path);
        if (format is not null) binding.StringFormat = format;
        _grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = binding, Width = new DataGridLength(width), ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center), new Setter(TextBlock.MarginProperty, new Thickness(7, 0, 6, 0)), new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis) } } });
    }

    private async Task RefreshAsync()
    {
        try
        {
            _message.Text = "Lizenzdaten werden geladen …";
            if (!_savingMaintenance) ShowMaintenanceState(await _service.GetMaintenanceAsync());
            var (keys, truncated) = await _service.ListAsync();
            _items.Clear();
            foreach (var key in keys) _items.Add(key);
            UpdateStats();
            ApplyFilter();
            await RefreshHistoryAsync();
            _message.Text = truncated ? "Die neuesten 1.000 Keys werden angezeigt. Suche, Sperren und Freigeben bleiben verfügbar." : $"Stand: {DateTime.Now:dd.MM.yyyy · HH:mm}  ·  {_items.Count} Keys";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _message.Text = ex.Message;
            _message.Foreground = Brush(Color.FromRgb(255, 133, 123));
        }
    }

    internal void ShowMaintenancePreview() => ShowMaintenanceState(new MaintenanceStatus(false,
        "Wir führen gerade Wartungsarbeiten durch. Bitte versuche es später erneut.", 0));

    private void ShowMaintenanceState(MaintenanceStatus state)
    {
        _maintenance = state;
        _maintenanceState.Text = state.Enabled ? "AKTIV · App gesperrt" : "AUS · App freigegeben";
        _maintenanceState.Foreground = Brush(state.Enabled ? Ember : Green);
        _maintenanceMessage.Text = state.Message;
        _enableMaintenance.Content = state.Enabled ? "Wartungstext speichern" : "Wartung aktivieren";
        _enableMaintenance.IsEnabled = true;
        _disableMaintenance.IsEnabled = state.Enabled;
    }

    private async Task SaveMaintenanceAsync(bool enabled)
    {
        if (_maintenance is null || _savingMaintenance) return;
        var message = _maintenanceMessage.Text.Trim();
        if (message.Length == 0) { _message.Text = "Bitte einen Wartungstext eingeben."; return; }
        if (enabled && !_maintenance.Enabled && MessageBox.Show(this,
            "Die Hauptapp wird für alle Nutzer gesperrt. Laufende Apps erkennen die Wartung innerhalb von etwa 15 Sekunden. Wartung jetzt aktivieren?",
            "Wartungsarbeiten aktivieren", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _savingMaintenance = true;
        _enableMaintenance.IsEnabled = _disableMaintenance.IsEnabled = false;
        try
        {
            ShowMaintenanceState(await _service.SetMaintenanceAsync(enabled, message, _maintenance.Revision));
            _message.Text = enabled ? "Wartung aktiv. Kunden sehen den Wartungshinweis." : "Wartung beendet. Kunden können die App wieder starten.";
            _message.Foreground = Brush(Green);
        }
        catch (Exception ex)
        {
            _message.Text = ex.Message;
            _message.Foreground = Brush(Ember);
            // A failed or conflicting write must be reloaded before another change is submitted.
            _maintenance = null;
            _maintenanceState.Text = "Bitte aktualisieren";
            _enableMaintenance.IsEnabled = _disableMaintenance.IsEnabled = false;
        }
        finally { _savingMaintenance = false; }
    }

    private async Task CreateKeysAsync()
    {
        if (_type.SelectedItem is not LicenseChoice choice || !int.TryParse(_count.SelectedItem?.ToString(), out var count)) return;
        try
        {
            _message.Text = "Keys werden sicher erzeugt …";
            var issued = await _service.CreateAsync(choice.Value, count);
            _issuedKeys.Text = string.Join(Environment.NewLine, issued.Select(x => x.Key));
            await RefreshAsync();
            _message.Text = $"{issued.Count} Key{(issued.Count == 1 ? "" : "s")} erzeugt. Vollständige Keys stehen nur in der linken Box.";
            _message.Foreground = Brush(Green);
            await RefreshHistoryAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _message.Text = ex.Message;
            _message.Foreground = Brush(Color.FromRgb(255, 133, 123));
        }
    }

    private void UpdateLicenseActionButton()
    {
        var selected = _grid.SelectedItem as LicenseAdminEntry;
        var isRevoked = selected?.Status == "revoked";
        _revokeButton.Content = isRevoked ? "Zugang freigeben" : "Key sperren";
        _revokeButton.Background = Brush(isRevoked ? Color.FromRgb(31, 79, 65) : Color.FromRgb(78, 38, 40));
        _revokeButton.IsEnabled = selected is not null;
        _resetDeviceButton.IsEnabled = selected?.InstallationState == "active";
    }

    private async Task ResetSelectedDeviceAsync()
    {
        if (_grid.SelectedItem is not LicenseAdminEntry selected || selected.InstallationState != "active") return;
        var result = MessageBox.Show(this,
            $"Gerätebindung für {selected.AccountName ?? "diesen Kunden"} (Key {selected.Fingerprint}) zurücksetzen?{Environment.NewLine}{Environment.NewLine}" +
            "Beim nächsten Login wird das verwendete Gerät neu gebunden. Der Lizenzstatus und die verbleibende Laufzeit ändern sich nicht.",
            "Gerätebindung zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            _message.Text = "Gerätebindung wird zurückgesetzt …";
            await _service.ResetDeviceAsync(selected.KeyHash);
            _message.Foreground = Brush(Muted);
            await RefreshAsync();
            _message.Text = $"Gerätebindung für {selected.Fingerprint} wurde zurückgesetzt. Die Lizenz bleibt unverändert.";
            _message.Foreground = Brush(Green);
            await RefreshHistoryAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _message.Text = ex.Message;
            _message.Foreground = Brush(Color.FromRgb(255, 133, 123));
        }
    }

    private async Task ApplySelectedLicenseActionAsync()
    {
        if (_grid.SelectedItem is not LicenseAdminEntry selected) return;
        if (selected.Status == "revoked")
        {
            await RestoreSelectedAsync(selected);
            return;
        }

        var result = MessageBox.Show(this,
            $"Key {selected.Fingerprint} sperren?{Environment.NewLine}{Environment.NewLine}" +
            (selected.RedeemedAt is null ? "Ein noch nicht eingelöster Key kann danach nicht mehr verwendet werden." : $"Der Zugang von {selected.AccountName ?? "diesem Kunden"} wird ebenfalls gesperrt."),
            "Lizenz-Key sperren", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            _message.Text = "Key wird gesperrt …";
            await _service.RevokeAsync(selected.KeyHash);
            _message.Foreground = Brush(Muted);
            await RefreshAsync();
            _message.Text = $"Key {selected.Fingerprint} wurde gesperrt.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _message.Text = ex.Message;
            _message.Foreground = Brush(Color.FromRgb(255, 133, 123));
        }
    }

    private async Task RestoreSelectedAsync(LicenseAdminEntry selected)
    {
        var account = selected.AccountName ?? "diesem Kunden";
        var result = MessageBox.Show(this,
            $"Zugang für {account} (Key {selected.Fingerprint}) wieder freigeben?{Environment.NewLine}{Environment.NewLine}" +
            "Die Gerätesperre wird zurückgesetzt; beim nächsten Login wird das verwendete Gerät neu gebunden. Die verbleibende Laufzeit wird nicht verlängert.",
            "Lizenzzugang freigeben", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            _message.Text = "Zugang wird freigegeben …";
            await _service.RestoreAsync(selected.KeyHash);
            _message.Foreground = Brush(Muted);
            await RefreshAsync();
            _message.Text = $"Zugang für Key {selected.Fingerprint} wurde freigegeben. Die Laufzeit bleibt unverändert.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _message.Text = ex.Message;
            _message.Foreground = Brush(Color.FromRgb(255, 133, 123));
        }
    }

    private void UpdateStats()
    {
        _activeStat.Text = _items.Where(x => x.Status == "active" && x.AccountId is not null)
            .Select(x => x.AccountId).Distinct(StringComparer.Ordinal).Count().ToString("N0");
        _availableStat.Text = _items.Count(x => x.Status == "available").ToString("N0");
        _revokedStat.Text = _items.Count(x => x.Status == "revoked").ToString("N0");
        _expiredStat.Text = _items.Count(x => x.Status == "expired").ToString("N0");
        var now = DateTimeOffset.UtcNow;
        _expiringStat.Text = _items.Count(x => x.Status == "active" && x.ExpiresAt is { } expiry && expiry > now && expiry <= now.AddDays(7)).ToString("N0");
    }

    private void ApplyFilter()
    {
        var filter = _search.Text.Trim();
        var searchedKeyHash = filter.StartsWith("AETH-", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(filter.ToUpperInvariant()))).ToLowerInvariant()
            : null;
        var selectedStatus = (_statusFilter.SelectedItem as LicenseChoice)?.Value ?? "all";
        var view = CollectionViewSource.GetDefaultView(_grid.ItemsSource);
        view.Filter = item => item is LicenseAdminEntry row &&
            (string.IsNullOrEmpty(filter) || row.Fingerprint.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
             (searchedKeyHash is not null && row.KeyHash.Equals(searchedKeyHash, StringComparison.OrdinalIgnoreCase)) ||
             row.AccountName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
             row.AccountId?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
             row.PlanLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) || row.StatusLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)) &&
            (selectedStatus == "all" || (selectedStatus == "expiring"
                ? row.Status == "active" && row.ExpiresAt is { } expiry && expiry > DateTimeOffset.UtcNow && expiry <= DateTimeOffset.UtcNow.AddDays(7)
                : row.Status == selectedStatus));
        view.Refresh();
    }

    private async Task RefreshHistoryAsync()
    {
        try { _historyGrid.ItemsSource = await _service.HistoryAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _historyGrid.ItemsSource = Array.Empty<LicenseAdminHistoryEntry>();
        }
    }

    private void CopyKeys()
    {
        if (string.IsNullOrWhiteSpace(_issuedKeys.Text))
        {
            _message.Text = "Erzeuge zuerst Keys, um sie hier zu kopieren.";
            return;
        }
        try
        {
            Clipboard.SetText(_issuedKeys.Text);
            _message.Text = "Keys kopiert. Du kannst die Anzeige jetzt leeren; die Zwischenablage bleibt erhalten.";
            _message.Foreground = Brush(Green);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            _message.Text = "Zwischenablage gerade nicht verfügbar. Markiere und kopiere die Keys manuell.";
        }
    }

    private readonly TextBlock _expiringStat = new();

    private Border StatCard(string label, TextBlock value, Color accent)
    {
        value.Text = "—";
        value.FontSize = 24;
        value.FontWeight = FontWeights.SemiBold;
        value.Foreground = Brush(accent);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = label, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush(Muted) });
        stack.Children.Add(value);
        var card = Card();
        card.Margin = new Thickness(0, 0, 12, 0);
        card.Padding = new Thickness(14, 10, 12, 8);
        card.Child = stack;
        return card;
    }

    private static Border Card() => new() { Background = Brush(CardBg), BorderBrush = Brush(Line), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(14) };

    private static StackPanel SectionHeading(string title, string subtitle)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = Brush(Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        return panel;
    }

    private static void AddLabel(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush(Muted), Margin = new Thickness(0, 9, 0, 5) });

    private static void StyleInput(Control control)
    {
        control.Padding = new Thickness(9, 7, 9, 7);
        control.Background = Brush(CardAlt);
        control.Foreground = Brush(Text);
        control.BorderBrush = Brush(Line);
        control.BorderThickness = new Thickness(1);
        control.FontSize = 13;

        if (control is ComboBox combo)
        {
            combo.MaxDropDownHeight = 320;
            combo.Template = CreateDarkComboTemplate();
            combo.Resources[SystemColors.WindowBrushKey] = Brush(CardBg);
            combo.Resources[SystemColors.WindowTextBrushKey] = Brush(Text);
            combo.Resources[SystemColors.ControlBrushKey] = Brush(CardBg);
            combo.Resources[SystemColors.ControlTextBrushKey] = Brush(Text);
            combo.Resources[SystemColors.HighlightBrushKey] = Brush(Color.FromRgb(111, 68, 39));
            combo.Resources[SystemColors.HighlightTextBrushKey] = Brush(Text);

            var optionStyle = new Style(typeof(ComboBoxItem));
            optionStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush(CardBg)));
            optionStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
            optionStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
            optionStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            optionStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

            var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            highlighted.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Color.FromRgb(52, 43, 39))));
            highlighted.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
            optionStyle.Triggers.Add(highlighted);

            var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Color.FromRgb(78, 50, 34))));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Color.FromRgb(255, 208, 166))));
            optionStyle.Triggers.Add(selected);

            combo.ItemContainerStyle = optionStyle;
        }
    }

    private static ControlTemplate CreateDarkComboTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.SetBinding(Border.BackgroundProperty, TemplatedBinding(nameof(Control.Background)));
        chrome.SetBinding(Border.BorderBrushProperty, TemplatedBinding(nameof(Control.BorderBrush)));
        chrome.SetBinding(Border.BorderThicknessProperty, TemplatedBinding(nameof(Control.BorderThickness)));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        root.AppendChild(chrome);

        var selected = new FrameworkElementFactory(typeof(ContentPresenter));
        selected.SetBinding(ContentPresenter.ContentProperty, new Binding("SelectionBoxItem") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        selected.SetBinding(ContentPresenter.ContentTemplateProperty, TemplatedBinding(nameof(ItemsControl.ItemTemplate)));
        selected.SetValue(TextElement.ForegroundProperty, Brush(Text));
        selected.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 0, 32, 0));
        selected.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        selected.SetValue(UIElement.IsHitTestVisibleProperty, false);
        root.AppendChild(selected);

        var toggle = new FrameworkElementFactory(typeof(ToggleButton), "PART_Toggle");
        toggle.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        toggle.SetValue(FrameworkElement.WidthProperty, 30d);
        toggle.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        toggle.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay });
        var arrowTemplate = new ControlTemplate(typeof(ToggleButton));
        var arrow = new FrameworkElementFactory(typeof(TextBlock));
        arrow.SetValue(TextBlock.TextProperty, "⌄");
        arrow.SetValue(TextBlock.ForegroundProperty, Brush(Muted));
        arrow.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        arrow.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowTemplate.VisualTree = arrow;
        toggle.SetValue(Control.TemplateProperty, arrowTemplate);
        root.AppendChild(toggle);

        var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, Brush(CardBg));
        popupBorder.SetValue(Border.BorderBrushProperty, Brush(Line));
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(FrameworkElement.MaxHeightProperty, 320d);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        popupBorder.AppendChild(scroll);
        popup.AppendChild(popupBorder);
        root.AppendChild(popup);

        return new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
    }

    private static Style CreateDarkTabStyle()
    {
        var style = new Style(typeof(TabItem));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Muted)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(13, 7, 13, 7)));
        var border = new FrameworkElementFactory(typeof(Border), "TabBorder");
        border.SetBinding(Border.BackgroundProperty, TemplatedBinding(nameof(Control.Background)));
        border.SetBinding(Border.BorderBrushProperty, TemplatedBinding(nameof(Control.BorderBrush)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5, 5, 0, 0));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        var header = new FrameworkElementFactory(typeof(ContentPresenter));
        header.SetBinding(ContentPresenter.ContentProperty, TemplatedBinding(nameof(HeaderedContentControl.Header)));
        header.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        header.SetBinding(FrameworkElement.MarginProperty, TemplatedBinding(nameof(Control.Padding)));
        border.AppendChild(header);
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(TabItem)) { VisualTree = border }));
        var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, Brush(CardAlt)));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
        selected.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(Ember)));
        style.Triggers.Add(selected);
        return style;
    }

    private static Button Button(string label, bool primary)
    {
        var button = new Button { Content = label };
        StyleButton(button, primary);
        return button;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.Padding = new Thickness(13, 8, 13, 8);
        button.BorderBrush = Brush(primary ? Ember : Line);
        button.BorderThickness = new Thickness(1);
        button.Background = Brush(primary ? Color.FromRgb(113, 63, 34) : CardAlt);
        button.Foreground = Brush(Text);
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, TemplatedBinding(nameof(Control.Background)));
        border.SetBinding(Border.BorderBrushProperty, TemplatedBinding(nameof(Control.BorderBrush)));
        border.SetBinding(Border.BorderThicknessProperty, TemplatedBinding(nameof(Control.BorderThickness)));
        border.SetBinding(Border.PaddingProperty, TemplatedBinding(nameof(Control.Padding)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(ContentPresenter.ContentProperty, TemplatedBinding(nameof(ContentControl.Content)));
        content.SetBinding(ContentPresenter.ContentTemplateProperty, TemplatedBinding(nameof(ContentControl.ContentTemplate)));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static Binding TemplatedBinding(string path) => new(path)
    {
        RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
    };

    private static void StyleDataGrid(DataGrid grid)
    {
        grid.Resources[SystemColors.HighlightBrushKey] = Brush(Color.FromRgb(66, 43, 31));
        grid.Resources[SystemColors.HighlightTextBrushKey] = Brush(Text);
        grid.Resources[SystemColors.ControlBrushKey] = Brush(CardBg);
        grid.Resources[SystemColors.ControlTextBrushKey] = Brush(Text);

        grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
        grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush(CardAlt)));
        grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Muted)));
        grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(Line)));
        grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 0, 6, 0)));
        grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));

        grid.RowStyle = new Style(typeof(DataGridRow));
        grid.RowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush(CardBg)));
        grid.RowStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
        var selectedRow = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selectedRow.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Color.FromRgb(66, 43, 31))));
        selectedRow.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
        grid.RowStyle.Triggers.Add(selectedRow);

        grid.CellStyle = new Style(typeof(DataGridCell));
        grid.CellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        grid.CellStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
        grid.CellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        var selectedCell = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectedCell.Setters.Add(new Setter(Control.BackgroundProperty, Brush(Color.FromRgb(66, 43, 31))));
        selectedCell.Setters.Add(new Setter(Control.ForegroundProperty, Brush(Text)));
        grid.CellStyle.Triggers.Add(selectedCell);
    }

    private static SolidColorBrush Brush(Color color) => new(color);
    private sealed record LicenseChoice(string Label, string Value) { public override string ToString() => Label; }
}
