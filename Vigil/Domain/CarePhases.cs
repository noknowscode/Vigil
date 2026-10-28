using System.Globalization;

namespace Vigil.Domain;

public enum CarePhaseKind { Waiting, Prep, Anesthesia, Recovery, Planned }

/// <summary>
/// One phase of a case on the care timeline. A <see cref="CarePhaseKind.Planned"/> span is a future slot;
/// <see cref="Plans"/> says which phase it plans (Prep or Anesthesia).
/// </summary>
public partial record PhaseSpan(Guid CaseId, string PatientName, CarePhaseKind Phase, DateTimeOffset Start, DateTimeOffset End, bool Ongoing)
{
    public CarePhaseKind? Plans { get; init; }

    public TimeSpan Duration => End - Start;

    public bool IsPlanned => Phase == CarePhaseKind.Planned;

    /// <summary>"Prep", "Anesthesia", "Planned prep", "Planned surgery".</summary>
    public string Label => Phase != CarePhaseKind.Planned
        ? CarePhases.Label(Phase)
        : Plans == CarePhaseKind.Prep ? Loc.T("Phase_PlannedPrep", "Planned prep") : Loc.T("Phase_PlannedSurgery", "Planned surgery");

    /// <summary>"Pepper · Prep 08:00–08:32", "Milo · Recovery 12:08–now".</summary>
    public string Tooltip => $"{PatientName} · {Label} {Start:HH:mm}–{(Ongoing ? Loc.T("Phase_Now", "now") : End.ToString("HH:mm", CultureInfo.CurrentCulture))}";
}

public partial record CareTimeRow(CarePhaseKind Phase, TimeSpan Duration, double Fraction)
{
    public string Label => CarePhases.Label(Phase);

    public int Percent => (int)Math.Round(Fraction * 100, MidpointRounding.AwayFromZero);

    public string DurationText => CarePhases.HoursMinutes(Duration);

    public string PercentText => $"{Percent}%";

    /// <summary>"Anesthesia: 4h 13m, 58% of care time".</summary>
    public string Tooltip => Loc.F("CareTime_Tooltip", "{0}: {1}, {2}% of care time", Label, DurationText, Percent);
}

/// <summary>"Where care time went": today's recorded time per phase across every case.</summary>
public partial record CareTotals(TimeSpan Total, int PatientCount, int ProcedureCount, DateTimeOffset Now, IImmutableList<CareTimeRow> Rows)
{
    public string TotalText => CarePhases.HoursMinutes(Total);

    /// <summary>"7h 16m across 4 patients today".</summary>
    public string Summary => PatientCount == 1
        ? Loc.F("CareTime_SummaryOne", "{0} across 1 patient today", TotalText)
        : Loc.F("CareTime_Summary", "{0} across {1} patients today", TotalText, PatientCount);

    /// <summary>"4 patients · 3 procedures · Wed 28 Oct".</summary>
    public string DayLine =>
        $"{(PatientCount == 1 ? Loc.T("CareTime_OnePatient", "1 patient") : Loc.F("CareTime_Patients", "{0} patients", PatientCount))} · " +
        $"{(ProcedureCount == 1 ? Loc.T("CareTime_OneProcedure", "1 procedure") : Loc.F("CareTime_Procedures", "{0} procedures", ProcedureCount))} · " +
        Now.ToString("ddd d MMM", CultureInfo.CurrentCulture);
}

public static class CarePhases
{
    /// <summary>Length of a planned surgery slot when the case has no <see cref="Case.PlannedMinutes"/>.</summary>
    public const int DefaultPlannedMinutes = 60;

    /// <summary>
    /// Ordered spans for one case: Waiting (booking → prep, induction or now), Prep (prep start → induction or now),
    /// Anesthesia (induction → end or now), Recovery (end → sign-off/discharge or now), and, for a booked case whose
    /// planned start is still ahead, Planned prep and Planned surgery. Times are in <paramref name="now"/>'s offset.
    /// </summary>
    public static ImmutableList<PhaseSpan> Of(Case c, DateTimeOffset now, int plannedPrepMinutes = 25)
    {
        var spans = ImmutableList.CreateBuilder<PhaseSpan>();
        void Add(CarePhaseKind phase, DateTimeOffset from, DateTimeOffset? to, CarePhaseKind? plans = null)
        {
            var end = (to ?? now).ToOffset(now.Offset);
            var start = from.ToOffset(now.Offset);
            if (start > now && phase != CarePhaseKind.Planned || end <= start)
            {
                return;
            }
            spans.Add(new(c.Id, c.Patient.Name, phase, start, end, to is null) { Plans = plans });
        }

        var home = Earliest(c.SignedAt, c.DischargedAt);
        Add(CarePhaseKind.Waiting, c.CreatedAt, c.PrepStartedAt ?? c.InducedAt ?? home);
        if (c.PrepStartedAt is { } prep)
        {
            Add(CarePhaseKind.Prep, prep, c.InducedAt ?? home);
        }
        if (c.InducedAt is { } induced)
        {
            Add(CarePhaseKind.Anesthesia, induced, c.EndedAt ?? home);
            if (c.EndedAt is { } ended)
            {
                Add(CarePhaseKind.Recovery, ended, home);
            }
        }
        if (c.PlannedStart is { } planned && planned > now && Stage.Of(c) is CareStage.Booked or CareStage.Prep)
        {
            if (c.PrepStartedAt is null)
            {
                var prepFrom = planned.AddMinutes(-plannedPrepMinutes);
                Add(CarePhaseKind.Planned, prepFrom < now ? now : prepFrom, planned, CarePhaseKind.Prep);
            }
            Add(CarePhaseKind.Planned, planned, planned.AddMinutes(c.PlannedMinutes ?? DefaultPlannedMinutes), CarePhaseKind.Anesthesia);
        }
        return spans.ToImmutable();
    }

    /// <summary>
    /// Recorded time today (midnight → now) per phase, across every case. Planned slots do not count.
    /// Rows are always Anesthesia, Prep, Recovery, Waiting, in that order.
    /// </summary>
    public static CareTotals Totals(IEnumerable<Case> cases, DateTimeOffset now)
    {
        var midnight = new DateTimeOffset(now.Date, now.Offset);
        var sums = new Dictionary<CarePhaseKind, TimeSpan>();
        var patients = 0;
        var procedures = 0;
        foreach (var c in cases)
        {
            var any = false;
            foreach (var s in Of(c, now).Where(s => !s.IsPlanned))
            {
                var from = s.Start < midnight ? midnight : s.Start;
                var to = s.End > now ? now : s.End;
                if (to <= from)
                {
                    continue;
                }
                sums[s.Phase] = sums.GetValueOrDefault(s.Phase) + (to - from);
                any = true;
            }
            if (any)
            {
                patients++;
            }
            if (c.InducedAt is { } i && i <= now && i.ToOffset(now.Offset) >= midnight)
            {
                procedures++;
            }
        }
        var total = sums.Values.Aggregate(TimeSpan.Zero, (a, b) => a + b);
        var rows = new[] { CarePhaseKind.Anesthesia, CarePhaseKind.Prep, CarePhaseKind.Recovery, CarePhaseKind.Waiting }
            .Select(p => new CareTimeRow(p, sums.GetValueOrDefault(p), total > TimeSpan.Zero ? sums.GetValueOrDefault(p) / total : 0))
            .ToImmutableList();
        return new CareTotals(total, patients, procedures, now, rows);
    }

    /// <summary>Whole hours around every span: the gantt's axis (08:00 to 15:00 on the sample day).</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Window(IEnumerable<PhaseSpan> spans, DateTimeOffset now)
    {
        var list = spans.ToList();
        var midnight = new DateTimeOffset(now.Date, now.Offset);
        var first = list.Count > 0 ? list.Min(s => s.Start) : now.AddHours(-1);
        var last = list.Count > 0 ? list.Max(s => s.End) : now;
        if (first < midnight)
        {
            first = midnight;
        }
        if (last < now)
        {
            last = now;
        }
        var start = new DateTimeOffset(first.Year, first.Month, first.Day, first.Hour, 0, 0, now.Offset);
        var end = new DateTimeOffset(last.Year, last.Month, last.Day, last.Hour, 0, 0, now.Offset);
        return (start, end < last ? end.AddHours(1) : end);
    }

    public static string Label(CarePhaseKind p) => p switch
    {
        CarePhaseKind.Waiting => Loc.T("Phase_Waiting", "Waiting"),
        CarePhaseKind.Prep => Loc.T("Phase_Prep", "Prep"),
        CarePhaseKind.Anesthesia => Loc.T("Phase_Anesthesia", "Anesthesia"),
        CarePhaseKind.Recovery => Loc.T("Phase_Recovery", "Recovery"),
        _ => Loc.T("Phase_Planned", "Planned"),
    };

    /// <summary>"4h 13m", "0h 15m" (whole minutes, rounded down).</summary>
    public static string HoursMinutes(TimeSpan t) =>
        Loc.F("Duration_HoursMinutes", "{0}h {1}m", (int)t.TotalHours, t.Minutes);

    private static DateTimeOffset? Earliest(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null ? b : b is null ? a : a < b ? a : b;
}
