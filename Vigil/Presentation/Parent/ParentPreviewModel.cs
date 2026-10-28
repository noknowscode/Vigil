namespace Vigil.Presentation;

/// <summary>Only factual, saved milestones. Signing a record never means cleared for discharge.</summary>
public partial record CareMilestone(string Label, string Detail, string Time, bool Done)
{
    public string Marker => Done ? "✓" : "·";
}
public partial record ParentSnapshot(string Name, string Breed, string? Photo, string Status, string Description, string Updated, string Next, string Procedure, string Team, IImmutableList<CareMilestone> Milestones)
{
    public bool HasNoPhoto => string.IsNullOrEmpty(Photo);
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1];
    public static ParentSnapshot From(Case c)
    {
        var latest = new DateTimeOffset?[] { c.CreatedAt, c.InducedAt, c.EndedAt, c.Recovery.ExtubatedAt, c.Recovery.SternalAt, c.SignedAt }.Where(x => x.HasValue).Max()!.Value;
        return new(c.Patient.Name, c.Patient.Breed, PatientMedia.Photo(c.Patient),
            c.Status switch { CaseStatus.Anesthetized => "In surgery", CaseStatus.Recovery => "In recovery", CaseStatus.Signed => "Record complete", _ => "Getting ready" },
            c.Status switch { CaseStatus.Anesthetized => "The team has recorded the start of anaesthesia. The next milestone will appear when recovery is recorded.", CaseStatus.Recovery => "Anaesthesia has ended and the team is recording recovery milestones.", CaseStatus.Signed => "The team has completed the anaesthesia record. Please wait for their collection instructions.", _ => "Your pet's visit record is open. The team is working through the preparation checklist." },
            $"Last milestone · {latest:dd MMM, HH:mm}",
            c.Status switch { CaseStatus.Anesthetized => "The team will update you when recovery begins.", CaseStatus.Recovery => "Wait for the team's collection instructions. Recovery timing is individual.", CaseStatus.Signed => "The clinic will confirm when your pet is ready to go home.", _ => "The team will record the start of anaesthesia after preparation." },
            c.Procedure, $"{c.Veterinarian} · {c.Technician}",
            new[] {
                new CareMilestone("Visit opened", "Your pet's visit is open.", $"{c.CreatedAt:HH:mm}", true),
                new CareMilestone("Preparation", c.Preop.IsComplete ? "Pre-op checklist complete." : "Pre-op checks are in progress.", c.Preop.IsComplete ? "Complete" : "Pending", c.Preop.IsComplete),
                new CareMilestone("Anaesthesia", c.InducedAt is not null ? "Start recorded by the care team." : "The team will record the start.", c.InducedAt is { } i ? $"{i:HH:mm}" : "Pending", c.InducedAt is not null),
                new CareMilestone("Recovery", c.EndedAt is not null ? "Anaesthesia ended; recovery started." : "The team will record recovery here.", c.EndedAt is { } e ? $"{e:HH:mm}" : "Pending", c.EndedAt is not null),
                new CareMilestone("Record complete", "Collection is confirmed separately.", c.SignedAt is { } s ? $"{s:HH:mm}" : "Pending", c.SignedAt is not null)
            }.ToImmutableList());
    }
}

public partial record ParentPreviewModel(CaseRef Ref, ICaseStore Store)
{
    public IFeed<ParentSnapshot> Patient => Feed<ParentSnapshot>.Async(async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(ParentSnapshot.From(c)) : Option.None<ParentSnapshot>(), Store.Changed);
    public void Retry() => Store.Reload();
}
