using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AionMeter.Core;
using AionMeter.Core.Combat;

namespace AionMeter.App.Services;

/// <summary>
/// Copies a Discord table from the overlay or breakdown. In-game chat formats are intentionally not exposed.
/// </summary>
public static class ChatCopy
{
    private static UiText T => UiText.Current;

    /// <summary>The menu entries; <paramref name="fight"/> is read when one is clicked, so a live fight copies its latest numbers.</summary>
    public static IEnumerable<MenuItem> Items(FrameworkElement noteAnchor, Func<EncounterSnapshot?> fight)
    {
        yield return Item(T.CopyTable, s => ChatLine.Table(s, T.Chat, Name));

        MenuItem Item(string header, Func<EncounterSnapshot, string> text)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                if (fight() is { } s) Copy(noteAnchor, text(PrivacyNames.Apply(s with { Title = T.FightTitle(s) })!));
            };
            return item;
        }
    }

    /// <summary>A small menu of <see cref="Items"/> under <paramref name="button"/>.</summary>
    public static void ShowMenu(FrameworkElement button, FrameworkElement noteAnchor, Func<EncounterSnapshot?> fight)
    {
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        foreach (var item in Items(noteAnchor, fight)) menu.Items.Add(item);
        menu.IsOpen = true;
    }

    /// <summary>Unnamed players read as "Templar #10388", never as a bare id.</summary>
    private static string Name(CombatantSnapshot c) => T.CombatantLabel(c.ActorId, c.Name, c.Class);

    private static void Copy(FrameworkElement anchor, string text) => ShowNote(anchor, AppHost.CopyText(text) ? T.Copied : T.CopyFailed);

    /// <summary>A note over <paramref name="anchor"/> that goes away by itself.</summary>
    private static void ShowNote(FrameworkElement anchor, string text)
    {
        var popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Center,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            StaysOpen = true,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x0A, 0x0B, 0x0D)),
                BorderBrush = (Brush)Application.Current.FindResource("Gold"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 6, 12, 7),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 12.5,
                    FontFamily = (FontFamily)Application.Current.FindResource("UiFont"),
                },
            },
        };
        popup.IsOpen = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            popup.IsOpen = false;
        };
        timer.Start();
    }
}
