using System.Collections.Immutable;

namespace Vigil.Domain;

public enum Species { Canine, Feline, Other }

public enum CaseStatus { Scheduled, Anesthetized, Recovery, Signed }

public enum Plane { Light, Surgical, Deep }

public enum DoseRoute { IV, IM, SC, PO }

public partial record CaseRef(Guid Id);

public partial record Patient(string Name, Species Species, string Breed, decimal WeightKg, decimal AgeYears, string Owner)
{
    /// <summary>Bundled image identifiers for fictional demo records; absent on existing records.</summary>
    public string? PhotoAsset { get; init; }
    public string? HeroAsset { get; init; }

    /// <summary>"Canine · Labrador retriever", skipping a blank breed.</summary>
    public string SpeciesAndBreed => string.IsNullOrWhiteSpace(Breed) ? Loc.Species(Species) : $"{Loc.Species(Species)} · {Breed}";
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

    public string AsaLabel => Asa is >= 1 and <= 5 ? $"ASA {Roman(Asa)}{(AsaEmergency ? "E" : "")}" : Loc.T("Asa_NotSet", "ASA —");

    public static string Roman(int asa) => asa switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "" };
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

    public static Case New(Patient patient, string procedure, string veterinarian, string technician, DateTimeOffset now) =>
        new(Guid.NewGuid(), patient, procedure, veterinarian, technician, now, CaseStatus.Scheduled, PreopCheck.Empty,
            null, null, ImmutableList<VitalsReading>.Empty, ImmutableList<DoseGiven>.Empty, RecoveryLog.Empty, null, null);
}
