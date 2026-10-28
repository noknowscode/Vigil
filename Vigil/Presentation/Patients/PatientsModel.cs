namespace Vigil.Presentation;

/// <summary>
/// Patients (sidebar): every case on this device, newest first, as the board's patient cards. Minimal until a
/// dedicated patient list is designed; it reuses <see cref="CaseSummary"/> and the shared card template.
/// </summary>
public partial record PatientsModel(ICaseStore Store, IClock Clock)
{
    private IFeed<IImmutableList<Case>> Cases { get; } = Feed.Async(async ct => (IImmutableList<Case>)await Store.GetAllAsync(ct), Store.Changed);

    public IListFeed<CaseSummary> All => Cases
        .Select(cases => (IImmutableList<CaseSummary>)cases
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => CaseSummary.From(c, Clock.Now))
            .ToImmutableList())
        .AsListFeed();

    public IFeed<string> Summary => Cases.Select(cases => Loc.F("Patients_Summary", "{0} in clinic · {1} on this device", ShellSection.InClinic(cases), cases.Count));

    public void Retry() => Store.Reload();
}
