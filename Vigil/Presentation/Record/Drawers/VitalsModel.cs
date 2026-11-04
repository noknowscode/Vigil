namespace Vigil.Presentation;

/// <summary>
/// The Record observations drawer (route "!Monitor"): the vitals pad pre-filled from the last reading, its range flags,
/// the steppers and Record reading, with the due countdown on the button.
/// </summary>
public partial record VitalsModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, IFormulary Formulary, INavigator Navigator, ILogger<VitalsModel> Logger)
{
    private CaseDrawer Drawer => _drawer ??= new(this, Ref, Store, Clock, Preferences, Navigator, Logger);
    private CaseDrawer? _drawer;

    public IFeed<CaseView> Case => Drawer.View;
    public IState<string> SaveError => Drawer.Error;
    public IFeed<bool> HasSaveError => Drawer.HasError;
    public async ValueTask DismissError(CancellationToken ct) => await Drawer.DismissErrorAsync(ct);
    public void Retry() => Drawer.Retry();
    public async ValueTask Close(CancellationToken ct) => await Drawer.CloseAsync(ct);

    private IFeed<DateTimeOffset> Now { get; } = Feed.AsyncEnumerable(Clock.Ticks);

    private IFeed<IImmutableList<Drug>> Drugs { get; } = Feed.Async(async ct => (IImmutableList<Drug>)await Formulary.GetDrugsAsync(ct));

    /// <summary>The due countdown on Record reading ("next 2:41", "overdue 0:40"), moving with the clock.</summary>
    public IFeed<CaseTimers> Timers => Feed.Combine(Drawer.Source, Now, Drugs).Select(x =>
        CaseTimers.From(x.Item1, x.Item2, Preferences.ReadingInterval, x.Item3));

    /// <summary>The pad, pre-filled from the last reading. Single writer: the pad; reset by RecordReading.</summary>
    public IState<VitalsDraft> Draft => State.Async(this, async ct =>
        VitalsDraft.From((await Store.GetAsync(Ref.Id, ct))?.Readings.LastOrDefault()));

    public IFeed<DraftFlags> Flags => Feed.Combine(Draft, Drawer.Source).Select(x => DraftFlags.From(x.Item1, x.Item2.Patient.Species));

    public async ValueTask Step(string arg, CancellationToken ct) =>
        await Draft.UpdateAsync(d => (d ?? VitalsDraft.Empty).Step(arg), ct);

    /// <summary>Saves the pad as a reading (the case must be under anesthesia); the next one starts from it.</summary>
    public async ValueTask RecordReading(VitalsDraft draft, CancellationToken ct)
    {
        var errors = draft.Validate();
        if (errors.Count > 0)
        {
            await Drawer.ShowErrorAsync(string.Join(" ", errors), ct);
            return;
        }
        var reading = draft.ToReading(Clock.Now);
        var saved = await Drawer.SaveAsync(c => c.Status == CaseStatus.Anesthetized
            ? c with { Readings = c.Readings.Add(reading) }
            : throw new CaseRuleException(Loc.T("Error_InduceFirst", "Induce before recording vitals.")), ct);
        if (saved)
        {
            // Next reading starts from this one: an unchanged patient is a single tap.
            await Draft.UpdateAsync(_ => VitalsDraft.From(reading), ct);
        }
    }
}
