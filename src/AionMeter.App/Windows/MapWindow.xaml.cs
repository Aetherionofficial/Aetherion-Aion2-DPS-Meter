using System.Diagnostics;
using System.Windows;
using AionMeter.App.Services;
using Microsoft.Web.WebView2.Core;

namespace AionMeter.App.Windows;

/// <summary>AION 2 guides and map, displayed inside Aetherion using the system WebView2 runtime.</summary>
public partial class MapWindow : Window
{
    private const string PortalUrl = "https://wakayashi.gg/aion2";
    private const string MapUrl = "https://questlog.gg/aion-2/de/map";
    public MapWindow()
    {
        InitializeComponent();
        Controls.DragAnywhere.Attach(this);
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
            Browser.Source = new Uri(PortalUrl);
        }
        catch (Exception ex)
        {
            RuntimeNotice.Visibility = Visibility.Visible;
            RuntimeMessage.Text = "Die AION2-Seite konnte nicht eingebettet geladen werden. Prüfe deine Internetverbindung oder installiere die Microsoft Edge WebView2 Runtime. " + ex.Message;
            Log.Error("Could not initialize the embedded AION2 hub", ex);
        }
    }

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

    private void Portal_Click(object sender, RoutedEventArgs e) => Browser.Source = new Uri(PortalUrl);

    private void Map_Click(object sender, RoutedEventArgs e) => Browser.Source = new Uri(MapUrl);

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Browser.Source?.AbsoluteUri ?? PortalUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Error("Could not open the AION2 map in the browser", ex);
        }
    }
}
