namespace Vigil.Presentation;

/// <summary>
/// Dose entry, shared by the Dose page (<see cref="DoseModel"/>) and the record's dose pane (<see cref="RecordModel"/>):
/// pick a drug, set mg/kg (pre-filled with the formulary default), read the volume, give. A dose outside the reference
/// range asks first, inline (a message dialog opened from the sheet dismissed the sheet itself and lost the entry; see
/// DECISIONS D4). The states belong to the owning model, which exposes them under its own names for its view.
/// </summary>
public sealed class DoseEntry
{
    private readonly Guid _caseId;
    private readonly ICaseStore _store;
    private readonly IClock _clock;
    private readonly DoseFeedback _feedback;
    private readonly ILogger _logger;

    public DoseEntry(object owner, Guid caseId, IFeed<Case> source, ICaseStore store, IFormulary formulary, IClock clock, DoseFeedback feedback, ILogger logger)
    {
        _caseId = caseId;
        _store = store;
        _clock = clock;
        _feedback = feedback;
        _logger = logger;

        Confirming = State.Value(owner, () => false);
        NotConfirming = Confirming.Select(c => !c);
        RouteIndex = State.Value(owner, () => 0);
        MgPerKg = State.Value(owner, () => "")
            .ForEach(async (_, ct) => await Confirming.UpdateAsync(_ => false, ct));
        Selected = State<Drug>.Empty(owner)
            .ForEach(async (drug, ct) =>
            {
                if (drug is not null)
                {
                    await MgPerKg.UpdateAsync(_ => drug.DefaultMgPerKg.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), ct);
                    await RouteIndex.UpdateAsync(_ => (int)drug.DefaultRoute, ct);
                }
            });
        Drugs = ListFeed.Async(async ct => (IImmutableList<Drug>)await formulary.GetDrugsAsync(ct)).Selection(Selected);
        Calc = Feed.Combine(source, Selected, MgPerKg)
            .Select(x => VitalsDraft.ParseDec(x.Item3) is decimal mg && mg > 0
                ? Dosing.Calculate(x.Item1.Patient.Species, x.Item1.Patient.WeightKg, x.Item2, mg)
                : null!);
        Error = State.Value(owner, () => "");
        ConfirmTitle = Calc.Select(c => Loc.F("Dose_ConfirmTitle", "Give {0:0.###} mg/kg?", c.MgPerKg));
        ConfirmText = Feed.Combine(Calc, Selected)
            .Select(x => Loc.F("Dose_ConfirmText", "The reference range for {0} is {1}. {2:0.###} mg/kg is {3}.", x.Item2.Name, x.Item1.ReferenceText, x.Item1.MgPerKg, x.Item1.VolumeText));
    }

    public IState<Drug> Selected { get; }

    public IListFeed<Drug> Drugs { get; }

    /// <summary>mg/kg as typed. Text so "0." is never rejected mid-entry.</summary>
    public IState<string> MgPerKg { get; }

    public IState<int> RouteIndex { get; }

    /// <summary>None until a drug is picked; invalid mg/kg stays None and the view says why.</summary>
    public IFeed<DoseCalc> Calc { get; }

    public IState<string> Error { get; }

    /// <summary>Set by Give when the dose is outside the reference range: the view shows the inline confirmation.</summary>
    public IState<bool> Confirming { get; }

    public IFeed<bool> NotConfirming { get; }

    public IFeed<string> ConfirmTitle { get; }

    public IFeed<string> ConfirmText { get; }

    /// <summary>Gives the dose, or asks first when it is outside the reference range. True once the dose is saved.</summary>
    public async ValueTask<bool> GiveAsync(DoseCalc calc, Drug drug, int routeIndex, CancellationToken ct)
    {
        if (calc.IsOutOfRange)
        {
            await Confirming.UpdateAsync(_ => true, ct);
            return false;
        }
        return await SaveAsync(calc, drug, routeIndex, ct);
    }

    /// <summary>"Give anyway": the out-of-range dose, confirmed. True once the dose is saved.</summary>
    public ValueTask<bool> GiveAnywayAsync(DoseCalc calc, Drug drug, int routeIndex, CancellationToken ct) =>
        SaveAsync(calc, drug, routeIndex, ct);

    /// <summary>"Change dose": back to the entry, the default answer to the confirmation.</summary>
    public async ValueTask ChangeAsync(CancellationToken ct) => await Confirming.UpdateAsync(_ => false, ct);

    private async ValueTask<bool> SaveAsync(DoseCalc calc, Drug drug, int routeIndex, CancellationToken ct)
    {
        var dose = new DoseGiven(Guid.NewGuid(), _clock.Now, drug.Id, drug.Name, calc.MgPerKg, calc.MgPerMl, calc.VolumeMl,
            (DoseRoute)Math.Clamp(routeIndex, 0, 3), calc.IsOutOfRange);
        try
        {
            await _store.UpdateAsync(_caseId, c => c with { Doses = c.Doses.Add(dose) }, ct);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException)
        {
            _logger.LogWarning(ex, "Saving a dose for case {CaseId} failed.", _caseId);
            await Error.UpdateAsync(_ => Loc.F("Error_NotSaved", "Not saved: {0}", StoreErrors.Describe(ex)), ct);
            return false;
        }
        _feedback.Recorded(_caseId);
        await Error.UpdateAsync(_ => "", ct);
        await Confirming.UpdateAsync(_ => false, ct);
        return true;
    }
}
