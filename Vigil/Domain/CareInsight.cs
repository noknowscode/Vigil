namespace Vigil.Domain;

/// <summary>The care-time insight: how long preparation ran today against the practice's plan.</summary>
public partial record CareInsight(int AverageMinutes, int PlannedMinutes, int Cases)
{
    public int DeltaMinutes => AverageMinutes - PlannedMinutes;

    public bool IsOverPlan => DeltaMinutes > 0;

    public static CareInsight? Of(IEnumerable<Case> cases, DateTimeOffset now, IPreferences prefs) =>
        Of(cases, now, prefs.PlannedPrepMinutes);

    /// <summary>
    /// Average of today's finished preparations (prep start to induction, induction today). Null without any.
    /// </summary>
    public static CareInsight? Of(IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes)
    {
        var minutes = cases
            .Where(c => c.PrepStartedAt is not null && c.InducedAt is { } i && i <= now && i.ToOffset(now.Offset).Date == now.Date)
            .Select(c => (c.InducedAt!.Value - c.PrepStartedAt!.Value).TotalMinutes)
            .Where(m => m >= 0)
            .ToList();
        return minutes.Count == 0
            ? null
            : new CareInsight((int)Math.Round(minutes.Average(), MidpointRounding.AwayFromZero), plannedPrepMinutes, minutes.Count);
    }
}
