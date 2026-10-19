using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AionMeter.Core.Events;

namespace AionMeter.App.Controls;

public static class ClassVisuals
{
    // Bar colours sampled from the in-game class emblems, brightened to read well as fills.
    private static readonly Dictionary<GameClass, Color> Colors = new()
    {
        [GameClass.Gladiator] = Color.FromRgb(0xF2, 0x8A, 0x45),
        [GameClass.Templar] = Color.FromRgb(0xFF, 0xB0, 0x6A),
        [GameClass.Ranger] = Color.FromRgb(0xFF, 0x8F, 0x4A),
        [GameClass.Assassin] = Color.FromRgb(0xD6, 0x6A, 0x2A),
        [GameClass.Sorcerer] = Color.FromRgb(0xC8, 0x54, 0x1E),
        [GameClass.Elementalist] = Color.FromRgb(0xF6, 0xC0, 0x9A),
        [GameClass.Cleric] = Color.FromRgb(0xFF, 0xC0, 0x74),
        [GameClass.Chanter] = Color.FromRgb(0xE4, 0x7A, 0x35),
        [GameClass.Brawler] = Color.FromRgb(0xB8, 0x4B, 0x1B),
        [GameClass.Unknown] = Color.FromRgb(0x94, 0xA3, 0xB8),
    };

    private static readonly Dictionary<GameClass, ImageSource?> Emblems = new();

    // A neutral player silhouette avoids suggesting an unknown class is literally a question mark.
    private static readonly Geometry UnknownGlyph = Freeze(Geometry.Parse(
        "M12 3.5 A4 4 0 1 0 12.01 3.5 Z M4 21 C4.5 16.5 7.5 14.5 12 14.5 C16.5 14.5 19.5 16.5 20 21"));

    // Saturated fills for the full-height damage bars (the emblem hues, pushed so a whole row reads at a glance).
    private static readonly Dictionary<GameClass, Color> BarColors = new()
    {
        [GameClass.Gladiator] = Color.FromRgb(0xD6, 0x6A, 0x2A),
        [GameClass.Templar] = Color.FromRgb(0xE4, 0x7A, 0x35),
        [GameClass.Ranger] = Color.FromRgb(0xFF, 0x8F, 0x4A),
        [GameClass.Assassin] = Color.FromRgb(0xB8, 0x4B, 0x1B),
        [GameClass.Sorcerer] = Color.FromRgb(0xC8, 0x54, 0x1E),
        [GameClass.Elementalist] = Color.FromRgb(0xF2, 0x8A, 0x45),
        [GameClass.Cleric] = Color.FromRgb(0xFF, 0xA0, 0x58),
        [GameClass.Chanter] = Color.FromRgb(0xE4, 0x7A, 0x35),
        [GameClass.Brawler] = Color.FromRgb(0x9E, 0x3D, 0x16),
        [GameClass.Unknown] = Color.FromRgb(0x5E, 0x6B, 0x84),
    };

    public static Color ColorOf(GameClass c) => Colors.TryGetValue(c, out var col) ? col : Colors[GameClass.Unknown];

    public static Color BarColorOf(GameClass c) => BarColors.TryGetValue(c, out var col) ? col : BarColors[GameClass.Unknown];

    public static string NameOf(GameClass c) => c == GameClass.Unknown ? "Unknown" : c.ToString();

    /// <summary>The official class emblem (Assets/Classes/&lt;class&gt;.png), or null for an unknown class.</summary>
    public static ImageSource? EmblemOf(GameClass c)
    {
        if (c == GameClass.Unknown) return null;
        lock (Emblems)
        {
            if (Emblems.TryGetValue(c, out var img)) return img;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri($"pack://application:,,,/Assets/Classes/{c.ToString().ToLowerInvariant()}.png");
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 96;
                bmp.EndInit();
                bmp.Freeze();
                img = bmp;
            }
            catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException)
            {
                img = null;
            }
            Emblems[c] = img;
            return img;
        }
    }

    public static Geometry UnknownGeometry => UnknownGlyph;

    private static Geometry Freeze(Geometry g)
    {
        g.Freeze();
        return g;
    }
}

/// <summary>Draws the official class emblem. Size it with Width/Height.</summary>
public sealed class ClassGlyph : FrameworkElement
{
    public static readonly DependencyProperty ClassProperty = DependencyProperty.Register(
        nameof(Class), typeof(GameClass), typeof(ClassGlyph),
        new FrameworkPropertyMetadata(GameClass.Unknown, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowProperty = DependencyProperty.Register(
        nameof(Glow), typeof(bool), typeof(ClassGlyph),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OnBarProperty = DependencyProperty.Register(
        nameof(OnBar), typeof(bool), typeof(ClassGlyph),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public GameClass Class
    {
        get => (GameClass)GetValue(ClassProperty);
        set => SetValue(ClassProperty, value);
    }

    /// <summary>Soft class-coloured disc behind the emblem (headers).</summary>
    public bool Glow
    {
        get => (bool)GetValue(GlowProperty);
        set => SetValue(GlowProperty, value);
    }

    /// <summary>Dark backdrop so the emblem stays readable on a bar of the same colour.</summary>
    public bool OnBar
    {
        get => (bool)GetValue(OnBarProperty);
        set => SetValue(OnBarProperty, value);
    }

    private static readonly Brush BarDisc = Frozen(new SolidColorBrush(Color.FromArgb(0xB4, 0x07, 0x09, 0x10)));
    private static readonly Pen BarRing = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), 1));

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    public ClassGlyph()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        var color = ClassVisuals.ColorOf(Class);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        if (Glow)
        {
            var disc = new RadialGradientBrush(Color.FromArgb(0x55, color.R, color.G, color.B), Color.FromArgb(0x10, color.R, color.G, color.B));
            dc.DrawEllipse(disc, new Pen(new SolidColorBrush(Color.FromArgb(0x90, color.R, color.G, color.B)), 1), center, size / 2, size / 2);
        }
        else if (OnBar)
        {
            // On a bar of the same hue: the real emblem sits on a dark coin with a faint rim, so it never blends in.
            dc.DrawEllipse(BarDisc, BarRing, center, size / 2, size / 2);
        }

        var inner = Glow ? size * 0.78 : OnBar ? size * 0.84 : size;
        var emblem = ClassVisuals.EmblemOf(Class);
        if (emblem is not null)
        {
            dc.DrawImage(emblem, new Rect(center.X - inner / 2, center.Y - inner / 2, inner, inner));
            return;
        }

        var scale = inner / 24.0;
        dc.PushTransform(new TranslateTransform(center.X - 12 * scale, center.Y - 12 * scale));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var pen = new Pen(new SolidColorBrush(color), 2.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, ClassVisuals.UnknownGeometry);
        dc.Pop();
        dc.Pop();
    }
}
