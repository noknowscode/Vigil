using Vigil.Domain;

namespace Vigil.Presentation;

/// <summary>A case row on the board. Inherits <see cref="CaseRef"/> so a tap navigates with the row as data.</summary>
public partial record CaseSummary(
    Guid Id,
    string Name,
    string Signalment,
    string Procedure,
    string Team,
    CaseStatus Status,
    string StatusText,
    string TimeText) : CaseRef(Id)
{
    public string SpeciesText { get; init; } = "";
    public string Breed { get; init; } = "";
    public string Weight { get; init; } = "";
    public string Age { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Asa { get; init; } = "";
    public string WeightDate { get; init; } = "";
    public string? Photo { get; init; }
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0]));
    public bool HasNoPhoto => string.IsNullOrEmpty(Photo);
    public bool IsActive => Status is CaseStatus.Anesthetized or CaseStatus.Recovery;
    public string AccessibleName => $"{Name}, {StatusText}. {Procedure}. {TimeText}";

    public static CaseSummary From(Case c, DateTimeOffset now)
    {
        var p = c.Patient;
        var time = c.Status switch
        {
            CaseStatus.Anesthetized => Loc.F("Board_UnderTime", "Under {0}", Schedule.HoursMinutes(Schedule.Elapsed(c, now))),
            CaseStatus.Recovery => c.Recovery.ExtubatedAt is DateTimeOffset x ? Loc.F("Board_ExtubatedTime", "Extubated {0:HH:mm}", x) : Loc.F("Board_EndedTime", "Ended {0:HH:mm}", c.EndedAt),
            CaseStatus.Signed => Loc.F("Board_SignedTime", "Signed {0:HH:mm}", c.SignedAt),
            _ => Loc.F("Board_AddedTime", "Added {0:HH:mm}", c.CreatedAt),
        };
        return new CaseSummary(
            c.Id,
            p.Name,
            $"{p.SpeciesAndBreed} · {p.WeightKg:0.0} kg",
            c.Procedure,
            $"{c.Veterinarian} · {c.Technician}",
            c.Status,
            StatusLabel(c.Status),
            time)
        {
            Photo = PatientMedia.Photo(p), SpeciesText = Loc.Species(p.Species),
            Breed = string.IsNullOrWhiteSpace(p.Breed) ? Loc.T("Patient_NotRecorded", "Not recorded") : p.Breed,
            Weight = $"{p.WeightKg:0.0} kg", Age = p.AgeYears == 1 ? Loc.T("Patient_OneYear", "1 year") : Loc.F("Patient_AgeYears", "{0:0.#} years", p.AgeYears),
            Owner = string.IsNullOrWhiteSpace(p.Owner) ? Loc.T("Patient_NotRecorded", "Not recorded") : p.Owner,
            Asa = c.Preop.AsaLabel,
            WeightDate = Loc.F("Patient_VisitWeight", "Weight recorded with visit on {0:dd MMM yyyy}", c.CreatedAt)
        };
    }

    public static string StatusLabel(CaseStatus s) => s switch
    {
        CaseStatus.Anesthetized => Loc.T("Status_Anesthetized", "Under anesthesia"),
        CaseStatus.Recovery => Loc.T("Status_Recovery", "Recovery"),
        CaseStatus.Signed => Loc.T("Status_Signed", "Signed"),
        _ => Loc.T("Status_Scheduled", "Scheduled"),
    };
}
