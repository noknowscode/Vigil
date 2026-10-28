namespace Vigil.Presentation;

/// <summary>
/// Patients (sidebar): every case on this device, newest first, as Today's patient cards (<see cref="PatientCard"/>,
/// drawn by PatientCardView). Each card's details toggle independently, as on Today.
/// </summary>
public partial record PatientsModel(ICaseStore Store, IClock Clock, IPreferences Preferences)
{
    private IFeed<IImmutableList<Case>> Cases { get; } = Feed.Async(async ct => (IImmutableList<Case>)await Store.GetAllAsync(ct), Store.Changed);

    private IFeed<DateTimeOffset> Minute { get; } = Feed.AsyncEnumerable(Clock.Ticks).Select(t => new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Offset));

    private IFeed<bool> ReduceMotion { get; } = Feed.Async(_ => ValueTask.FromResult(Preferences.ReduceMotion), Preferences.Changed);

    /// <summary>Cards whose details are open (all closed on arrival).</summary>
    public IState<IImmutableSet<Guid>> Expanded => State.Value(this, () => (IImmutableSet<Guid>)ImmutableHashSet<Guid>.Empty);

    public IListFeed<PatientCard> All => Feed
        .Combine(Cases, Expanded, Minute, ReduceMotion)
        .Select(x => (IImmutableList<PatientCard>)x.Item1
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => PatientCard.From(c, x.Item3, x.Item2.Contains(c.Id), x.Item4))
            .ToImmutableList())
        .AsListFeed();

    public IFeed<string> Summary => Cases.Select(cases => Loc.F("Patients_Summary", "{0} in clinic · {1} on this device", ShellSection.InClinic(cases), cases.Count));

    public async ValueTask ToggleDetails(PatientCard card, CancellationToken ct) =>
        await Expanded.UpdateAsync(set => set is null ? ImmutableHashSet.Create(card.CaseId) : set.Contains(card.CaseId) ? set.Remove(card.CaseId) : set.Add(card.CaseId), ct);

    public void Retry() => Store.Reload();
}
