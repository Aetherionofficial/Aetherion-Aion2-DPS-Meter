using System.Windows;
using AionMeter.App.Services;
using AionMeter.Core;
using AionMeter.Core.Combat;
using AionMeter.Core.Storage;

namespace AionMeter.App.Windows;

public partial class FightComparisonWindow : Window
{
    private enum ValueKind { Number, Duration, Percent }
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
        var rows = new List<MetricRow>
        {
            Row(UiText.Current.CompareDamage, s1.TotalDamage, s2.TotalDamage, ValueKind.Number),
            Row(UiText.Current.CompareDps, s1.PartyDps, s2.PartyDps, ValueKind.Number),
            Row(UiText.Current.CompareDuration, s1.CombatMs, s2.CombatMs, ValueKind.Duration),
            Row(UiText.Current.CompareHealing, h1, h2, ValueKind.Number),
        };
        var a = firstFight.Details.FirstOrDefault(d => d.IsSelf);
        var b = secondFight.Details.FirstOrDefault(d => d.IsSelf);
        if (a is not null && b is not null && a.Name == b.Name && a.ServerId == b.ServerId && a.Class == b.Class)
        {
            rows.Add(Row("Eigene DPS", a.Dps, b.Dps, ValueKind.Number));
            rows.Add(a.Quality.CritRate is { } firstCrit && b.Quality.CritRate is { } secondCrit
                ? Row("Eigene kritische Treffer", firstCrit, secondCrit, ValueKind.Percent)
                : new MetricRow("Eigene kritische Treffer", a.Quality.CritRate?.ToString("P1", culture) ?? "—",
                    b.Quality.CritRate?.ToString("P1", culture) ?? "—", "Keine vergleichbaren Trefferdaten"));
            rows.Add(Row("Eigene Todesmeldungen", a.Deaths?.Count ?? 0, b.Deaths?.Count ?? 0, ValueKind.Number));
            var x = FightInsights.Activity(a.PerSecond, s1.CombatMs);
            var y = FightInsights.Activity(b.PerSecond, s2.CombatMs);
            rows.Add(Row("Sekunden mit empfangenem Schaden", x.DamageSeconds, y.DamageSeconds, ValueKind.Number));
            rows.Add(Row("Längste Folge ohne Schaden (s)", x.LongestQuietSeconds, y.LongestQuietSeconds, ValueKind.Number));
            foreach (var code in a.Skills.Concat(b.Skills).GroupBy(skill => skill.SkillCode)
                .OrderByDescending(group => group.Sum(skill => skill.Damage)).Take(15).Select(group => group.Key))
            {
                var left = a.Skills.FirstOrDefault(skill => skill.SkillCode == code);
                var right = b.Skills.FirstOrDefault(skill => skill.SkillCode == code);
                rows.Add(Row("Skill-Anteil · " + (right?.Name ?? left!.Name), left?.Share ?? 0, right?.Share ?? 0, ValueKind.Percent));
            }
        }
        if (s1.MostDamageUnsent || s2.MostDamageUnsent || s1.Boss?.CountedFrom is not null || s2.Boss?.CountedFrom is not null)
            rows.Insert(0, new MetricRow("Datenqualität", "Erfasste Ereignisse", "Erfasste Ereignisse", "Erfassung teilweise unvollständig"));
        ComparisonRows.ItemsSource = rows;
    }

    private static MetricRow Row(string label, double a, double b, ValueKind kind)
    {
        var delta = b - a;
        var sign = delta > 0 ? "+" : delta < 0 ? "−" : "";
        var absolute = Math.Abs(delta);
        var amount = kind == ValueKind.Percent ? absolute.ToString("P1", UiText.Current.Culture) : kind == ValueKind.Duration
            ? Format.ClockPrecise((long)Math.Round(absolute))
            : Format.Compact((long)Math.Round(absolute));
        var percent = a == 0 ? "" : $" ({(delta / Math.Abs(a)).ToString("+0.0%;−0.0%;0.0%", UiText.Current.Culture)})";
        return new MetricRow(label, Value(a, kind), Value(b, kind), delta == 0 ? "—" : sign + amount + percent);
    }

    private static string Value(double value, ValueKind kind) => kind == ValueKind.Percent ? value.ToString("P1", UiText.Current.Culture) : kind == ValueKind.Duration
        ? Format.ClockPrecise((long)Math.Round(value))
        : Format.Compact((long)Math.Round(value));

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
