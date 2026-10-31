namespace Vigil.Presentation;

/// <summary>Only factual, saved milestones. Signing a record never means cleared for discharge.</summary>
public partial record CareMilestone(string Label, string Detail, string Time, bool Done)
{
    public string Marker => Done ? "✓" : "·";
}
public partial record ParentSnapshot(string Name, string Breed, string? Photo, string Status, string Description, string Updated, string Next, string Procedure, string Team, IImmutableList<CareMilestone> Milestones)
{
    public bool HasNoPhoto => string.IsNullOrEmpty(Photo);
    public bool HasPhoto => !HasNoPhoto;
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1];
    public static ParentSnapshot From(Case c)
    {
        var latest = new DateTimeOffset?[] { c.CreatedAt, c.InducedAt, c.EndedAt, c.Recovery.ExtubatedAt, c.Recovery.SternalAt, c.SignedAt }.Where(x => x.HasValue).Max()!.Value;
        var pending = Loc.T("Parent_Pending", "Pending");
        return new(c.Patient.Name, c.Patient.Breed, PatientMedia.Photo(c.Patient),
            c.Status switch { CaseStatus.Anesthetized => Loc.T("Parent_StatusSurgery", "In surgery"), CaseStatus.Recovery => Loc.T("Parent_StatusRecovery", "In recovery"), CaseStatus.Signed => Loc.T("Parent_StatusComplete", "Record complete"), _ => Loc.T("Parent_StatusReady", "Getting ready") },
            c.Status switch { CaseStatus.Anesthetized => Loc.T("Parent_DescSurgery", "The team has recorded the start of anaesthesia. The next milestone will appear when recovery is recorded."), CaseStatus.Recovery => Loc.T("Parent_DescRecovery", "Anaesthesia has ended and the team is recording recovery milestones."), CaseStatus.Signed => Loc.T("Parent_DescComplete", "The team has completed the anaesthesia record. Please wait for their collection instructions."), _ => Loc.T("Parent_DescReady", "Your pet's visit record is open. The team is working through the preparation checklist.") },
            Loc.F("Parent_Updated", "Last milestone · {0:dd MMM, HH:mm}", latest),
            c.Status switch { CaseStatus.Anesthetized => Loc.T("Parent_NextSurgery", "The team will update you when recovery begins."), CaseStatus.Recovery => Loc.T("Parent_NextRecovery", "Wait for the team's collection instructions. Recovery timing is individual."), CaseStatus.Signed => Loc.T("Parent_NextComplete", "The clinic will confirm when your pet is ready to go home."), _ => Loc.T("Parent_NextReady", "The team will record the start of anaesthesia after preparation.") },
            c.Procedure, $"{c.Veterinarian} · {c.Technician}",
            new[] {
                new CareMilestone(Loc.T("Parent_MsOpened", "Visit opened"), Loc.T("Parent_MsOpenedDetail", "Your pet's visit is open."), $"{c.CreatedAt:HH:mm}", true),
                new CareMilestone(Loc.T("Parent_MsPrep", "Preparation"), c.Preop.IsComplete ? Loc.T("Parent_MsPrepDone", "Pre-op checklist complete.") : Loc.T("Parent_MsPrepPending", "Pre-op checks are in progress."), c.Preop.IsComplete ? Loc.T("Parent_Complete", "Complete") : pending, c.Preop.IsComplete),
                new CareMilestone(Loc.T("Parent_MsAnaesthesia", "Anaesthesia"), c.InducedAt is not null ? Loc.T("Parent_MsAnaesthesiaDone", "Start recorded by the care team.") : Loc.T("Parent_MsAnaesthesiaPending", "The team will record the start."), c.InducedAt is { } i ? $"{i:HH:mm}" : pending, c.InducedAt is not null),
                new CareMilestone(Loc.T("Parent_MsRecovery", "Recovery"), c.EndedAt is not null ? Loc.T("Parent_MsRecoveryDone", "Anaesthesia ended; recovery started.") : Loc.T("Parent_MsRecoveryPending", "The team will record recovery here."), c.EndedAt is { } e ? $"{e:HH:mm}" : pending, c.EndedAt is not null),
                new CareMilestone(Loc.T("Parent_StatusComplete", "Record complete"), Loc.T("Parent_MsCompleteDetail", "Collection is confirmed separately."), c.SignedAt is { } s ? $"{s:HH:mm}" : pending, c.SignedAt is not null)
            }.ToImmutableList());
    }
}

public partial record ParentPreviewModel(CaseRef Ref, ICaseStore Store)
{
    public IFeed<ParentSnapshot> Patient => Feed<ParentSnapshot>.Async(async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(ParentSnapshot.From(c)) : Option.None<ParentSnapshot>(), Store.Changed);
    public void Retry() => Store.Reload();
}
