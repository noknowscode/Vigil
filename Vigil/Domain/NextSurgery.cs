using System.Globalization;
using Vigil.Services;

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

    /// <summary>"Next surgery · Oscar".</summary>
    public string Title => Loc.F("Next_Title", "Next surgery · {0}", Case.Patient.Name);

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

    /// <summary>"until 14:00".</summary>
    public string UntilText => Loc.F("Next_Until", "until {0:HH:mm}", PlannedStart);

    public string PrepStartsText => PrepStartsAt.ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>"in 48 min".</summary>
    public string PrepStartsSub => DueTasks.Countdown(PrepStartsAt, Now, lowercase: true);

    /// <summary>"Fasting since 06:00"; empty when not recorded.</summary>
    public string FastingText => Case.FastingSince is { } f ? Loc.F("Next_Fasting", "Fasting since {0:HH:mm}", f.ToOffset(Now.Offset)) : "";

    /// <summary>"notified 12:30"; empty when the pet parent has not been told.</summary>
    public string NotifiedText => Case.ParentNotifiedAt is { } n ? Loc.F("Next_Notified", "notified {0:HH:mm}", n.ToOffset(Now.Offset)) : "";

    public string Team => $"{Case.Veterinarian} · {Case.Technician}";

    public static NextSurgery? Of(IEnumerable<Case> cases, DateTimeOffset now, IPreferences prefs) => Of(cases, now, prefs.PlannedPrepMinutes);

    /// <summary>The earliest booked (or preparing) case with a planned start; null when none is planned.</summary>
    public static NextSurgery? Of(IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes) =>
        cases.Where(c => c.PlannedStart is not null && Stage.Of(c) is CareStage.Booked or CareStage.Prep)
            .OrderBy(c => c.PlannedStart)
            .Select(c =>
            {
                var start = c.PlannedStart!.Value.ToOffset(now.Offset);
                return new NextSurgery(c, start, c.PrepStartedAt?.ToOffset(now.Offset) ?? start.AddMinutes(-(c.PlannedPrepMinutes ?? plannedPrepMinutes)), now);
            })
            .FirstOrDefault();
}
