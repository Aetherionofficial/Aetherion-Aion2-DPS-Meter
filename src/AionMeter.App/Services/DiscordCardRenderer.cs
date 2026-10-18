using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AionMeter.Core;
using AionMeter.Core.Storage;

namespace AionMeter.App.Services;

/// <summary>Local PNG rendering. No clipboard, chat automation, network requests or player identifiers in PNG metadata.</summary>
public static class DiscordCardRenderer
{
    public static BitmapSource Render(DiscordCardPage page)
    {
        const int width = 1080;
        var height = 480 + Math.Max(1, page.Rows.Count) * 64;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(B("#0B0D12"), null, new Rect(0, 0, width, height));
            dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(48, 27, 21), Color.FromRgb(16, 20, 29), 30),
                new Pen(B("#49342E"), 1), new Rect(22, 22, width - 44, height - 44), 22, 22);
            dc.DrawRectangle(B("#FF823F"), null, new Rect(44, 50, 5, 54));
            Text(dc, "AETHERION", 66, 43, 25, "#FF9C60", true);
            Text(dc, "AION 2  /  KAMPFERGEBNIS", 66, 78, 13, "#B4BAC7");
            Text(dc, page.Result, 820, 51, 19, "#74D7AF", true, 190);
            Text(dc, page.Boss, 48, 128, 36, "#F6F4F2", true, 980, 60);
            Text(dc, $"Kampfdauer {Format.Clock(page.DurationMs)}    ·    {page.Players} erfasste Spieler    ·    Seite {page.Page}/{page.Pages}", 48, 184, 18, "#BAC0CD");
            Text(dc, "RANG / SPIELER", 48, 230, 13, "#858FA0", true);
            Text(dc, "SCHADEN", 669, 230, 13, "#858FA0", true);
            Text(dc, "DPS", 817, 230, 13, "#858FA0", true);
            Text(dc, "ANTEIL", 944, 230, 13, "#858FA0", true);
            var max = page.Rows.Select(r => r.Damage).DefaultIfEmpty(1).Max();
            for (var i = 0; i < page.Rows.Count; i++)
            {
                var row = page.Rows[i]; var y = 263 + i * 64;
                dc.DrawRoundedRectangle(B(i % 2 == 0 ? "#1B202B" : "#151A23"), null, new Rect(44, y, 992, 56), 8, 8);
                var ratio = max <= 0 ? 0 : row.Damage / (double)max;
                dc.DrawRoundedRectangle(B(row.IsSelf ? "#553221" : "#31251F"), null, new Rect(44, y, 992 * ratio, 56), 8, 8);
                Text(dc, row.Rank.ToString(CultureInfo.InvariantCulture), 57, y + 16, 18, row.Rank <= 3 ? "#FFAF73" : "#B6BECC", true);
                Text(dc, row.Name, 109, y + 6, 20, "#FFFFFF", row.IsSelf, 495);
                Text(dc, row.Class + (row.IsSelf ? "  ·  DU" : ""), 109, y + 33, 12, "#A8B1C1", false, 495);
                Text(dc, Format.Compact(row.Damage), 669, y + 16, 19, "#E0E5ED", false, 139);
                Text(dc, Format.Compact(row.Dps) + "/s", 817, y + 16, 19, "#FFFFFF", true, 121);
                Text(dc, row.Share.ToString("0.0%", CultureInfo.GetCultureInfo("de-DE")), 944, y + 16, 17, "#CDD4E0", false, 86);
            }
            if (page.Rows.Count == 0) Text(dc, "Keine empfangenen Spielerdaten", 52, 280, 20, "#BAC0CD");
            var footer = 285 + Math.Max(1, page.Rows.Count) * 64;
            Text(dc, page.Quality, 48, footer, 18, "#FFB078", true);
            Text(dc, page.QualityExplanation, 48, footer + 31, 14, "#ABB4C3", false, 980, 64);
            Text(dc, page.Anonymous ? "NAMEN ANONYMISIERT · nur erfasste Werte" : "Rangliste und Anteile beziehen sich auf erfasste Werte", 48, height - 72, 13, "#8390A4");
            Text(dc, "aetherionofficial.github.io", 750, height - 72, 13, "#A8B1C1", false, 280);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }

    public static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { using (var stream = File.Create(temp)) encoder.Save(stream); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static SolidColorBrush B(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static void Text(DrawingContext dc, string text, double x, double y, double size, string color, bool bold = false,
        double maxWidth = 1000, double maxHeight = 46)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, B(color), 1)
        { MaxTextWidth = maxWidth, MaxTextHeight = maxHeight, Trimming = TextTrimming.CharacterEllipsis };
        dc.DrawText(formatted, new Point(x, y));
    }
}
