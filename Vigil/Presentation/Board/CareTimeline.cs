using System.Globalization;
using Vigil.Controls;

namespace Vigil.Presentation;

/// <summary>One mark on a gantt row: a phase segment, a planned (hatched) slot or the now dot.</summary>
public partial record GanttMark(double Start, double Length, double Gap, CarePhaseKind Phase, bool IsDot, string Tooltip) : ISpan, IPhaseTone
{
    public bool IsPrep => !IsPlanned && Phase == CarePhaseKind.Prep;
    public bool IsAnesthesia => !IsPlanned && Phase == CarePhaseKind.Anesthesia;
    public bool IsRecovery => !IsPlanned && Phase == CarePhaseKind.Recovery;
    public bool IsWaiting => !IsPlanned && Phase == CarePhaseKind.Waiting;
    public bool IsPlanned => !IsDot && Phase == CarePhaseKind.Planned;
    public bool IsSegment => !IsDot;
}

public partial record GanttRow(string Name, IImmutableList<GanttMark> Marks);

/// <summary>An hour on the gantt axis ("08:00") and its gridline.</summary>
public partial record AxisTick(string Label, double Start) : ISpan
{
    public double Length => 0;
    public double Gap => 0;
}

/// <summary>"Under care today" and the gantt "Care through the day" (today-dashboard-values.md §2.1-2.2).</summary>
public partial record CareTimeline(
    string TotalText,
    string DayLine,
    string NowText,
    double NowFraction,
    IImmutableList<GanttRow> Rows,
    IImmutableList<AxisTick> Ticks,
    string AccessibleName)
{
    /// <summary>Two pixels between a segment and the one that starts where it ends.</summary>
    public const double SegmentGap = 2;

    public static CareTimeline? From(IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes)
    {
        var today = cases.Where(c => Today.IsToday(c, now)).ToList();
        var perCase = today
            .Select(c => (Case: c, Spans: CarePhases.Of(c, now, plannedPrepMinutes)))
            .Where(x => x.Spans.Count > 0)
            .OrderBy(x => x.Spans.Min(s => s.Start))
            .ToList();
        if (perCase.Count == 0)
        {
            return null;
        }
        var (start, end) = CarePhases.Window(perCase.SelectMany(x => x.Spans), now);
        var total = (end - start).TotalMinutes;
        double At(DateTimeOffset t) => Math.Clamp((t - start).TotalMinutes / total, 0, 1);

        var rows = perCase.Select(x =>
        {
            var marks = new List<GanttMark>();
            for (var i = 0; i < x.Spans.Count; i++)
            {
                var s = x.Spans[i];
                var joined = i + 1 < x.Spans.Count && x.Spans[i + 1].Start == s.End;
                marks.Add(new GanttMark(At(s.Start), At(s.End) - At(s.Start), joined ? SegmentGap : 0, s.Phase, false, CareText.Tooltip(s)));
            }
            // The now dot marks a phase that is running: anesthesia, recovery or prep (not waiting).
            if (x.Spans.LastOrDefault(s => s.Ongoing) is { Phase: CarePhaseKind.Prep or CarePhaseKind.Anesthesia or CarePhaseKind.Recovery } running)
            {
                marks.Add(new GanttMark(At(now), 0, 0, running.Phase, true, CareText.Tooltip(running)));
            }
            return new GanttRow(x.Case.Patient.Name, marks.ToImmutableList());
        }).ToImmutableList();

        var ticks = Enumerable.Range(0, (int)Math.Round((end - start).TotalHours) + 1)
            .Select(h => new AxisTick(start.AddHours(h).ToString("HH:mm", CultureInfo.CurrentCulture), At(start.AddHours(h))))
            .ToImmutableList();

        var totals = CarePhases.Totals(today, now);
        var name = Loc.F("Gantt_Accessible", "Care through the day, {0} to {1}. {2}",
            start.ToString("HH:mm", CultureInfo.CurrentCulture), end.ToString("HH:mm", CultureInfo.CurrentCulture),
            string.Join(" ", perCase.Select(x => $"{x.Case.Patient.Name}: {string.Join(", ", x.Spans.Select(s => CareText.Tooltip(s)[(x.Case.Patient.Name.Length + 3)..]))}.")));
        return new CareTimeline(
            CareText.HoursMinutes(totals.Total),
            DayLineText(totals),
            Loc.F("Gantt_Now", "Now {0:HH:mm}", now),
            At(now),
            rows,
            ticks,
            name);
    }

    /// <summary>"4 patients · 3 procedures · Wed 28 Oct".</summary>
    public static string DayLineText(CareTotals totals) =>
        $"{(totals.PatientCount == 1 ? Loc.T("CareTime_OnePatient", "1 patient") : Loc.F("CareTime_Patients", "{0} patients", totals.PatientCount))} · " +
        $"{(totals.ProcedureCount == 1 ? Loc.T("CareTime_OneProcedure", "1 procedure") : Loc.F("CareTime_Procedures", "{0} procedures", totals.ProcedureCount))} · " +
        totals.Now.ToString("ddd d MMM", CultureInfo.CurrentCulture);
}
