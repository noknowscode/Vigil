using System.Globalization;

namespace Vigil.Presentation;

/// <summary>A "Due next" row (today-dashboard-values.md §2.5).</summary>
public partial record DueRow(
    Guid CaseId,
    DueAction Action,
    string DueLabel,
    string Time,
    bool IsLate,
    string Title,
    string PatientName,
    string Meta,
    string Status,
    DueTone Tone,
    CareStage Stage,
    string ActionLabel,
    bool IsPrimary,
    string AccessibleName) : IPhaseTone
{
    public bool IsDue => !IsLate;
    public bool IsOverdue => Tone == DueTone.Overdue;
    public bool IsReady => Tone == DueTone.Ready;
    public bool IsNeutral => Tone == DueTone.Neutral;
    public bool IsSecondary => !IsPrimary;

    // Meta dot: the patient's phase colour (home reads as muted ink).
    public bool IsPrep => Stage == CareStage.Prep;
    public bool IsAnesthesia => Stage == CareStage.Surgery;
    public bool IsRecovery => Stage == CareStage.Recovery;
    public bool IsWaiting => Stage == CareStage.Booked;
    public bool IsHome => Stage == CareStage.Home;

    /// <summary>Where the row's button goes: the redose page, else the record.</summary>
    public string Route => Action == DueAction.Prepare ? "Redose" : "Record";

    /// <summary>The record pane whose drawer the row's button opens: observations, recovery (pain score, discharge call) or labs.</summary>
    public string? Pane => Action switch
    {
        DueAction.Record => "Monitor",
        DueAction.Score or DueAction.Call => "Recovery",
        DueAction.Review => "Labs",
        _ => null,
    };

    public static DueRow From(DueTask t, DateTimeOffset now)
    {
        var dueLabel = t.IsLate ? Loc.T("Task_Late", "Late") : Loc.T("Task_Due", "Due");
        var time = t.DueAt.ToString("HH:mm", CultureInfo.CurrentCulture);
        var (title, meta) = t.Kind switch
        {
            DueTaskKind.Observations => (Loc.T("Task_Observations", "Record observations"),
                $"{CareText.Phase(CarePhaseKind.Anesthesia)} · {Loc.F("Task_Strip", "{0}-minute strip", (int)Math.Round(t.Interval.TotalMinutes))}"),
            DueTaskKind.Redose when t.Dose is { } dose => (CareText.Redose(dose), dose.Detail),
            DueTaskKind.PainScore => (Loc.T("Task_PainScore", "Pain score"),
                $"{CareText.Phase(CarePhaseKind.Recovery)} · {Loc.F("Task_Every", "every {0} min", (int)DueTasks.PainScoreInterval.TotalMinutes)}"),
            DueTaskKind.Bloodwork => (t.Stage is CareStage.Booked or CareStage.Prep
                ? Loc.T("Task_PreopBloodwork", "Pre-op bloodwork")
                : Loc.T("Task_Bloodwork", "Bloodwork review"), t.Panel),
            _ => (Loc.T("Task_DischargeCall", "Discharge call"), $"{Loc.T("Task_PetParent", "Pet parent")} · {t.Owner}"),
        };
        var status = t.Tone == DueTone.Ready ? Loc.T("Task_ResultsReady", "Results ready")
            : t.IsScheduled ? Loc.T("Task_Scheduled", "Scheduled")
            : CareText.Countdown(t.DueAt, now);
        var action = t.Action switch
        {
            DueAction.Record => Loc.T("DueAction_Record", "Record"),
            DueAction.Score => Loc.T("DueAction_Score", "Score"),
            DueAction.Prepare => Loc.T("DueAction_Prepare", "Prepare"),
            DueAction.Review => Loc.T("DueAction_Review", "Review"),
            _ => Loc.T("DueAction_Call", "Call"),
        };
        // Late and ready rows get the primary button; the rest are secondary.
        return new(t.CaseId, t.Action, dueLabel, time, t.IsLate, title, t.PatientName, meta, status, t.Tone, t.Stage, action,
            t.IsLate || t.Tone == DueTone.Ready,
            $"{dueLabel} {time}, {title}, {t.PatientName} · {meta}, {status}");
    }
}

/// <summary>"Due next" header line and the all-caught-up line under the empty table.</summary>
public partial record DueHeader(string Summary, string NextUp)
{
    public static DueHeader From(IReadOnlyCollection<DueTask> rows, IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes)
    {
        var next = NextSurgery.Of(cases, now, plannedPrepMinutes);
        var summary = rows.Count > 0
            ? SummaryText(rows)
            : next is null
                ? Loc.T("Task_Nothing", "Nothing due")
                : Loc.F("Due_NothingBefore", "Nothing due before {0:HH:mm}", next.PlannedStart);
        var nextUp = next is null
            ? Loc.T("Due_NothingPlanned", "Nothing else is planned today.")
            : Loc.F("Due_NextPrep", "Next: {0} prep at {1}", next.Case.Patient.Name, next.PrepStartsText);
        return new DueHeader(summary, nextUp);
    }

    /// <summary>"5 tasks before 14:00 · 1 overdue"; "Nothing due" when the list is empty.</summary>
    public static string SummaryText(IReadOnlyCollection<DueTask> rows)
    {
        if (rows.Count == 0)
        {
            return Loc.T("Task_Nothing", "Nothing due");
        }
        var last = rows.Max(r => r.DueAt);
        var head = rows.Count == 1
            ? Loc.F("Task_OneTaskBefore", "1 task before {0:HH:mm}", last)
            : Loc.F("Task_TasksBefore", "{0} tasks before {1:HH:mm}", rows.Count, last);
        var late = rows.Count(r => r.IsLate);
        return late > 0 ? $"{head} · {Loc.F("Task_OverdueCount", "{0} overdue", late)}" : head;
    }
}
