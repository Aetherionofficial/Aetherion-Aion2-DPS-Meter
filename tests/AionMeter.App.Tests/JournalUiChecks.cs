using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AionMeter.App.Services;
using AionMeter.App.Windows;
using AionMeter.Core.Storage;

namespace AionMeter.App.Tests;

internal static class JournalUiChecks
{
    public static void Run(MeterService meter)
    {
        var preview = JournalPreview.Create(meter.Tracker.Record(null)!);
        var window = new FightJournalWindow(meter, preview[0].Entry, preview);
        try
        {
            var list = Find<ListBox>(window).Single();
            Assert.Equal(4, list.Items.Count);
            var favorite = Find<CheckBox>(window).Single(c => Equals(c.Content, "Als Favorit behalten"));
            var notes = Find<TextBox>(window).Single(t => t.Height == 125);
            var target = Find<TextBox>(window).Single(t => t.Width == 180);
            favorite.IsChecked = true; notes.Text = "Neue Ausrüstung getestet"; target.Text = "50.000";
            var save = Find<Button>(window).Single(b => Equals(b.Content, "Notiz, Favorit & Ziel speichern"));
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(meter.Journal.Annotation(preview[0].Entry.FileName).Favorite);
            Assert.Null(meter.Journal.Target(TrainingScope.For(preview[0].Record)));
            target.Text = "50000";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(meter.Journal.Annotation(preview[0].Entry.FileName).Favorite);
            Assert.Equal("Neue Ausrüstung getestet", meter.Journal.Annotation(preview[0].Entry.FileName).Note);
            Assert.Equal(50000, meter.Journal.Target(TrainingScope.For(preview[0].Record)));
            notes.Text = "Noch ungespeicherte Notiz";
            PrivacyNames.Enabled = true;
            Assert.Equal("", notes.Text);
            PrivacyNames.Enabled = false;
            Assert.Equal("Noch ungespeicherte Notiz", notes.Text);
            notes.Text = "Neue Ausrüstung getestet";
            Find<CheckBox>(window).Single(c => Equals(c.Content, "Nur Favoriten")).IsChecked = true;
            Assert.Single(list.Items.Cast<object>());
            Find<TextBox>(window).Single(t => t.Width == 230).Text = "Keine passenden Bosskämpfe";
            Assert.Empty(list.Items.Cast<object>());
        }
        finally { window.Close(); }

        var card = new DiscordCardWindow(preview[0].Record);
        var path = Path.Combine(Path.GetTempPath(), "Aetherion-card-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var anonymous = Find<CheckBox>(card).Single(); anonymous.IsChecked = false;
            PrivacyNames.Enabled = true;
            Assert.True(anonymous.IsChecked); Assert.False(anonymous.IsEnabled);
            var image = Find<Image>(card).Single(); var bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
            Assert.Equal(1080, bitmap.PixelWidth); Assert.InRange(bitmap.PixelHeight, 480, 1440);
            DiscordCardRenderer.Save(bitmap, path);
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Assert.Equal(1080, decoder.Frames[0].PixelWidth);
            var bytes = File.ReadAllBytes(path);
            for (var offset = 8; offset + 12 <= bytes.Length;)
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
                var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                Assert.DoesNotContain(type, new[] { "tEXt", "iTXt", "zTXt" });
                offset += 12 + size;
            }
        }
        finally { card.Close(); PrivacyNames.Enabled = false; if (File.Exists(path)) File.Delete(path); }
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var match in Find<T>(child)) yield return match;
    }
}
