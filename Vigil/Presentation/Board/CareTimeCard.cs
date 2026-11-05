namespace Vigil.Presentation;

public partial record CareBar(CarePhaseKind Phase, string Label, string DurationText, string PercentText, double Fraction, string Tooltip) : IPhaseTone
{
    public bool IsPrep => Phase == CarePhaseKind.Prep;
    public bool IsAnesthesia => Phase == CarePhaseKind.Anesthesia;
    public bool IsRecovery => Phase == CarePhaseKind.Recovery;
    public bool IsWaiting => Phase == CarePhaseKind.Waiting;
}

/// <summary>"Where care time went": totals per phase, the prep insight and the next case to prepare (its checklist link).</summary>
public partial record CareTimeCard(string Summary, IImmutableList<CareBar> Bars, string Insight)
{
    public bool HasInsight => Insight.Length > 0;

    /// <summary>The next booked case's name: "See the prep checklist" opens its Preparation drawer. Empty when none is booked.</summary>
    public string ChecklistPatient { get; init; } = "";

    public bool HasChecklist => ChecklistPatient.Length > 0;

    public string ChecklistName => Loc.F("Insight_ChecklistName", "See the prep checklist for {0}", ChecklistPatient);

    public static CareTimeCard? From(IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes)
    {
        var list = cases.ToList();
        var totals = CarePhases.Totals(list, now);
        if (totals.Total <= TimeSpan.Zero)
        {
            return null;
        }
        var total = CareText.HoursMinutes(totals.Total);
        return new CareTimeCard(
            totals.PatientCount == 1
                ? Loc.F("CareTime_SummaryOne", "{0} across 1 patient today", total)
                : Loc.F("CareTime_Summary", "{0} across {1} patients today", total, totals.PatientCount),
            totals.Rows.Select(r => new CareBar(r.Phase, CareText.Phase(r.Phase), CareText.HoursMinutes(r.Duration), r.PercentText, r.Fraction, CareText.Tooltip(r))).ToImmutableList(),
            CareInsight.Of(list, now, plannedPrepMinutes) is { } insight ? InsightText(insight) : "")
        {
            ChecklistPatient = NextSurgery.Of(list, now, plannedPrepMinutes)?.Case.Patient.Name ?? "",
        };
    }

    /// <summary>"Prep ran 31 minutes on average today, 6 minutes over plan."</summary>
    public static string InsightText(CareInsight insight) => insight.DeltaMinutes switch
    {
        > 0 => Loc.F("Insight_Over", "Prep ran {0} minutes on average today, {1} minutes over plan.", insight.AverageMinutes, insight.DeltaMinutes),
        < 0 => Loc.F("Insight_Under", "Prep ran {0} minutes on average today, {1} minutes under plan.", insight.AverageMinutes, -insight.DeltaMinutes),
        _ => Loc.F("Insight_OnPlan", "Prep ran {0} minutes on average today, on plan.", insight.AverageMinutes),
    };
}
