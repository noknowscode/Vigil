namespace Vigil.Domain;

public enum Species { Canine, Feline, Other }

public enum CaseStatus { Scheduled, Anesthetized, Recovery, Signed }

public enum Plane { Light, Surgical, Deep }

public enum DoseRoute { IV, IM, SC, PO }

public partial record CaseRef(Guid Id);

/// <summary>Opens a record with one of its panes' drawers open ("Labs", "Recovery", ...): a "Due next" action lands on its task.</summary>
public partial record CaseRefPane(Guid Id, string Pane) : CaseRef(Id);

public partial record Patient(string Name, Species Species, string Breed, decimal WeightKg, decimal AgeYears, string Owner)
{
    /// <summary>Bundled image identifiers for fictional demo records; absent on existing records.</summary>
    public string? PhotoAsset { get; init; }
    public string? HeroAsset { get; init; }
}

public partial record PreopCheck(
    bool Fasted,
    bool IvCatheter,
    bool MachineChecked,
    bool ConsentSigned,
    bool BloodworkReviewed,
    int Asa,
    bool AsaEmergency,
    string Notes)
{
    public static PreopCheck Empty { get; } = new(false, false, false, false, false, 0, false, "");

    public int DoneCount => new[] { Fasted, IvCatheter, MachineChecked, ConsentSigned, BloodworkReviewed }.Count(x => x);

    public bool IsComplete => DoneCount == 5 && Asa is >= 1 and <= 5;

    /// <summary>"ASA IIE"; "ASA —" before the status is set (notation, the same in every language).</summary>
    public string AsaLabel => Asa is >= 1 and <= 5 ? $"ASA {Roman(Asa)}{(AsaEmergency ? "E" : "")}" : "ASA —";

    public static string Roman(int asa) => asa switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "" };

    /// <summary>
    /// This check with the edits that turned <paramref name="before"/> into <paramref name="after"/>. A field the edit
    /// left alone keeps this check's value, which may have been set elsewhere since (Mark reviewed ticks the bloodwork).
    /// </summary>
    public PreopCheck WithEdits(PreopCheck before, PreopCheck after)
    {
        static T Pick<T>(T mine, T was, T now) => EqualityComparer<T>.Default.Equals(was, now) ? mine : now;
        return new(
            Pick(Fasted, before.Fasted, after.Fasted),
            Pick(IvCatheter, before.IvCatheter, after.IvCatheter),
            Pick(MachineChecked, before.MachineChecked, after.MachineChecked),
            Pick(ConsentSigned, before.ConsentSigned, after.ConsentSigned),
            Pick(BloodworkReviewed, before.BloodworkReviewed, after.BloodworkReviewed),
            Pick(Asa, before.Asa, after.Asa),
            Pick(AsaEmergency, before.AsaEmergency, after.AsaEmergency),
            Pick(Notes, before.Notes, after.Notes));
    }
}

public partial record VitalsReading(
    DateTimeOffset At,
    int? Hr,
    int? Rr,
    int? SpO2,
    int? EtCo2,
    int? Sys,
    int? Dia,
    int? Map,
    decimal? TempC,
    decimal? VaporizerPct,
    decimal? O2LMin,
    Plane Plane,
    string Note)
{
    /// <summary>Mean arterial pressure: entered when the monitor shows it, else derived (Sys + 2·Dia) / 3.</summary>
    public int? EffectiveMap => Map ?? Vitals.DeriveMap(Sys, Dia);

    public bool MapIsCalculated => Map is null && EffectiveMap is not null;
}

public partial record DoseGiven(
    Guid Id,
    DateTimeOffset At,
    string DrugId,
    string DrugName,
    decimal MgPerKg,
    decimal MgPerMl,
    decimal VolumeMl,
    DoseRoute Route,
    bool OutOfRangeConfirmed);

public partial record RecoveryLog(DateTimeOffset? ExtubatedAt, DateTimeOffset? SternalAt, int? PainScore, decimal? TempC, string Notes)
{
    public static RecoveryLog Empty { get; } = new(null, null, null, null, "");

    /// <summary>Timed pain scores in recovery (one every 15 minutes is due). <see cref="PainScore"/> stays for export.</summary>
    public ImmutableList<Domain.PainScore> PainScores { get; init => field = value ?? []; } = [];

    /// <summary>
    /// Records a pain score (0 none to 4 severe) taken at <paramref name="at"/>: it joins the timed list (which resets the
    /// 15-minute "Pain score" task even when the score is unchanged) and becomes the current score the export reads.
    /// </summary>
    public RecoveryLog WithPainScore(int score, DateTimeOffset at) => score is < 0 or > 4
        ? throw new ArgumentOutOfRangeException(nameof(score), score, "Pain score is 0 to 4.")
        : this with { PainScore = score, PainScores = PainScores.Add(new Domain.PainScore(at, score)) };
}

public partial record PainScore(DateTimeOffset At, int Score);

/// <summary>A fluid infusion, e.g. LRS at 5 mL/kg/h. Running while <see cref="StoppedAt"/> is null.</summary>
public partial record FluidRun(string Name, decimal RateMlKgH, DateTimeOffset StartedAt, DateTimeOffset? StoppedAt)
{
    public bool IsRunning => StoppedAt is null;

    /// <summary>"LRS 5 mL/kg/h".</summary>
    public string Label => $"{Name} {RateMlKgH:0.##} mL/kg/h";
}

public enum LabState { Pending, Ready, Reviewed }

/// <summary>A lab panel. <see cref="At"/> is when the result is expected (Pending) or due for review (Ready).</summary>
public partial record LabResult(string Panel, LabState State, DateTimeOffset At);

/// <summary>
/// A correction to a case's details after booking (a mistyped weight, the wrong room): which detail
/// (<see cref="CaseDetail"/>), its value before and after (invariant text), and when it was made.
/// </summary>
public partial record CaseCorrection(DateTimeOffset At, string Field, string Before, string After);

/// <summary>The details a correction can change, as <see cref="CaseCorrection.Field"/> names.</summary>
public static class CaseDetail
{
    public const string Name = "Name";
    public const string Species = "Species";
    public const string Breed = "Breed";
    public const string Weight = "Weight";
    public const string Age = "Age";
    public const string Owner = "Owner";
    public const string Procedure = "Procedure";
    public const string Veterinarian = "Veterinarian";
    public const string Technician = "Technician";
    public const string Room = "Room";
    public const string PlannedStart = "PlannedStart";
    public const string PlannedLength = "PlannedLength";
    public const string FastingSince = "FastingSince";
    public const string ParentNotified = "ParentNotified";
}

/// <summary>The follow-up call to the pet parent after discharge.</summary>
public partial record DischargeCall(DateTimeOffset DueAt, DateTimeOffset? DoneAt)
{
    public bool IsDone => DoneAt is not null;
}

public partial record Case(
    Guid Id,
    Patient Patient,
    string Procedure,
    string Veterinarian,
    string Technician,
    DateTimeOffset CreatedAt,
    CaseStatus Status,
    PreopCheck Preop,
    DateTimeOffset? InducedAt,
    DateTimeOffset? EndedAt,
    ImmutableList<VitalsReading> Readings,
    ImmutableList<DoseGiven> Doses,
    RecoveryLog Recovery,
    string? SignedBy,
    DateTimeOffset? SignedAt)
{
    public bool IsSigned => Status == CaseStatus.Signed;

    // v2 fields. All optional with defaults, so a case file written before v2 still loads. The JSON source generator
    // assigns default(T) to an init property missing from the file, so reference types fall back in the setter.

    /// <summary>"OR 1"; empty when no room is assigned.</summary>
    public string OperatingRoom { get; init => field = value ?? ""; } = "";

    public DateTimeOffset? PlannedStart { get; init; }

    public int? PlannedMinutes { get; init; }

    /// <summary>Planned preparation for this case, overriding the practice's <c>PlannedPrepMinutes</c> (gantt slot, prep start).</summary>
    public int? PlannedPrepMinutes { get; init; }

    /// <summary>Set when preparation starts (Booked → Prep).</summary>
    public DateTimeOffset? PrepStartedAt { get; init; }

    /// <summary>Set when the patient goes home (Home stage).</summary>
    public DateTimeOffset? DischargedAt { get; init; }

    public ImmutableList<FluidRun> Fluids { get; init => field = value ?? []; } = [];

    public ImmutableList<LabResult> Labs { get; init => field = value ?? []; } = [];

    public DischargeCall? DischargeCall { get; init; }

    public DateTimeOffset? FastingSince { get; init; }

    public DateTimeOffset? ParentNotifiedAt { get; init; }

    /// <summary>Corrections to the case's details since booking, oldest first (<see cref="WithDetailsFrom"/>).</summary>
    public ImmutableList<CaseCorrection> Corrections { get; init => field = value ?? []; } = [];

    /// <summary>
    /// This case with the details of <paramref name="edited"/>: the patient (name, species, breed, weight, age, owner),
    /// the procedure, the team and the schedule. Each changed detail is logged as a <see cref="CaseCorrection"/> at
    /// <paramref name="at"/>. What was recorded stays this case's: readings, doses (with the volume drawn up at the
    /// time), stage and recovery; a dose given after the correction is calculated from the corrected weight.
    /// </summary>
    public Case WithDetailsFrom(Case edited, DateTimeOffset at)
    {
        static string Dec(decimal v, string format) => v.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        string Time(DateTimeOffset? t) => t is { } v ? v.ToOffset(at.Offset).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "";
        var changes = new (string Field, string Before, string After)[]
        {
            (CaseDetail.Name, Patient.Name, edited.Patient.Name),
            (CaseDetail.Species, Patient.Species.ToString(), edited.Patient.Species.ToString()),
            (CaseDetail.Breed, Patient.Breed, edited.Patient.Breed),
            (CaseDetail.Weight, Dec(Patient.WeightKg, "0.0"), Dec(edited.Patient.WeightKg, "0.0")),
            (CaseDetail.Age, Dec(Patient.AgeYears, "0.#"), Dec(edited.Patient.AgeYears, "0.#")),
            (CaseDetail.Owner, Patient.Owner, edited.Patient.Owner),
            (CaseDetail.Procedure, Procedure, edited.Procedure),
            (CaseDetail.Veterinarian, Veterinarian, edited.Veterinarian),
            (CaseDetail.Technician, Technician, edited.Technician),
            (CaseDetail.Room, OperatingRoom, edited.OperatingRoom),
            (CaseDetail.PlannedStart, Time(PlannedStart), Time(edited.PlannedStart)),
            (CaseDetail.PlannedLength, PlannedMinutes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", edited.PlannedMinutes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""),
            (CaseDetail.FastingSince, Time(FastingSince), Time(edited.FastingSince)),
            (CaseDetail.ParentNotified, ParentNotifiedAt is null ? "no" : "yes", edited.ParentNotifiedAt is null ? "no" : "yes"),
        };
        return this with
        {
            Patient = Patient with
            {
                Name = edited.Patient.Name,
                Species = edited.Patient.Species,
                Breed = edited.Patient.Breed,
                WeightKg = edited.Patient.WeightKg,
                AgeYears = edited.Patient.AgeYears,
                Owner = edited.Patient.Owner,
            },
            Procedure = edited.Procedure,
            Veterinarian = edited.Veterinarian,
            Technician = edited.Technician,
            OperatingRoom = edited.OperatingRoom,
            PlannedStart = edited.PlannedStart,
            PlannedMinutes = edited.PlannedMinutes,
            FastingSince = edited.FastingSince,
            ParentNotifiedAt = edited.ParentNotifiedAt,
            Corrections = Corrections.AddRange(changes.Where(x => x.Before != x.After).Select(x => new CaseCorrection(at, x.Field, x.Before, x.After))),
        };
    }

    /// <summary>
    /// Marks every Ready panel named <paramref name="panel"/> as reviewed (the "Bloodwork review" task clears). Before
    /// induction it also ticks the pre-op "bloodwork reviewed" check, which is the same act.
    /// </summary>
    public Case WithLabReviewed(string panel) => this with
    {
        Labs = Labs.Select(l => l.Panel == panel && l.State == LabState.Ready ? l with { State = LabState.Reviewed } : l).ToImmutableList(),
        Preop = Status == CaseStatus.Scheduled && Labs.Any(l => l.Panel == panel && l.State == LabState.Ready) ? Preop with { BloodworkReviewed = true } : Preop,
    };

    /// <summary>
    /// A signed record is read-only except for the follow-up after it: the discharge call and the time the patient
    /// went home. True when <paramref name="changed"/> differs from this record in those fields only.
    /// </summary>
    public bool OnlyFollowUpDiffers(Case changed) =>
        this with { DischargeCall = changed.DischargeCall, DischargedAt = changed.DischargedAt } == changed;

    /// <summary>Records the discharge call to the pet parent as made at <paramref name="at"/> (the "Discharge call" task clears).</summary>
    public Case WithDischargeCallDone(DateTimeOffset at) =>
        DischargeCall is { IsDone: false } call ? this with { DischargeCall = call with { DoneAt = at } } : this;

    /// <summary>Highest fluid rate the record accepts, in mL/kg/h (shock boluses are given as doses, not runs).</summary>
    public const decimal MaxFluidRate = 30m;

    /// <summary>
    /// Starts a fluid run at <paramref name="at"/>. A run already going is stopped at the same moment, so a rate change
    /// reads as one run ending and the next starting.
    /// </summary>
    public Case WithFluidsStarted(string name, decimal rateMlKgH, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A fluid needs a name.", nameof(name));
        }
        if (rateMlKgH is <= 0 or > MaxFluidRate)
        {
            throw new ArgumentOutOfRangeException(nameof(rateMlKgH), rateMlKgH, $"Fluid rate is above 0 and at most {MaxFluidRate} mL/kg/h.");
        }
        var stopped = WithFluidsStopped(at);
        return stopped with { Fluids = stopped.Fluids.Add(new FluidRun(name.Trim(), rateMlKgH, at, null)) };
    }

    /// <summary>Stops the running fluid at <paramref name="at"/>; a case with none running is unchanged.</summary>
    public Case WithFluidsStopped(DateTimeOffset at) => Fluids.Any(f => f.IsRunning)
        ? this with { Fluids = Fluids.Select(f => f.IsRunning ? f with { StoppedAt = at } : f).ToImmutableList() }
        : this;

    public static Case New(Patient patient, string procedure, string veterinarian, string technician, DateTimeOffset now) =>
        new(Guid.NewGuid(), patient, procedure, veterinarian, technician, now, CaseStatus.Scheduled, PreopCheck.Empty,
            null, null, ImmutableList<VitalsReading>.Empty, ImmutableList<DoseGiven>.Empty, RecoveryLog.Empty, null, null);
}
