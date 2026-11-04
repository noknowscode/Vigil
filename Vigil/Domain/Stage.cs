namespace Vigil.Domain;

/// <summary>The five steps of a patient's day, as the card stepper shows them.</summary>
public enum CareStage { Booked, Prep, Surgery, Recovery, Home }

public static class Stage
{
    public const int Count = 5;

    /// <summary>Home once signed or discharged; otherwise the saved status, with Prep once preparation has started.</summary>
    public static CareStage Of(Case c) => c switch
    {
        { Status: CaseStatus.Signed } or { DischargedAt: not null } => CareStage.Home,
        { Status: CaseStatus.Recovery } => CareStage.Recovery,
        { Status: CaseStatus.Anesthetized } => CareStage.Surgery,
        { PrepStartedAt: not null, InducedAt: null } => CareStage.Prep,
        _ => CareStage.Booked,
    };

    /// <summary>1 for Booked … 5 for Home.</summary>
    public static int Number(CareStage s) => (int)s + 1;
}
