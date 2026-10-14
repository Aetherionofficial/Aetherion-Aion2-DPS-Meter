using System.Windows;
using AionMeter.App.Services;
using AionMeter.Core;
using AionMeter.Core.Combat;
using AionMeter.Core.Storage;

namespace AionMeter.App.Windows;

public partial class FightComparisonWindow : Window
{
    private enum ValueKind { Number, Duration }
    private sealed record MetricRow(string Metric, string First, string Second, string Change);

    public FightComparisonWindow(MeterService meter, HistoryEntry first, FightRecord firstFight, HistoryEntry second, FightRecord secondFight)
    {
        InitializeComponent();
        Controls.DragAnywhere.Attach(this);
        var culture = UiText.Current.Culture;
        FirstTitle.Text = meter.DisplayTitle(first);
        SecondTitle.Text = meter.DisplayTitle(second);
        FirstMeta.Text = first.StartedAt.ToString("dd MMM yyyy HH:mm", culture);
        SecondMeta.Text = second.StartedAt.ToString("dd MMM yyyy HH:mm", culture);

        var s1 = firstFight.Summary;
        var s2 = secondFight.Summary;
        var h1 = firstFight.Details.Sum(d => d.HealingTotal);
        var h2 = secondFight.Details.Sum(d => d.HealingTotal);
        ComparisonRows.ItemsSource = new[]
        {
            Row(UiText.Current.CompareDamage, s1.TotalDamage, s2.TotalDamage, ValueKind.Number),
            Row(UiText.Current.CompareDps, s1.PartyDps, s2.PartyDps, ValueKind.Number),
            Row(UiText.Current.CompareDuration, s1.CombatMs, s2.CombatMs, ValueKind.Duration),
            Row(UiText.Current.CompareHealing, h1, h2, ValueKind.Number),
        };
    }

    private static MetricRow Row(string label, double a, double b, ValueKind kind)
    {
        var delta = b - a;
        var sign = delta > 0 ? "+" : delta < 0 ? "−" : "";
        var absolute = Math.Abs(delta);
        var amount = kind == ValueKind.Duration
            ? Format.ClockPrecise((long)Math.Round(absolute))
            : Format.Compact((long)Math.Round(absolute));
        var percent = a == 0 ? "" : $" ({(delta / Math.Abs(a)).ToString("+0.0%;−0.0%;0.0%", UiText.Current.Culture)})";
        return new MetricRow(label, Value(a, kind), Value(b, kind), delta == 0 ? "—" : sign + amount + percent);
    }

    private static string Value(double value, ValueKind kind) => kind == ValueKind.Duration
        ? Format.ClockPrecise((long)Math.Round(value))
        : Format.Compact((long)Math.Round(value));

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
