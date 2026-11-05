namespace Vigil.Presentation;

/// <summary>
/// Administer dose for one case, behind the Redose page (route Main/Redose, from a Due next "Prepare" task) and the
/// record's Dose drawer: pick a drug, set mg/kg (pre-filled with the formulary default), read the volume, give. A dose
/// outside the reference range asks first, inline (a message dialog opened from the sheet dismissed the sheet itself
/// and lost the entry; see DECISIONS D4). Give saves and goes back.
/// </summary>
public partial record DoseModel(CaseRef Ref, ICaseStore Store, IFormulary Formulary, IClock Clock, INavigator Navigator, DoseFeedback Feedback, ILogger<DoseModel> Logger)
{
    private IFeed<Case> Source { get; } = Feed<Case>.Async(
        async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(c) : Option.None<Case>(), Store.Changed);

    public IFeed<string> Patient => Source.Select(c => $"{c.Patient.Name} · {Loc.Species(c.Patient.Species)} · {c.Patient.WeightKg:0.0} kg");

    /// <summary>The picked drug; picking one fills mg/kg and the route with its defaults.</summary>
    public IState<Drug> Selected => State<Drug>.Empty(this)
        .ForEach(async (drug, ct) =>
        {
            if (drug is not null)
            {
                await MgPerKg.UpdateAsync(_ => drug.DefaultMgPerKg.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), ct);
                await RouteIndex.UpdateAsync(_ => (int)drug.DefaultRoute, ct);
            }
        });

    public IListFeed<Drug> Drugs => ListFeed.Async(async ct => (IImmutableList<Drug>)await Formulary.GetDrugsAsync(ct)).Selection(Selected);

    /// <summary>mg/kg as typed. Text so "0." is never rejected mid-entry; a change withdraws the confirmation.</summary>
    public IState<string> MgPerKg => State.Value(this, () => "")
        .ForEach(async (_, ct) => await Confirming.UpdateAsync(_ => false, ct));

    public IState<int> RouteIndex => State.Value(this, () => 0);

    /// <summary>None until a drug is picked; invalid mg/kg stays None and the view says why.</summary>
    public IFeed<DoseCalc> Calc => Feed.Combine(Source, Selected, MgPerKg)
        .Select(x => VitalsDraft.ParseDec(x.Item3) is decimal mg && mg > 0
            ? Dosing.Calculate(x.Item1.Patient.Species, x.Item1.Patient.WeightKg, x.Item2, mg)
            : null!);

    public IState<string> Error => State.Value(this, () => "");

    /// <summary>Set by Give when the dose is outside the reference range: the view shows the inline confirmation.</summary>
    public IState<bool> Confirming => State.Value(this, () => false);

    public IFeed<bool> NotConfirming => Confirming.Select(c => !c);

    public IFeed<string> ConfirmTitle => Calc.Select(c => Loc.F("Dose_ConfirmTitle", "Give {0:0.###} mg/kg?", c.MgPerKg));

    public IFeed<string> ConfirmText => Feed.Combine(Calc, Selected)
        .Select(x => Loc.F("Dose_ConfirmText", "The reference range for {0} is {1}. {2:0.###} mg/kg is {3}.", x.Item2.Name, x.Item1.ReferenceText, x.Item1.MgPerKg, x.Item1.VolumeText));

    /// <summary>Gives the dose, or asks first when it is outside the reference range.</summary>
    public async ValueTask Give(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct)
    {
        if (calc.IsOutOfRange)
        {
            await Confirming.UpdateAsync(_ => true, ct);
            return;
        }
        await SaveAndCloseAsync(calc, selected, routeIndex, ct);
    }

    /// <summary>"Give anyway": the out-of-range dose, confirmed.</summary>
    public async ValueTask GiveAnyway(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct) =>
        await SaveAndCloseAsync(calc, selected, routeIndex, ct);

    /// <summary>"Change dose": back to the entry, the default answer to the confirmation.</summary>
    public async ValueTask ChangeDose(CancellationToken ct) => await Confirming.UpdateAsync(_ => false, ct);

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);

    private async ValueTask SaveAndCloseAsync(DoseCalc calc, Drug drug, int routeIndex, CancellationToken ct)
    {
        var dose = new DoseGiven(Guid.NewGuid(), Clock.Now, drug.Id, drug.Name, calc.MgPerKg, calc.MgPerMl, calc.VolumeMl,
            (DoseRoute)Math.Clamp(routeIndex, 0, 3), calc.IsOutOfRange);
        try
        {
            await Store.UpdateAsync(Ref.Id, c => c with { Doses = c.Doses.Add(dose) }, ct);
        }
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            Logger.LogWarning(ex, "Saving a dose for case {CaseId} failed.", Ref.Id);
            await Error.UpdateAsync(_ => Loc.F("Error_NotSaved", "Not saved: {0}", StoreErrors.Describe(ex)), ct);
            return;
        }
        Feedback.Recorded(Ref.Id);
        await Error.UpdateAsync(_ => "", ct);
        await Confirming.UpdateAsync(_ => false, ct);
        await Navigator.NavigateBackAsync(this, cancellation: ct);
    }
}
