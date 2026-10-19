using System.Diagnostics;
using System.Windows;
using AionMeter.App.Services;
using Microsoft.Web.WebView2.Core;

namespace AionMeter.App.Windows;

/// <summary>AION 2 guides and map, displayed inside Aetherion using the system WebView2 runtime.</summary>
public partial class MapWindow : Window
{
    private const string MapUrl = "https://daevaos.com/maps";
    private const string OfficialNewsUrl = "https://aion2.plaync.com/en-us/board/update/list";
    private string? _focusArea;
    private string? _focusMarker;

    public MapWindow(string? focusArea = null, string? focusMarker = null)
    {
        _focusArea = focusArea;
        _focusMarker = focusMarker;
        InitializeComponent();
        Controls.DragAnywhere.Attach(this);
    }

    public void NavigateToArea(string? area, string? marker = null)
    {
        _focusArea = area;
        _focusMarker = marker;
        if (Browser.CoreWebView2 is { } core)
            core.Navigate(BuildMapUrl(area, marker));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Browser.CoreWebView2.Navigate(args.Uri);
            };
            Browser.CoreWebView2.NavigationCompleted += (_, _) => UpdateNavigationButtons();
            Browser.CoreWebView2.HistoryChanged += (_, _) => UpdateNavigationButtons();
            Browser.Source = new Uri(BuildMapUrl(_focusArea, _focusMarker));
            if (!string.IsNullOrWhiteSpace(_focusArea) || !string.IsNullOrWhiteSpace(_focusMarker))
            {
                RuntimeNotice.Visibility = Visibility.Visible;
                RuntimeMessage.Text = $"Pet-Fundort ausgewählt: {_focusMarker ?? "Pet-Marker"} · {_focusArea ?? "Gebiet"}. Die AION2-Karte wurde direkt geöffnet.";
            }

            // The official NC updates board is the source of truth. Keeping it live in its
            // own pane means newly published patch notes appear without shipping app updates.
            await NewsBrowser.EnsureCoreWebView2Async();
            NewsBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            NewsBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            NewsBrowser.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                NewsBrowser.CoreWebView2.Navigate(args.Uri);
            };
            NewsBrowser.Source = new Uri(OfficialNewsUrl);
        }
        catch (Exception ex)
        {
            RuntimeNotice.Visibility = Visibility.Visible;
            RuntimeMessage.Text = "Die Questlog-Karte oder das offizielle AION2-Updates-Board konnte nicht eingebettet geladen werden. Prüfe deine Internetverbindung oder installiere die Microsoft Edge WebView2 Runtime. " + ex.Message;
            Log.Error("Could not initialize the embedded AION2 map and news hub", ex);
        }
    }

    private static string BuildMapUrl(string? area, string? marker) => string.IsNullOrWhiteSpace(area) && string.IsNullOrWhiteSpace(marker)
        ? MapUrl
        : $"{MapUrl}?search={Uri.EscapeDataString(marker ?? area ?? string.Empty)}";

    private void UpdateNavigationButtons()
    {
        if (Browser.CoreWebView2 is not { } core) return;
        BackButton.IsEnabled = core.CanGoBack;
        ForwardButton.IsEnabled = core.CanGoForward;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true) Browser.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoForward == true) Browser.CoreWebView2.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => Browser.Reload();

    private void OpenWakayashi_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowWakayashi();

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Browser.Source?.AbsoluteUri ?? MapUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Error("Could not open the AION2 map in the browser", ex);
        }
    }

    private void OpenNewsBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(OfficialNewsUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Error("Could not open the official AION2 update board", ex);
        }
    }
}
