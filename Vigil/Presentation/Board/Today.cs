using System.Globalization;

namespace Vigil.Presentation;

/// <summary>"Today's patients" segmented filter (docs/design/today-dashboard-values.md §2.3).</summary>
public enum CardFilter { InClinic, SignedOut, All }

/// <summary>Dot and fill colour of a phase-coloured mark; the templates switch on these flags (no brushes in records).</summary>
public interface IPhaseTone
{
    bool IsPrep { get; }
    bool IsAnesthesia { get; }
    bool IsRecovery { get; }
    bool IsWaiting { get; }
}

/// <summary>Today view-model building: pure functions of the cases and the time, unit-tested in TodayViewTests.</summary>
public static class Today
{
    /// <summary>Cases that belong on today's board: everyone still in the clinic, plus whoever went home today.</summary>
    public static bool IsToday(Case c, DateTimeOffset now) =>
        Stage.Of(c) != CareStage.Home || HomeAt(c)?.ToOffset(now.Offset).Date == now.Date;

    public static DateTimeOffset? HomeAt(Case c) => c.SignedAt ?? c.DischargedAt;

    public static bool Matches(Case c, CardFilter filter, DateTimeOffset now) => filter switch
    {
        CardFilter.InClinic => Stage.Of(c) != CareStage.Home,
        CardFilter.SignedOut => Stage.Of(c) == CareStage.Home && IsToday(c, now),
        _ => IsToday(c, now),
    };

    /// <summary>Card order: surgery, recovery, prep, booked (by planned start), home.</summary>
    public static int Rank(Case c) => Stage.Of(c) switch
    {
        CareStage.Surgery => 0,
        CareStage.Recovery => 1,
        CareStage.Prep => 2,
        CareStage.Booked => 3,
        _ => 4,
    };

    /// <summary>"1:12": hours and minutes, no leading zero (the card strip and hero timer).</summary>
    public static string Clock(TimeSpan t)
    {
        t = t < TimeSpan.Zero ? TimeSpan.Zero : t;
        return $"{(int)t.TotalHours}:{t.Minutes:00}";
    }

    public static string Time(DateTimeOffset t, DateTimeOffset now) => t.ToOffset(now.Offset).ToString("HH:mm", CultureInfo.CurrentCulture);

    public static ImmutableList<FilterTab> Tabs(IEnumerable<Case> cases, CardFilter selected, DateTimeOffset now)
    {
        var list = cases.ToList();
        FilterTab Tab(CardFilter f, string label) => new(f, label, list.Count(c => Matches(c, f, now)), f == selected);
        return
        [
            Tab(CardFilter.InClinic, Loc.T("Today_FilterInClinic", "In clinic")),
            Tab(CardFilter.SignedOut, Loc.T("Today_FilterSignedOut", "Signed out")),
            Tab(CardFilter.All, Loc.T("Today_FilterAll", "All")),
        ];
    }

    public static ImmutableList<PatientCard> Cards(IEnumerable<Case> cases, CardFilter filter, IImmutableSet<Guid> expanded, DateTimeOffset now, bool reduceMotion) =>
        cases.Where(c => Matches(c, filter, now))
            .OrderBy(Rank)
            .ThenBy(c => c.PlannedStart ?? c.CreatedAt)
            .Select(c => PatientCard.From(c, now, expanded.Contains(c.Id), reduceMotion))
            .ToImmutableList();

    /// <summary>
    /// A recorded-heart-rate sparkline in a 72 x 20 box (2 px inset): the last <paramref name="count"/> readings, scaled
    /// between their own minimum and maximum. Empty with fewer than two readings. SPEC-v2 decision 3: recorded
    /// readings, never a decorative ECG.
    /// </summary>
    public static string Sparkline(IEnumerable<VitalsReading> readings, int count = 12, double width = 72, double height = 20)
    {
        var hr = readings.Where(r => r.Hr is not null).Select(r => (double)r.Hr!.Value).TakeLast(count).ToList();
        if (hr.Count < 2)
        {
            return "";
        }
        var min = hr.Min();
        var max = hr.Max();
        const double inset = 2;
        var points = hr.Select((v, i) =>
        {
            var x = i * width / (hr.Count - 1);
            var y = max == min ? height / 2 : inset + (max - v) / (max - min) * (height - 2 * inset);
            return string.Create(CultureInfo.InvariantCulture, $"{x:0.##},{y:0.##}");
        });
        return "M" + string.Join(" L", points);
    }
}

public partial record FilterTab(CardFilter Filter, string Label, int Count, bool IsSelected)
{
    public bool IsUnselected => !IsSelected;

    public string AccessibleName => $"{Label}, {Count}";
}
