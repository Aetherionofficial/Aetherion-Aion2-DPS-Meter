using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AionMeter.App.Controls;

/// <summary>Animates only the visual width, never the measured combat values.</summary>
public sealed class AnimatedBar : Border
{
    public static readonly DependencyProperty TargetWidthProperty = DependencyProperty.Register(
        nameof(TargetWidth), typeof(double), typeof(AnimatedBar), new PropertyMetadata(0d, Changed));

    public double TargetWidth { get => (double)GetValue(TargetWidthProperty); set => SetValue(TargetWidthProperty, value); }

    private static void Changed(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        var bar = (AnimatedBar)source;
        var target = args.NewValue is double value && double.IsFinite(value) ? Math.Max(0, value) : 0;
        var current = double.IsFinite(bar.Width) ? bar.Width : 0;
        bar.BeginAnimation(WidthProperty, null);
        bar.SetCurrentValue(WidthProperty, target);
        if (!bar.IsLoaded || (double)args.OldValue <= 0 || current == target) return;
        bar.BeginAnimation(WidthProperty, new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
    }
}
