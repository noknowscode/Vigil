using System.Globalization;

namespace Vigil.Presentation;

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

    public string StageName => CareText.StageAccessibleName(Stage);

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
