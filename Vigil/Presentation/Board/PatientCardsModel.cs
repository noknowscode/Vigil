namespace Vigil.Presentation;

/// <summary>
/// The base of the pages that show patient cards (Today and Patients): every case, reloaded on each store change, the
/// motion preference, which cards have their details open (each toggles on its own, all closed on arrival) and Retry.
/// MVUX generates its view model, which each page's generated view model extends, so PatientCardView's Show details
/// (the page's ToggleDetails) works on either page.
/// </summary>
public abstract partial record PatientCardsModel
{
    private readonly ICaseStore _store;

    protected PatientCardsModel(ICaseStore store, IPreferences preferences)
    {
        _store = store;
        Cases = Feed.Async(async ct => (IImmutableList<Case>)await store.GetAllAsync(ct), store.Changed);
        ReduceMotion = Feed.Async(_ => ValueTask.FromResult(preferences.ReduceMotion), preferences.Changed);
        Expanded = State.Value(this, () => (IImmutableSet<Guid>)ImmutableHashSet<Guid>.Empty);
    }

    /// <summary>Show details / Hide details on a card.</summary>
    public async ValueTask ToggleDetails(PatientCard card, CancellationToken ct) =>
        await Expanded.UpdateAsync(open => open is null ? ImmutableHashSet.Create(card.CaseId) : open.Contains(card.CaseId) ? open.Remove(card.CaseId) : open.Add(card.CaseId), ct);

    /// <summary>Raises the store's change signal: FeedView.Refresh does not recover an upstream failure.</summary>
    public void Retry() => _store.Reload();

    protected IFeed<IImmutableList<Case>> Cases { get; }

    /// <summary>In-app setting OR'ed with the platform's: the cards skip their motion.</summary>
    protected IFeed<bool> ReduceMotion { get; }

    /// <summary>Cards whose details are open.</summary>
    protected IState<IImmutableSet<Guid>> Expanded { get; }
}
