using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

public sealed class ActivationWindow : Window
{
    private readonly TextBlock _accountStatus = new();
    private readonly TextBox _licenseKey = new();
    private readonly Button _discordButton;
    private readonly Button _redeemButton;

    public ActivationWindow(AccountLicenseStatus? existingStatus = null)
    {
        Title = "Aetherion — Anmeldung und Lizenz";
        Width = 560;
        Height = 430;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        ShowActivated = true;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(18, 21, 30));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        var panel = new StackPanel { Margin = new Thickness(26) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "AETHERION", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(236, 190, 93)) });
        panel.Children.Add(new TextBlock
        {
            Text = "Melde dich mit Discord an und löse deinen Key ein. Keys erhältst du nach dem Kauf über ein Ticket auf unserem Discord.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 14),
        });

        _discordButton = new Button
        {
            Content = "Mit Discord anmelden / Konto aktualisieren",
            Padding = new Thickness(12, 10, 12, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontWeight = FontWeights.SemiBold,
        };
        _discordButton.Click += DiscordButton_Click;
        panel.Children.Add(_discordButton);

        _accountStatus.TextWrapping = TextWrapping.Wrap;
        _accountStatus.Margin = new Thickness(0, 12, 0, 12);
        _accountStatus.Foreground = Brushes.LightGray;
        panel.Children.Add(_accountStatus);
        SetAccountStatus(existingStatus);

        panel.Children.Add(new TextBlock { Text = "Lizenz-Key", Margin = new Thickness(0, 0, 0, 6) });
        _licenseKey.Height = 38;
        _licenseKey.Padding = new Thickness(8);
        _licenseKey.ToolTip = "AETH-…";
        panel.Children.Add(_licenseKey);
        _redeemButton = new Button
        {
            Content = "Key einlösen",
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 130,
            Margin = new Thickness(0, 10, 0, 0),
        };
        _redeemButton.Click += RedeemButton_Click;
        panel.Children.Add(_redeemButton);

        var ticket = new Button { Content = "Key im Discord-Ticket kaufen / anfragen", Padding = new Thickness(8, 6, 8, 6), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        ticket.Click += (_, _) => Process.Start(new ProcessStartInfo("https://discord.gg/mostwantedde") { UseShellExecute = true });
        panel.Children.Add(ticket);
    }

    private async void DiscordButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Prüfe Discord-Konto …");
        try
        {
            var status = await SupabaseAccessService.CheckSavedSessionAsync();
            status ??= await SupabaseAccessService.SignInWithDiscordAsync();
            SetAccountStatus(status);
            if (status.HasActiveLicense) DialogResult = true;
        }
        catch (Exception ex)
        {
            _accountStatus.Text = "Anmeldung fehlgeschlagen: " + ex.Message;
            _accountStatus.Foreground = Brushes.Orange;
        }
        finally { SetBusy(false); }
    }

    private async void RedeemButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Prüfe Anmeldung und löse Key ein …");
        try
        {
            var status = await SupabaseAccessService.CheckSavedSessionAsync();
            status ??= await SupabaseAccessService.SignInWithDiscordAsync();
            if (string.IsNullOrWhiteSpace(_licenseKey.Text))
            {
                SetAccountStatus(status);
                _accountStatus.Text += " Gib anschließend deinen Lizenz-Key ein.";
                return;
            }

            status = await SupabaseAccessService.RedeemLicenseKeyAsync(_licenseKey.Text);
            _licenseKey.Clear();
            SetAccountStatus(status);
            if (status.HasActiveLicense) DialogResult = true;
        }
        catch (Exception ex)
        {
            _accountStatus.Text = ex.Message;
            _accountStatus.Foreground = Brushes.Orange;
        }
        finally { SetBusy(false); }
    }

    private void SetAccountStatus(AccountLicenseStatus? status)
    {
        if (status is null)
        {
            _accountStatus.Text = "Noch nicht mit Discord angemeldet.";
            _accountStatus.Foreground = Brushes.LightGray;
            return;
        }

        _accountStatus.Text = status.HasActiveLicense
            ? "Discord-Konto angemeldet · " + SupabaseAccessService.FormatRemaining(status.ExpiresAt)
            : "Discord-Konto angemeldet · " + SupabaseAccessService.FormatRemaining(status.ExpiresAt) + ". Kaufe oder verlängere deinen Zugang über ein Discord-Ticket.";
        _accountStatus.Foreground = status.HasActiveLicense ? Brushes.LightGreen : Brushes.LightGoldenrodYellow;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _discordButton.IsEnabled = !busy;
        _redeemButton.IsEnabled = !busy;
        if (message is not null)
        {
            _accountStatus.Text = message;
            _accountStatus.Foreground = Brushes.LightGray;
        }
    }
}
