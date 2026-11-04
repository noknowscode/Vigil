namespace Vigil.Presentation;

/// <summary>
/// The words the screens put on the care day's domain values: stages, phases, durations, countdowns and redoses.
/// The domain keeps the facts; Today, the record and the agenda read their labels here.
/// </summary>
public static class CareText
{
    public static string Stage(CareStage s) => s switch
    {
        CareStage.Prep => Loc.T("Stage_Prep", "Prep"),
        CareStage.Surgery => Loc.T("Stage_Surgery", "Surgery"),
        CareStage.Recovery => Loc.T("Stage_Recovery", "Recovery"),
        CareStage.Home => Loc.T("Stage_Home", "Home"),
        _ => Loc.T("Stage_Booked", "Booked"),
    };

    /// <summary>"Stage 3 of 5: surgery".</summary>
    public static string StageAccessibleName(CareStage s) =>
        Loc.F("Stage_Accessible", "Stage {0} of {1}: {2}", Domain.Stage.Number(s), Domain.Stage.Count, Stage(s).ToLowerInvariant());

    public static string Phase(CarePhaseKind p) => p switch
    {
        CarePhaseKind.Waiting => Loc.T("Phase_Waiting", "Waiting"),
        CarePhaseKind.Prep => Loc.T("Phase_Prep", "Prep"),
        CarePhaseKind.Anesthesia => Loc.T("Phase_Anesthesia", "Anesthesia"),
        CarePhaseKind.Recovery => Loc.T("Phase_Recovery", "Recovery"),
        _ => Loc.T("Phase_Planned", "Planned"),
    };

    /// <summary>"Prep", "Anesthesia", "Planned prep", "Planned surgery".</summary>
    public static string Phase(PhaseSpan s) => s.Phase != CarePhaseKind.Planned
        ? Phase(s.Phase)
        : s.Plans == CarePhaseKind.Prep ? Loc.T("Phase_PlannedPrep", "Planned prep") : Loc.T("Phase_PlannedSurgery", "Planned surgery");

    /// <summary>"Pepper · Prep 08:00–08:32", "Milo · Recovery 12:08–now".</summary>
    public static string Tooltip(PhaseSpan s) =>
        $"{s.PatientName} · {Phase(s)} {s.Start:HH:mm}–{(s.Ongoing ? Loc.T("Phase_Now", "now") : s.End.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture))}";

    /// <summary>"Anesthesia: 4h 13m, 58% of care time".</summary>
    public static string Tooltip(CareTimeRow r) =>
        Loc.F("CareTime_Tooltip", "{0}: {1}, {2}% of care time", Phase(r.Phase), HoursMinutes(r.Duration), r.Percent);

    /// <summary>"4h 13m", "0h 15m" (whole minutes, rounded down).</summary>
    public static string HoursMinutes(TimeSpan t) =>
        Loc.F("Duration_HoursMinutes", "{0}h {1}m", (int)t.TotalHours, t.Minutes);

    /// <summary>"In 8 min" (whole minutes, rounded up) or "Overdue 1:26"; lowercase inside a sentence.</summary>
    public static string Countdown(DateTimeOffset due, DateTimeOffset now, bool lowercase = false)
    {
        var left = due - now;
        if (left < TimeSpan.Zero)
        {
            var clock = Schedule.Clock(left.Duration());
            return lowercase ? Loc.F("Task_OverdueLower", "overdue {0}", clock) : Loc.F("Task_Overdue", "Overdue {0}", clock);
        }
        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        return lowercase ? Loc.F("Task_InMinutesLower", "in {0} min", minutes) : Loc.F("Task_InMinutes", "In {0} min", minutes);
    }

    /// <summary>"Cefazolin redose".</summary>
    public static string Redose(NextDose next) => Loc.F("Redose_Title", "{0} redose", next.Last.DrugName);

    /// <summary>"Cefazolin redose · in 28 min".</summary>
    public static string Redose(NextDose next, DateTimeOffset now) => $"{Redose(next)} · {Countdown(next.DueAt, now, lowercase: true)}";
}
