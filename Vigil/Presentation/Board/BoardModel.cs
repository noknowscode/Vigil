using System.Collections.Immutable;
using Vigil.Domain;
using Vigil.Services;

namespace Vigil.Presentation;

/// <summary>
/// Today (route Main/Today): greeting, the "Right now" hero (live case or next surgery, and the care gantt), today's
/// patient cards, "Due next" and "Where care time went". Every block reads one shared case feed, so they fail and
/// recover together; Retry raises the store's change signal (FeedView.Refresh does not recover an upstream failure).
/// </summary>
public partial record BoardModel(ICaseStore Store, IClock Clock, IPreferences Preferences, IFormulary Formulary, INavigator Navigator)
{
    private IFeed<IImmutableList<Case>> Cases { get; } = Feed.Async(async ct => (IImmutableList<Case>)await Store.GetAllAsync(ct), Store.Changed);

    private IFeed<DateTimeOffset> Now { get; } = Feed.AsyncEnumerable(Clock.Ticks);

    /// <summary>When the page model started: the minute clock steps from here.</summary>
    private DateTimeOffset Opened { get; } = Clock.Now;

    /// <summary>
    /// The clock in whole minutes from <see cref="Opened"/>: card timers, the gantt and the totals move once a minute
    /// (equal values are deduplicated, so the cards are rebuilt once a minute, not every second).
    /// </summary>
    private IFeed<DateTimeOffset> Minute => Now.Select(t => t.AddTicks(-((t - Opened).Ticks % TimeSpan.TicksPerMinute)));

    private IFeed<ImmutableList<Drug>> Drugs { get; } = Feed.Async(async ct => await Formulary.GetDrugsAsync(ct));

    private IFeed<bool> ReduceMotion { get; } = Feed.Async(_ => ValueTask.FromResult(Preferences.ReduceMotion), Preferences.Changed);

    // Re-read on the store signal and every tick, so the greeting turns over at noon and 18:00 and the recap follows the board.
    public IFeed<BoardGreeting> Greeting => Feed.Combine(Cases, Now).Select(x => BoardGreeting.From(x.Item1, x.Item2, Preferences.ReadingInterval, Preferences.ClinicianName));

    public IFeed<HeroCard> Hero => Feed
        .Combine(Cases, Now)
        .Select(x => HeroCard.From(x.Item1, x.Item2, Preferences.ReadingInterval, Preferences.PlannedPrepMinutes)!);

    public IFeed<CareTimeline> Timeline => Feed
        .Combine(Cases, Minute)
        .Select(x => CareTimeline.From(x.Item1, x.Item2, Preferences.PlannedPrepMinutes)!);

    public IState<CardFilter> Filter => State.Value(this, () => CardFilter.InClinic);

    /// <summary>Cards whose details are open (each card toggles independently; all closed on arrival).</summary>
    public IState<IImmutableSet<Guid>> Expanded => State.Value(this, () => (IImmutableSet<Guid>)ImmutableHashSet<Guid>.Empty);

    public IListFeed<FilterTab> Tabs => Feed
        .Combine(Cases, Filter, Minute)
        .Select(x => (IImmutableList<FilterTab>)Today.Tabs(x.Item1, x.Item2, x.Item3))
        .AsListFeed();

    public IListFeed<PatientCard> Cards => Feed
        .Combine(Cases, Filter, Expanded, Minute, ReduceMotion)
        .Select(x => (IImmutableList<PatientCard>)Today.Cards(x.Item1, x.Item2, x.Item3, x.Item4, x.Item5))
        .AsListFeed();

    public IFeed<CardsEmpty> CardsEmpty => Feed.Combine(Cases, Filter, Minute).Select(x => Presentation.CardsEmpty.From(x.Item1, x.Item2, x.Item3));

    private IFeed<ImmutableList<DueTask>> Due => Feed
        .Combine(Cases, Now, Drugs)
        .Select(x => DueTasks.Of(x.Item1.Where(c => Today.IsToday(c, x.Item2)), x.Item2, Preferences.ReadingInterval, x.Item3));

    public IListFeed<DueRow> DueRows => Due
        .Select(rows => (IImmutableList<DueRow>)rows.Select(DueRow.From).ToImmutableList())
        .AsListFeed();

    public IFeed<DueHeader> DueHeader => Feed
        .Combine(Due, Cases, Now)
        .Select(x => Presentation.DueHeader.From(x.Item1, x.Item2, x.Item3, Preferences.PlannedPrepMinutes));

    public IFeed<CareTimeCard> CareTime => Feed
        .Combine(Cases, Minute)
        .Select(x => CareTimeCard.From(x.Item1.Where(c => Today.IsToday(c, x.Item2)), x.Item2, Preferences.PlannedPrepMinutes)!);

    public void Retry() => Store.Reload();

    public async ValueTask ShowFilter(FilterTab tab, CancellationToken ct) => await Filter.UpdateAsync(_ => tab.Filter, ct);

    public async ValueTask ToggleDetails(PatientCard card, CancellationToken ct) =>
        await Expanded.UpdateAsync(set => set is null ? ImmutableHashSet.Create(card.CaseId) : set.Contains(card.CaseId) ? set.Remove(card.CaseId) : set.Add(card.CaseId), ct);

    /// <summary>Placeholder count for the card skeleton (the populated grid's first row).</summary>
    public IImmutableList<int> Skeleton { get; } = [1, 2, 3];

    /// <summary>Hero "Record": the live case's record.</summary>
    public async ValueTask OpenRecord(HeroCard hero, CancellationToken ct) =>
        await Navigator.NavigateRouteAsync(this, "Record", data: new CaseRef(hero.Id), cancellation: ct);

    /// <summary>A "Due next" row opens its patient's record; the row's button does the task.</summary>
    public async ValueTask OpenRow(DueRow row, CancellationToken ct) =>
        await Navigator.NavigateRouteAsync(this, "Record", data: new CaseRef(row.CaseId), cancellation: ct);

    /// <summary>A "Due next" action: the record, the dose screen or sign-off for that case.</summary>
    public async ValueTask OpenTask(DueRow row, CancellationToken ct) =>
        await Navigator.NavigateRouteAsync(this, row.Route, data: row.Pane is { } pane ? new CaseRefPane(row.CaseId, pane) : new CaseRef(row.CaseId), cancellation: ct);

    /// <summary>Next surgery: "Start prep" moves the booked case to Prep (stamps <see cref="Case.PrepStartedAt"/>).</summary>
    public async ValueTask StartPrep(HeroCard hero, CancellationToken ct) =>
        await Store.UpdateAsync(hero.Id, c => c.PrepStartedAt is null ? c with { PrepStartedAt = Clock.Now } : c, ct);

    /// <summary>Opens the New case sheet; on Create, opens the new record with Today behind it (Back returns here).</summary>
    public async ValueTask NewCase(CancellationToken ct)
    {
        var result = await Navigator.NavigateRouteForResultAsync<CaseRef>(this, "!NewCase", cancellation: ct).AsResult();
        if (result.SomeOrDefault() is CaseRef created)
        {
            await Navigator.NavigateRouteAsync(this, "Record", data: created, cancellation: ct);
        }
    }

    public async ValueTask OpenAgenda(CancellationToken ct) =>
        await Navigator.NavigateRouteAsync(this, "Agenda", cancellation: ct);

    public async ValueTask OpenPatients(CancellationToken ct) =>
        await Navigator.NavigateRouteAsync(this, "Patients", cancellation: ct);
}
