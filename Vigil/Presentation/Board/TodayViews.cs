using System.Globalization;
using Vigil.Controls;
using Vigil.Domain;

namespace Vigil.Presentation;

/// <summary>"Today's patients" segmented filter (docs/design/today-dashboard-values.md §2.3).</summary>
public enum CardFilter { InClinic, SignedOut, All }

/// <summary>Dot and fill colour of a phase-coloured mark; the templates switch on these flags (no brushes in records).</summary>
public interface IPhaseTone
{
    bool IsPrep { get; }
    bool IsAnesthesia { get; }
    bool IsRecovery { get; }
    bool IsWaiting { get; }
}

/// <summary>Today view-model building: pure functions of the cases and the time, unit-tested in TodayViewTests.</summary>
public static class Today
{
    /// <summary>Cases that belong on today's board: everyone still in the clinic, plus whoever went home today.</summary>
    public static bool IsToday(Case c, DateTimeOffset now) =>
        Stage.Of(c) != CareStage.Home || HomeAt(c)?.ToOffset(now.Offset).Date == now.Date;

    public static DateTimeOffset? HomeAt(Case c) => c.SignedAt ?? c.DischargedAt;

    public static bool Matches(Case c, CardFilter filter, DateTimeOffset now) => filter switch
    {
        CardFilter.InClinic => Stage.Of(c) != CareStage.Home,
        CardFilter.SignedOut => Stage.Of(c) == CareStage.Home && IsToday(c, now),
        _ => IsToday(c, now),
    };

    /// <summary>Card order: surgery, recovery, prep, booked (by planned start), home.</summary>
    public static int Rank(Case c) => Stage.Of(c) switch
    {
        CareStage.Surgery => 0,
        CareStage.Recovery => 1,
        CareStage.Prep => 2,
        CareStage.Booked => 3,
        _ => 4,
    };

    /// <summary>"1:12": hours and minutes, no leading zero (the card strip and hero timer).</summary>
    public static string Clock(TimeSpan t)
    {
        t = t < TimeSpan.Zero ? TimeSpan.Zero : t;
        return $"{(int)t.TotalHours}:{t.Minutes:00}";
    }

    public static string Time(DateTimeOffset t, DateTimeOffset now) => t.ToOffset(now.Offset).ToString("HH:mm", CultureInfo.CurrentCulture);

    public static ImmutableList<FilterTab> Tabs(IEnumerable<Case> cases, CardFilter selected, DateTimeOffset now)
    {
        var list = cases.ToList();
        FilterTab Tab(CardFilter f, string label) => new(f, label, list.Count(c => Matches(c, f, now)), f == selected);
        return
        [
            Tab(CardFilter.InClinic, Loc.T("Today_FilterInClinic", "In clinic")),
            Tab(CardFilter.SignedOut, Loc.T("Today_FilterSignedOut", "Signed out")),
            Tab(CardFilter.All, Loc.T("Today_FilterAll", "All")),
        ];
    }

    public static ImmutableList<PatientCard> Cards(IEnumerable<Case> cases, CardFilter filter, IImmutableSet<Guid> expanded, DateTimeOffset now, bool reduceMotion) =>
        cases.Where(c => Matches(c, filter, now))
            .OrderBy(Rank)
            .ThenBy(c => c.PlannedStart ?? c.CreatedAt)
            .Select(c => PatientCard.From(c, now, expanded.Contains(c.Id), reduceMotion))
            .ToImmutableList();

    /// <summary>
    /// A recorded-heart-rate sparkline in a 72 x 20 box (2 px inset): the last <paramref name="count"/> readings, scaled
    /// between their own minimum and maximum. Empty with fewer than two readings. SPEC-v2 decision 3: recorded
    /// readings, never a decorative ECG.
    /// </summary>
    public static string Sparkline(IEnumerable<VitalsReading> readings, int count = 12, double width = 72, double height = 20)
    {
        var hr = readings.Where(r => r.Hr is not null).Select(r => (double)r.Hr!.Value).TakeLast(count).ToList();
        if (hr.Count < 2)
        {
            return "";
        }
        var min = hr.Min();
        var max = hr.Max();
        const double inset = 2;
        var points = hr.Select((v, i) =>
        {
            var x = i * width / (hr.Count - 1);
            var y = max == min ? height / 2 : inset + (max - v) / (max - min) * (height - 2 * inset);
            return string.Create(CultureInfo.InvariantCulture, $"{x:0.##},{y:0.##}");
        });
        return "M" + string.Join(" L", points);
    }
}

public partial record FilterTab(CardFilter Filter, string Label, int Count, bool IsSelected)
{
    public bool IsUnselected => !IsSelected;

    public string AccessibleName => $"{Label}, {Count}";
}

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
                Loc.F("Hero_Ago", "{0} ago", CarePhases.HoursMinutes(elapsed)), StatTone.Live),
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
        return new HeroCard(
            c.Id,
            false,
            next.Title,
            next.Subtitle,
            next.CountdownText,
            next.UntilText,
            false,
            next.BloodworkReady ? Loc.T("Hero_BloodworkReady", "Bloodwork ready") : "",
            next.FastingText,
            new HeroStat(Loc.T("Hero_PrepStarts", "Prep starts"), next.PrepStartsText,
                next.PrepStarted ? Loc.T("Hero_PrepStarted", "started") : next.PrepStartsSub, StatTone.Prep),
            new HeroStat(Loc.T("Hero_Team", "Team"), c.Veterinarian, c.Technician, StatTone.Muted, IsMono: false),
            new HeroStat(Loc.T("Hero_PetParent", "Pet parent"), c.Patient.Owner, next.NotifiedText, StatTone.Muted, IsMono: false))
        {
            CanStartPrep = !next.PrepStarted,
            ChipReady = next.BloodworkReady,
        };
    }
}

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
                marks.Add(new GanttMark(At(s.Start), At(s.End) - At(s.Start), joined ? SegmentGap : 0, s.Phase, false, s.Tooltip));
            }
            // The now dot marks a phase that is running: anesthesia, recovery or prep (not waiting).
            if (x.Spans.LastOrDefault(s => s.Ongoing) is { Phase: CarePhaseKind.Prep or CarePhaseKind.Anesthesia or CarePhaseKind.Recovery } running)
            {
                marks.Add(new GanttMark(At(now), 0, 0, running.Phase, true, running.Tooltip));
            }
            return new GanttRow(x.Case.Patient.Name, marks.ToImmutableList());
        }).ToImmutableList();

        var ticks = Enumerable.Range(0, (int)Math.Round((end - start).TotalHours) + 1)
            .Select(h => new AxisTick(start.AddHours(h).ToString("HH:mm", CultureInfo.CurrentCulture), At(start.AddHours(h))))
            .ToImmutableList();

        var totals = CarePhases.Totals(today, now);
        var name = Loc.F("Gantt_Accessible", "Care through the day, {0} to {1}. {2}",
            start.ToString("HH:mm", CultureInfo.CurrentCulture), end.ToString("HH:mm", CultureInfo.CurrentCulture),
            string.Join(" ", perCase.Select(x => $"{x.Case.Patient.Name}: {string.Join(", ", x.Spans.Select(s => s.Tooltip[(x.Case.Patient.Name.Length + 3)..]))}.")));
        return new CareTimeline(
            totals.TotalText,
            totals.DayLine,
            Loc.F("Gantt_Now", "Now {0:HH:mm}", now),
            At(now),
            rows,
            ticks,
            name);
    }
}

/// <summary>
/// A patient card (today-dashboard-values.md §2.4); a tap opens the record (<see cref="Ref"/>). Deliberately no <c>Id</c>
/// property: MVUX treats an <c>Id</c> as the item key and replaces a key-equal card in place, which left the repeater
/// showing the previous card (timers and Show details stuck); without a key a changed card is swapped for a new one.
/// </summary>
public partial record PatientCard(
    Guid CaseId,
    string Name,
    string Sub,
    string Asa,
    bool AsaPending,
    string? Photo,
    CareStage Stage,
    string StripLabel,
    string StripValue,
    string Sparkline,
    string Procedure,
    string Weight,
    string Owner,
    string Team,
    bool IsExpanded,
    bool ReduceMotion)
{
    public CaseRef Ref => new(CaseId);

    public bool HasPhoto => !string.IsNullOrEmpty(Photo);
    public bool HasNoPhoto => !HasPhoto;
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0]));
    public bool AsaKnown => !AsaPending;
    public bool HasSparkline => Sparkline.Length > 0;

    /// <summary>The strip value is a clock (mono) except "Not started" on a booked card.</summary>
    public bool StripIsClock => Stage != CareStage.Booked;
    public bool StripIsText => Stage == CareStage.Booked;

    /// <summary>Stepper column of the knob: 0 Booked … 4 Home.</summary>
    public int StageIndex => (int)Stage;
    public bool ReachedPrep => Stage >= CareStage.Prep;
    public bool ReachedSurgery => Stage >= CareStage.Surgery;
    public bool ReachedRecovery => Stage >= CareStage.Recovery;
    public bool ReachedHome => Stage >= CareStage.Home;
    public bool KnobLive => Stage == CareStage.Surgery;

    public string StageName => Domain.Stage.AccessibleName(Stage);

    public string ToggleText => IsExpanded ? Loc.T("Card_HideDetails", "Hide details") : Loc.T("Card_ShowDetails", "Show details");

    /// <summary>
    /// States for <c>utu:VisualStateManagerExtensions.States</c> on the card: strip (Live, LiveStill, Prep, Recovery,
    /// Booked, Home), stepper label (StageBooked … StageHome) and details (Collapsed/Expanded, or the Still variants
    /// that skip the motion when Reduce motion is on).
    /// </summary>
    public string VisualStates =>
        $"{StripState},Stage{Stage},{(IsExpanded ? "Expanded" : "Collapsed")}{(ReduceMotion ? "Still" : "")}";

    private string StripState => Stage switch
    {
        CareStage.Surgery => ReduceMotion ? "LiveStill" : "Live",
        CareStage.Prep => "Prep",
        CareStage.Recovery => "Recovery",
        CareStage.Home => "Home",
        _ => "Booked",
    };

    public string AccessibleName => $"{Name}, {Sub}. {StripLabel} {StripValue}. {StageName}. {Procedure}";

    public static PatientCard From(Case c, DateTimeOffset now, bool expanded, bool reduceMotion)
    {
        var p = c.Patient;
        var stage = Domain.Stage.Of(c);
        var age = p.AgeYears == 1 ? Loc.T("Card_OneYear", "1 yr") : Loc.F("Card_Years", "{0:0.#} yrs", p.AgeYears);
        var breed = string.IsNullOrWhiteSpace(p.Breed) ? Loc.Species(p.Species) : p.Breed;
        var (label, value) = stage switch
        {
            CareStage.Surgery => (Loc.T("Strip_Anesthesia", "ANESTHESIA"), Today.Clock(Schedule.Elapsed(c, now))),
            CareStage.Recovery => (Loc.T("Strip_Recovery", "IN RECOVERY"), Today.Clock(now - (c.EndedAt ?? now))),
            CareStage.Prep => (Loc.T("Strip_Prep", "IN PREP"), Today.Clock(now - (c.PrepStartedAt ?? now))),
            CareStage.Home => (Loc.T("Strip_Home", "SIGNED OUT"), Today.HomeAt(c) is { } home ? Today.Time(home, now) : ""),
            _ => (Loc.T("Strip_Booked", "BOOKED"), Loc.T("Strip_NotStarted", "Not started")),
        };
        return new PatientCard(
            c.Id,
            p.Name,
            $"{breed} · {age}",
            c.Preop.Asa is >= 1 and <= 5 ? c.Preop.AsaLabel : Loc.T("Card_AsaPending", "ASA pending"),
            c.Preop.Asa is not (>= 1 and <= 5),
            PatientMedia.Photo(p),
            stage,
            label,
            value,
            stage == CareStage.Surgery ? Today.Sparkline(c.Readings) : "",
            c.Procedure,
            string.Create(CultureInfo.CurrentCulture, $"{p.WeightKg:0.0} kg"),
            string.IsNullOrWhiteSpace(p.Owner) ? Loc.T("Patient_NotRecorded", "Not recorded") : p.Owner,
            $"{c.Veterinarian} · {c.Technician}",
            expanded,
            reduceMotion);
    }
}

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

    /// <summary>Where the row's button goes: the record, the dose screen (redose) or sign-off (discharge call).</summary>
    public string Route => Action switch
    {
        DueAction.Prepare => "Dose",
        DueAction.Call => "SignOff",
        _ => "Record",
    };

    public static DueRow From(DueTask t) => new(
        t.CaseId, t.Action, t.DueLabel, t.Time, t.IsLate, t.Title, t.PatientName, t.Meta, t.Status, t.Tone, t.Stage,
        t.ActionLabel, t.IsPrimary, t.AccessibleName);
}

/// <summary>"Due next" header line and the all-caught-up line under the empty table.</summary>
public partial record DueHeader(string Summary, string NextUp)
{
    public static DueHeader From(IReadOnlyCollection<DueTask> rows, IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes)
    {
        var next = NextSurgery.Of(cases, now, plannedPrepMinutes);
        var summary = rows.Count > 0
            ? DueTasks.Summary(rows)
            : next is null
                ? Loc.T("Task_Nothing", "Nothing due")
                : Loc.F("Due_NothingBefore", "Nothing due before {0:HH:mm}", next.PlannedStart);
        var nextUp = next is null
            ? Loc.T("Due_NothingPlanned", "Nothing else is planned today.")
            : Loc.F("Due_NextPrep", "Next: {0} prep at {1}", next.Case.Patient.Name, next.PrepStartsText);
        return new DueHeader(summary, nextUp);
    }
}

public partial record CareBar(CarePhaseKind Phase, string Label, string DurationText, string PercentText, double Fraction, string Tooltip) : IPhaseTone
{
    public bool IsPrep => Phase == CarePhaseKind.Prep;
    public bool IsAnesthesia => Phase == CarePhaseKind.Anesthesia;
    public bool IsRecovery => Phase == CarePhaseKind.Recovery;
    public bool IsWaiting => Phase == CarePhaseKind.Waiting;
}

/// <summary>"Where care time went": totals per phase and the prep insight.</summary>
public partial record CareTimeCard(string Summary, IImmutableList<CareBar> Bars, string Insight)
{
    public bool HasInsight => Insight.Length > 0;

    public static CareTimeCard? From(IEnumerable<Case> cases, DateTimeOffset now, int plannedPrepMinutes)
    {
        var list = cases.ToList();
        var totals = CarePhases.Totals(list, now);
        if (totals.Total <= TimeSpan.Zero)
        {
            return null;
        }
        return new CareTimeCard(
            totals.Summary,
            totals.Rows.Select(r => new CareBar(r.Phase, r.Label, r.DurationText, r.PercentText, r.Fraction, r.Tooltip)).ToImmutableList(),
            CareInsight.Of(list, now, plannedPrepMinutes)?.Text ?? "");
    }
}

/// <summary>Copy for the card grid's None state: no one today, or nobody under the chosen filter.</summary>
public partial record CardsEmpty(string Title, string Body, bool ShowActions)
{
    public static CardsEmpty From(IEnumerable<Case> cases, CardFilter filter, DateTimeOffset now) =>
        cases.Any(c => Today.IsToday(c, now))
            ? filter == CardFilter.SignedOut
                ? new(Loc.T("Today_NoneSignedOut", "No one has gone home yet today."), Loc.T("Today_NoneSignedOutBody", "Signed-out patients appear here."), false)
                : new(Loc.T("Today_NoneInClinic", "No patients in the clinic right now."), Loc.T("Today_NoneInClinicBody", "Everyone booked today has gone home."), false)
            : new(Loc.T("Today_NoneTitle", "No one is booked in for today yet."), Loc.T("Today_NoneBody", "New patients and today's bookings appear here."), true);
}
