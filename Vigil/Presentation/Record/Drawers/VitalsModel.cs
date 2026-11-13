namespace Vigil.Presentation;

/// <summary>
/// The Record observations drawer (route "!Monitor"): the vitals pad pre-filled from the last reading, its range flags,
/// the steppers and Record reading, with the due countdown on the button.
/// </summary>
public partial record VitalsModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, IFormulary Formulary, INavigator Navigator, ILogger<VitalsModel> Logger)
    : CaseDrawerModel(Ref, Store, Clock, Preferences, Navigator, Logger)
{
    /// <summary>The due countdown on Record reading ("next 2:41", "overdue 0:40"), moving with the clock.</summary>
    public IFeed<CaseTimers> Timers => TimersOver(Formulary);

    /// <summary>The pad, pre-filled from the last reading. Single writer: the pad; reset by RecordReading.</summary>
    public IState<VitalsDraft> Draft => State.Async(this, async ct =>
        VitalsDraft.From((await Store.GetAsync(Ref.Id, ct))?.Readings.LastOrDefault()));

    public IFeed<DraftFlags> Flags => Feed.Combine(Draft, Source).Select(x => DraftFlags.From(x.Item1, x.Item2.Patient.Species));

    public async ValueTask Step(string arg, CancellationToken ct) =>
        await Draft.UpdateAsync(d => (d ?? VitalsDraft.Empty).Step(arg), ct);

    /// <summary>Saves the pad as a reading (the case must be under anesthesia); the next one starts from it.</summary>
    public async ValueTask RecordReading(VitalsDraft draft, CancellationToken ct)
    {
        var errors = draft.Validate();
        if (errors.Count > 0)
        {
            await ShowErrorAsync(string.Join(" ", errors), ct);
            return;
        }
        var reading = draft.ToReading(Clock.Now);
        var saved = await SaveAsync(c => c.Status == CaseStatus.Anesthetized
            ? c with { Readings = c.Readings.Add(reading) }
            : throw new CaseRuleException(Loc.T("Error_InduceFirst", "Induce before recording vitals.")), ct);
        if (saved)
        {
            // Next reading starts from this one: an unchanged patient is a single tap.
            await Draft.UpdateAsync(_ => VitalsDraft.From(reading), ct);
        }
    }
}
