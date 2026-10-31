using System.Globalization;
using Vigil.Services;

namespace Vigil.Domain;

public enum DueTaskKind { Observations, PainScore, Redose, Bloodwork, DischargeCall }

/// <summary>The row's button: Record, Score, Prepare, Review or Call.</summary>
public enum DueAction { Record, Score, Prepare, Review, Call }

/// <summary>How the status text reads: overdue (error), ready (success) or neutral.</summary>
public enum DueTone { Neutral, Overdue, Ready }

/// <summary>One row of "Due next".</summary>
public partial record DueTask(
    Guid CaseId,
    DueTaskKind Kind,
    DateTimeOffset DueAt,
    bool IsLate,
    string Title,
    string PatientName,
    CareStage Stage,
    string Meta,
    string Status,
    DueTone Tone,
    DueAction Action)
{
    public CaseRef Case => new(CaseId);

    /// <summary>"Late" or "Due", above the time.</summary>
    public string DueLabel => IsLate ? Loc.T("Task_Late", "Late") : Loc.T("Task_Due", "Due");

    public string Time => DueAt.ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Late and ready rows get the primary button; the rest are secondary.</summary>
    public bool IsPrimary => IsLate || Tone == DueTone.Ready;

    public string ActionLabel => Action switch
    {
        DueAction.Record => Loc.T("DueAction_Record", "Record"),
        DueAction.Score => Loc.T("DueAction_Score", "Score"),
        DueAction.Prepare => Loc.T("DueAction_Prepare", "Prepare"),
        DueAction.Review => Loc.T("DueAction_Review", "Review"),
        _ => Loc.T("DueAction_Call", "Call"),
    };

    /// <summary>"Late 12:41, Record observations, Knuckles · Anesthesia · 5-minute strip, Overdue 1:26".</summary>
    public string AccessibleName => $"{DueLabel} {Time}, {Title}, {PatientName} · {Meta}, {Status}";
}

/// <summary>
/// "Due next": every task that falls to the team, ordered by due time. Observations for a case under anesthesia
/// (reading interval), a pain score every 15 minutes in recovery, redoses (formulary interval), bloodwork that is
/// ready for review, and the discharge call to the pet parent.
/// </summary>
public static class DueTasks
{
    public static readonly TimeSpan PainScoreInterval = TimeSpan.FromMinutes(15);

    /// <summary>Tasks further away than this read "Scheduled" instead of a countdown.</summary>
    public static readonly TimeSpan CountdownHorizon = TimeSpan.FromMinutes(60);

    public static ImmutableList<DueTask> Of(IEnumerable<Case> cases, DateTimeOffset now, IPreferences prefs, IEnumerable<Drug> formulary) =>
        Of(cases, now, prefs.ReadingInterval, formulary);

    public static ImmutableList<DueTask> Of(IEnumerable<Case> cases, DateTimeOffset now, TimeSpan readingInterval, IEnumerable<Drug> formulary)
    {
        var drugs = formulary.ToList();
        var rows = new List<DueTask>();
        foreach (var c in cases)
        {
            var stage = Stage.Of(c);
            var name = c.Patient.Name;

            if (c.Status == CaseStatus.Anesthetized && Schedule.NextDue(c, readingInterval) is { } due)
            {
                var late = Schedule.State(c, readingInterval, now) == DueState.Overdue;
                var minutes = (int)Math.Round(readingInterval.TotalMinutes);
                rows.Add(Row(DueTaskKind.Observations, due, late, Loc.T("Task_Observations", "Record observations"),
                    $"{CarePhases.Label(CarePhaseKind.Anesthesia)} · {Loc.F("Task_Strip", "{0}-minute strip", minutes)}", DueAction.Record));

                foreach (var next in Redose.Of(c, drugs))
                {
                    rows.Add(Row(DueTaskKind.Redose, next.DueAt, next.DueAt < now, next.Title, next.Detail, DueAction.Prepare));
                }
            }

            if (c.Status == CaseStatus.Recovery && c.EndedAt is { } ended)
            {
                var last = c.Recovery.PainScores.Count > 0 ? c.Recovery.PainScores.Max(p => p.At) : ended;
                var dueAt = last + PainScoreInterval;
                rows.Add(Row(DueTaskKind.PainScore, dueAt, dueAt < now, Loc.T("Task_PainScore", "Pain score"),
                    $"{CarePhases.Label(CarePhaseKind.Recovery)} · {Loc.F("Task_Every", "every {0} min", (int)PainScoreInterval.TotalMinutes)}", DueAction.Score));
            }

            if (!c.IsSigned)
            {
                foreach (var lab in c.Labs.Where(l => l.State == LabState.Ready))
                {
                    var title = stage is CareStage.Booked or CareStage.Prep
                        ? Loc.T("Task_PreopBloodwork", "Pre-op bloodwork")
                        : Loc.T("Task_Bloodwork", "Bloodwork review");
                    rows.Add(Row(DueTaskKind.Bloodwork, lab.At, false, title, lab.Panel, DueAction.Review) with
                    {
                        Status = Loc.T("Task_ResultsReady", "Results ready"),
                        Tone = DueTone.Ready,
                    });
                }
            }

            if (c.DischargeCall is { IsDone: false } call)
            {
                rows.Add(Row(DueTaskKind.DischargeCall, call.DueAt, call.DueAt < now, Loc.T("Task_DischargeCall", "Discharge call"),
                    $"{Loc.T("Task_PetParent", "Pet parent")} · {c.Patient.Owner}", DueAction.Call));
            }

            DueTask Row(DueTaskKind kind, DateTimeOffset at, bool late, string title, string meta, DueAction action)
            {
                at = at.ToOffset(now.Offset);
                var status = late ? Countdown(at, now)
                    : at - now > CountdownHorizon ? Loc.T("Task_Scheduled", "Scheduled")
                    : Countdown(at, now);
                return new DueTask(c.Id, kind, at, late, title, name, stage, meta, status, late ? DueTone.Overdue : DueTone.Neutral, action);
            }
        }
        return rows.OrderBy(r => r.DueAt).ThenBy(r => r.Kind).ToImmutableList();
    }

    /// <summary>"5 tasks before 14:00 · 1 overdue"; "Nothing due" when the list is empty.</summary>
    public static string Summary(IReadOnlyCollection<DueTask> rows)
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

    /// <summary>"In 8 min" (whole minutes, rounded up) or "Overdue 1:26".</summary>
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
}
