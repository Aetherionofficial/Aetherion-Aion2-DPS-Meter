using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

public sealed class KeyIssuerWindow : Window
{
    private readonly TextBox _machine = new() { MaxLength = 24 };
    private readonly ComboBox _kind = new();
    private readonly ComboBox _duration = new();
    private readonly TextBox _partner = new();
    private readonly TextBox _code = new();
    private readonly TextBox _output = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 102, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox _revokeId = new();
    private readonly TextBlock _status = new();
    private readonly string _privatePath = Path.Combine(AppContext.BaseDirectory, "owner-private.pem");

    public KeyIssuerWindow()
    {
        Title = "Aetherion — Eigentümer Key-Ausgabe";
        Width = 600;
        Height = 690;
        MinWidth = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(18, 21, 30));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        panel.Children.Add(new TextBlock { Text = "AETHERION • KEY-VERWALTUNG", FontSize = 21, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(236, 190, 93)) });
        panel.Children.Add(new TextBlock { Text = "Nur für den Eigentümer. Behalte owner-private.pem geheim und sichere davon ein Backup.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Orange, Margin = new Thickness(0, 8, 0, 16) });
        AddField(panel, "Installations-ID des Nutzers", _machine);
        _kind.Items.Add("Kunden-Key");
        _kind.Items.Add("Partner-Key mit Badge");
        _kind.SelectedIndex = 0;
        AddField(panel, "Key-Typ", _kind);
        _duration.Items.Add(new DurationChoice("1 Tag", 1));
        _duration.Items.Add(new DurationChoice("1 Woche", 7));
        _duration.Items.Add(new DurationChoice("1 Monat (30 Tage)", 30));
        _duration.Items.Add(new DurationChoice("1 Jahr (365 Tage)", 365));
        _duration.Items.Add(new DurationChoice("Lifetime (nur Partner)", null));
        _duration.SelectedIndex = 2;
        AddField(panel, "Laufzeit ab Ausstellung", _duration);
        AddField(panel, "Partnername (für Partner-Badge)", _partner);
        AddField(panel, "Partner-Code (15 % Rabatt)", _code);
        _kind.SelectionChanged += (_, _) => UpdatePartnerFields();
        UpdatePartnerFields();
        var issue = new Button { Content = "Signierten Key erstellen", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 6, 0, 8), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromRgb(236, 190, 93)), Foreground = Brushes.Black, FontWeight = FontWeights.SemiBold };
        issue.Click += Issue_Click;
        panel.Children.Add(issue);
        _status.Margin = new Thickness(0, 0, 0, 8);
        _status.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "Key — komplett kopieren und dem Nutzer senden", Margin = new Thickness(0, 0, 0, 6) });
        _output.Padding = new Thickness(8);
        panel.Children.Add(_output);
        var copy = new Button { Content = "Key kopieren", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(_output.Text)) Clipboard.SetText(_output.Text); };
        panel.Children.Add(copy);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "KEY SPERREN", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(236, 190, 93)) });
        panel.Children.Add(new TextBlock { Text = "Key-ID aus issued-keys.csv eintragen. Die signierte Sperrliste muss anschließend auf deinen konfigurierten Sperrlisten-Server hochgeladen werden.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, Margin = new Thickness(0, 6, 0, 8) });
        AddField(panel, "Lizenz-ID (32 Zeichen)", _revokeId);
        var revoke = new Button { Content = "Key sperren und Sperrliste signieren", Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromRgb(113, 40, 44)), Foreground = Brushes.White };
        revoke.Click += Revoke_Click;
        panel.Children.Add(revoke);
    }

    private static void AddField(Panel panel, string label, Control control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 4), Foreground = Brushes.LightGray });
        control.Padding = new Thickness(8, 6, 8, 6);
        control.Margin = new Thickness(0, 0, 0, 4);
        panel.Children.Add(control);
    }

    private void UpdatePartnerFields()
    {
        var partnerMode = _kind.SelectedIndex == 1;
        _partner.IsEnabled = partnerMode;
        _partner.Opacity = partnerMode ? 1 : .55;
        if (partnerMode) _duration.SelectedIndex = 4;
        else if ((_duration.SelectedItem as DurationChoice)?.Days is null) _duration.SelectedIndex = 2;
    }

    private void Issue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var machine = _machine.Text.Trim().ToUpperInvariant();
            if (machine.Length != 24 || machine.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Die Installations-ID muss aus 24 Hex-Zeichen bestehen.");
            if (!File.Exists(_privatePath)) throw new FileNotFoundException("owner-private.pem fehlt neben dem Eigentümer-Programm. Diese Datei gehört nur dir.", _privatePath);
            var days = (_duration.SelectedItem as DurationChoice)?.Days;
            var isPartner = _kind.SelectedIndex == 1;
            if (days is null && !isPartner) throw new InvalidOperationException("Lifetime ist nur für freigegebene Partner verfügbar.");
            var partnerName = isPartner ? _partner.Text.Trim() : null;
            var code = _code.Text.Trim().ToUpperInvariant();
            if (isPartner && string.IsNullOrWhiteSpace(partnerName)) throw new InvalidOperationException("Für einen Partner-Key ist ein Partnername erforderlich.");
            if (isPartner && string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Bitte einen eindeutigen Partner-Code festlegen.");
            var now = DateTimeOffset.UtcNow;
            var expires = days is { } term ? now.AddDays(term) : (DateTimeOffset?)null;
            var license = new MeterLicense("Aetherion", machine, expires, Guid.NewGuid().ToString("N"), partnerName,
                string.IsNullOrWhiteSpace(code) ? null : code, string.IsNullOrWhiteSpace(code) ? 0 : 15);
            var payload = JsonSerializer.SerializeToUtf8Bytes(license);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(_privatePath));
            var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            _output.Text = Base64Url(payload) + "." + Base64Url(signature);
            _status.Text = $"Key für {machine} · {(license.ExpiresUtc is { } end ? $"gültig bis {end.ToLocalTime():dd.MM.yyyy HH:mm}" : "Lifetime")}." +
                           (isPartner ? $" Partner-Badge „{partnerName}“ · Code {code} · 15 %." : string.IsNullOrWhiteSpace(code) ? "" : $" Rabattcode {code} erfasst · 15 % manuell anwenden.");
            var logPath = Path.Combine(AppContext.BaseDirectory, "issued-keys.csv");
            File.AppendAllText(logPath, $"{license.Id},{machine},{now:O},{license.ExpiresUtc?.ToString("O") ?? "LIFETIME"},{(isPartner ? "partner" : "customer")},{code}\r\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Key konnte nicht erstellt werden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Revoke_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = _revokeId.Text.Trim();
            if (id.Length != 32 || id.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Die Lizenz-ID muss aus 32 Hex-Zeichen bestehen.");
            if (!File.Exists(_privatePath)) throw new FileNotFoundException("owner-private.pem fehlt neben dem Eigentümer-Programm.", _privatePath);
            var revocationPath = Path.Combine(AppContext.BaseDirectory, "revoked-licenses.txt");
            var prior = File.Exists(revocationPath) && LicenseService.TryReadSignedFeed(File.ReadAllText(revocationPath), out var parsed)
                ? parsed?.RevokedIds : null;
            var ids = prior?.ToList() ?? new List<string>();
            if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase)) ids.Add(id);
            var feed = new RevocationList("Aetherion", DateTimeOffset.UtcNow, ids.Order(StringComparer.OrdinalIgnoreCase).ToArray());
            var signed = LicenseService.SignRevocations(feed, _privatePath);
            File.WriteAllText(revocationPath, signed);
            MessageBox.Show(this, "Key wurde in revoked-licenses.txt eingetragen. Lade diese Datei in das Stammverzeichnis deines MostWanted-DPS-Repositories hoch; Nutzer erhalten die Sperre beim nächsten erfolgreichen Online-Abgleich.", "Key gesperrt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Key konnte nicht gesperrt werden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private sealed record DurationChoice(string Name, int? Days) { public override string ToString() => Name; }
}
