using System.Globalization;

namespace Vigil.Domain;

/// <summary>
/// The hero's "no surgery in progress" state: the next booked case by planned start, with its countdown,
/// prep start (planned start minus the case's planned prep time, else the practice's), fasting and pet-parent notice.
/// </summary>
public partial record NextSurgery(Case Case, DateTimeOffset PlannedStart, DateTimeOffset PrepStartsAt, DateTimeOffset Now)
{
    public Guid CaseId => Case.Id;

    public CaseRef Ref => new(Case.Id);

    /// <summary>Time to the planned start, never negative.</summary>
    public TimeSpan Countdown => PlannedStart > Now ? PlannedStart - Now : TimeSpan.Zero;

    public TimeSpan UntilPrep => PrepStartsAt > Now ? PrepStartsAt - Now : TimeSpan.Zero;

    public bool PrepStarted => Case.PrepStartedAt is not null;

    public bool BloodworkReady => Case.Labs.Any(l => l.State is LabState.Ready or LabState.Reviewed);

    /// <summary>"Mass removal, left flank · OR 1" (the room only when one is assigned).</summary>
    public string Subtitle => string.IsNullOrWhiteSpace(Case.OperatingRoom) ? Case.Procedure : $"{Case.Procedure} · {Case.OperatingRoom}";

    /// <summary>"1:18" (h:mm, minutes rounded up so a pending start never reads 0:00).</summary>
    public string CountdownText
    {
        get
        {
            var minutes = (int)Math.Ceiling(Countdown.TotalMinutes);
            return $"{minutes / 60}:{minutes % 60:00}";
        }
    }

    public string PrepStartsText => PrepStartsAt.ToString("HH:mm", CultureInfo.CurrentCulture);

    public string Team => $"{Case.Veterinarian} · {Case.Technician}";

    public static NextSurgery? Of(IEnumerable<Case> cases, DateTimeOffset now, IPreferences prefs) => Of(cases, now, prefs.PlannedPrepMinutes);

    /// <summary>
    /// The earliest booked (or preparing) case planned for today (the local day of <paramref name="now"/>); null when
    /// none is. A booking from another day never takes the hero: yesterday's no-show is not today's next surgery.
    /// </summary>
    public static NextSurgery? Of(IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes) =>
        cases.Where(c => c.PlannedStart is { } planned && planned.ToOffset(now.Offset).Date == now.Date
                && Stage.Of(c) is CareStage.Booked or CareStage.Prep)
            .OrderBy(c => c.PlannedStart)
            .Select(c =>
            {
                var start = c.PlannedStart!.Value.ToOffset(now.Offset);
                return new NextSurgery(c, start, c.PrepStartedAt?.ToOffset(now.Offset) ?? start.AddMinutes(-(c.PlannedPrepMinutes ?? plannedPrepMinutes)), now);
            })
            .FirstOrDefault();
}
