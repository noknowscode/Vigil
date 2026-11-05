namespace Vigil.Domain;

public enum DueTaskKind { Observations, PainScore, Redose, Bloodwork, DischargeCall }

/// <summary>The row's button: Record, Score, Prepare, Review or Call.</summary>
public enum DueAction { Record, Score, Prepare, Review, Call }

/// <summary>How the status text reads: overdue (error), ready (success) or neutral.</summary>
public enum DueTone { Neutral, Overdue, Ready }

/// <summary>
/// One row of "Due next", as facts: what is due, for whom, when, and the details its words are built from (the reading
/// interval, the redose, the lab panel or the pet parent).
/// </summary>
public partial record DueTask(
    Guid CaseId,
    DueTaskKind Kind,
    DateTimeOffset DueAt,
    bool IsLate,
    string PatientName,
    CareStage Stage,
    DueTone Tone,
    DueAction Action)
{
    /// <summary>Observations: the reading interval the case is recorded at.</summary>
    public TimeSpan Interval { get; init; }

    /// <summary>Redose: the drug due again.</summary>
    public NextDose? Dose { get; init; }

    /// <summary>Bloodwork: the panel that is ready.</summary>
    public string Panel { get; init; } = "";

    /// <summary>Discharge call: the pet parent to call.</summary>
    public string Owner { get; init; } = "";

    /// <summary>Further away than <see cref="DueTasks.CountdownHorizon"/>: the row says "Scheduled", not a countdown.</summary>
    public bool IsScheduled { get; init; }
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

            if (c.Status == CaseStatus.Anesthetized && Schedule.NextDue(c, readingInterval) is { } due)
            {
                var late = Schedule.State(c, readingInterval, now) == DueState.Overdue;
                rows.Add(Row(DueTaskKind.Observations, due, late, DueAction.Record) with { Interval = readingInterval });

                foreach (var next in Redose.Of(c, drugs))
                {
                    rows.Add(Row(DueTaskKind.Redose, next.DueAt, next.DueAt < now, DueAction.Prepare) with { Dose = next });
                }
            }

            if (c.Status == CaseStatus.Recovery && c.EndedAt is { } ended)
            {
                var last = c.Recovery.PainScores.Count > 0 ? c.Recovery.PainScores.Max(p => p.At) : ended;
                var dueAt = last + PainScoreInterval;
                rows.Add(Row(DueTaskKind.PainScore, dueAt, dueAt < now, DueAction.Score));
            }

            if (!c.IsSigned)
            {
                foreach (var lab in c.Labs.Where(l => l.State == LabState.Ready))
                {
                    rows.Add(Row(DueTaskKind.Bloodwork, lab.At, false, DueAction.Review) with { Panel = lab.Panel, Tone = DueTone.Ready });
                }
            }

            if (c.DischargeCall is { IsDone: false } call)
            {
                rows.Add(Row(DueTaskKind.DischargeCall, call.DueAt, call.DueAt < now, DueAction.Call) with { Owner = c.Patient.Owner });
            }

            DueTask Row(DueTaskKind kind, DateTimeOffset at, bool late, DueAction action)
            {
                at = at.ToOffset(now.Offset);
                return new DueTask(c.Id, kind, at, late, c.Patient.Name, stage, late ? DueTone.Overdue : DueTone.Neutral, action)
                {
                    IsScheduled = !late && at - now > CountdownHorizon,
                };
            }
        }
        return rows.OrderBy(r => r.DueAt).ThenBy(r => r.Kind).ToImmutableList();
    }
}
