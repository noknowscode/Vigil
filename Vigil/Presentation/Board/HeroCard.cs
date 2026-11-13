using System.Globalization;

namespace Vigil.Presentation;

/// <summary>A dot on the hero stats row: live (green), prep (amber), error (red) or muted.</summary>
public enum StatTone { Muted, Live, Prep, Error }

public partial record HeroStat(string Label, string Value, string Sub, StatTone Tone, bool IsMono = true)
{
    public bool IsLive => Tone == StatTone.Live;
    public bool IsPrep => Tone == StatTone.Prep;
    public bool IsError => Tone == StatTone.Error;
    public bool IsMuted => Tone == StatTone.Muted;
    public bool IsText => !IsMono;
}

/// <summary>
/// The "Right now" card: the case under anesthesia (live), or, when no surgery is running, the next booked case
/// (SPEC-v2 State designs, "No surgery in progress"). Null when neither exists: the FeedView shows its None state.
/// </summary>
public partial record HeroCard(
    Guid Id,
    bool IsLive,
    string Title,
    string Subtitle,
    string Timer,
    string TimerCaption,
    bool IsOverdue,
    string ChipText,
    string ChipNote,
    HeroStat First,
    HeroStat Second,
    HeroStat Third) : CaseRef(Id)
{
    public bool IsNext => !IsLive;

    /// <summary>Next surgery: "Start prep" stays until preparation begins.</summary>
    public bool CanStartPrep { get; init; }

    /// <summary>Next surgery with bloodwork ready: the green chip.</summary>
    public bool ChipReady { get; init; }

    public bool HasChip => ChipText.Length > 0;
    public bool ShowErrorChip => IsLive && IsOverdue;
    public bool ShowNeutralChip => HasChip && !ShowErrorChip && !ChipReady;
    public bool ShowReadyChip => HasChip && ChipReady;

    public string AccessibleName => $"{Title}. {Subtitle}. {Timer} {TimerCaption}. {ChipText}";

    public static HeroCard? From(IEnumerable<Case> cases, DateTimeOffset now, TimeSpan interval, int plannedPrepMinutes)
    {
        var list = cases.ToList();
        var live = list.Where(c => c.Status == CaseStatus.Anesthetized).OrderByDescending(c => c.InducedAt).FirstOrDefault();
        if (live is not null)
        {
            return Live(live, now, interval);
        }
        return NextSurgery.Of(list, now, plannedPrepMinutes) is { } next ? Next(next) : null;
    }

    private static HeroCard Live(Case c, DateTimeOffset now, TimeSpan interval)
    {
        var p = c.Patient;
        var elapsed = Schedule.Elapsed(c, now);
        var overdue = Schedule.State(c, interval, now) == DueState.Overdue;
        var remaining = (Schedule.Remaining(c, interval, now) ?? TimeSpan.Zero).Duration();
        var last = c.Readings.Count > 0 ? c.Readings[^1] : null;
        var next = Schedule.NextDue(c, interval);
        var minutes = (int)Math.Round(interval.TotalMinutes);
        return new HeroCard(
            c.Id,
            true,
            Loc.F("Hero_InSurgery", "In surgery · {0}", p.Name),
            string.IsNullOrWhiteSpace(c.OperatingRoom) ? c.Procedure : $"{c.Procedure} · {c.OperatingRoom}",
            Today.Clock(elapsed),
            Loc.T("Hero_UnderAnesthesia", "under anesthesia"),
            overdue,
            overdue
                ? Loc.F("Hero_ReadingOverdue", "Reading overdue {0}", Schedule.Clock(remaining))
                : Loc.F("Hero_NextReading", "Next reading in {0}", Schedule.Clock(remaining)),
            Loc.F("Task_Strip", "{0}-minute strip", minutes),
            new HeroStat(Loc.T("Hero_Induced", "Induced"), c.InducedAt is { } i ? Today.Time(i, now) : "—",
                Loc.F("Hero_Ago", "{0} ago", CareText.HoursMinutes(elapsed)), StatTone.Live),
            last is null
                ? new HeroStat(Loc.T("Hero_LastReading", "Last reading"), "—", Loc.T("Readings_None", "No readings yet"), StatTone.Muted)
                : new HeroStat(Loc.T("Hero_LastReading", "Last reading"), Today.Time(last.At, now),
                    Loc.F("Hero_LastValues", "HR {0} · SpO₂ {1}%", last.Hr?.ToString(CultureInfo.CurrentCulture) ?? "—", last.SpO2?.ToString(CultureInfo.CurrentCulture) ?? "—"), StatTone.Muted),
            new HeroStat(Loc.T("Hero_NextDue", "Next due"), next is { } n ? Today.Time(n, now) : "—",
                overdue ? Loc.F("Hero_Late", "{0} late", Schedule.Clock(remaining)) : Loc.F("Hero_In", "in {0}", Schedule.Clock(remaining)),
                overdue ? StatTone.Error : StatTone.Muted));
    }

    private static HeroCard Next(NextSurgery next)
    {
        var c = next.Case;
        var offset = next.Now.Offset;
        return new HeroCard(
            c.Id,
            false,
            Loc.F("Next_Title", "Next surgery · {0}", c.Patient.Name),
            next.Subtitle,
            next.CountdownText,
            Loc.F("Next_Until", "until {0:HH:mm}", next.PlannedStart),
            false,
            next.BloodworkReady ? Loc.T("Hero_BloodworkReady", "Bloodwork ready") : "",
            c.FastingSince is { } f ? Loc.F("Next_Fasting", "Fasting since {0:HH:mm}", f.ToOffset(offset)) : "",
            new HeroStat(Loc.T("Hero_PrepStarts", "Prep starts"), next.PrepStartsText,
                next.PrepStarted ? Loc.T("Hero_PrepStarted", "started") : CareText.Countdown(next.PrepStartsAt, next.Now, lowercase: true), StatTone.Prep),
            new HeroStat(Loc.T("Hero_Team", "Team"), c.Veterinarian, c.Technician, StatTone.Muted, IsMono: false),
            new HeroStat(Loc.T("Hero_PetParent", "Pet parent"), c.Patient.Owner,
                c.ParentNotifiedAt is { } n ? Loc.F("Next_Notified", "notified {0:HH:mm}", n.ToOffset(offset)) : "", StatTone.Muted, IsMono: false))
        {
            CanStartPrep = !next.PrepStarted,
            ChipReady = next.BloodworkReady,
        };
    }
}
