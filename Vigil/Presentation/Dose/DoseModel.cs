namespace Vigil.Presentation;

/// <summary>The dose sheet: pick a drug, set mg/kg (pre-filled with the formulary default), read the volume, give.</summary>
public partial record DoseModel(CaseRef Ref, ICaseStore Store, IFormulary Formulary, IClock Clock, INavigator Navigator, DoseFeedback Feedback)
{
    private IFeed<Case> Source { get; } = Feed<Case>.Async(
        async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(c) : Option.None<Case>(), Store.Changed);

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

    /// <summary>mg/kg as typed. Text so "0." is never rejected mid-entry.</summary>
    public IState<string> MgPerKg => State.Value(this, () => "")
        .ForEach(async (_, ct) => await Confirming.UpdateAsync(_ => false, ct));

    public IState<int> RouteIndex => State.Value(this, () => 0);

    public IFeed<string> Patient => Source.Select(c => $"{c.Patient.Name} · {Loc.Species(c.Patient.Species)} · {c.Patient.WeightKg:0.0} kg");

    /// <summary>None until a drug is picked; invalid mg/kg stays None and the sheet says why.</summary>
    public IFeed<DoseCalc> Calc => Feed.Combine(Source, Selected, MgPerKg)
        .Select(x => VitalsDraft.ParseDec(x.Item3) is decimal mg && mg > 0
            ? Dosing.Calculate(x.Item1.Patient.Species, x.Item1.Patient.WeightKg, x.Item2, mg)
            : null!);

    public IState<string> Error => State.Value(this, () => "");

    public IFeed<bool> NotConfirming => Confirming.Select(c => !c);

    public IFeed<string> ConfirmTitle => Calc.Select(c => Loc.F("Dose_ConfirmTitle", "Give {0:0.###} mg/kg?", c.MgPerKg));

    public IFeed<string> ConfirmText => Feed.Combine(Calc, Selected)
        .Select(x => Loc.F("Dose_ConfirmText", "The reference range for {0} is {1}. {2:0.###} mg/kg is {3}.", x.Item2.Name, x.Item1.ReferenceText, x.Item1.MgPerKg, x.Item1.VolumeText));

    /// <summary>
    /// Set by Give when the dose is outside the reference range: the sheet shows an inline confirmation.
    /// (A message dialog opened from the sheet dismissed the sheet itself and lost the entry; see DECISIONS D4.)
    /// </summary>
    public IState<bool> Confirming => State.Value(this, () => false);

    public async ValueTask Give(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct)
    {
        if (calc.IsOutOfRange)
        {
            await Confirming.UpdateAsync(_ => true, ct);
            return;
        }
        await SaveAsync(calc, selected, routeIndex, ct);
    }

    public async ValueTask GiveAnyway(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct) =>
        await SaveAsync(calc, selected, routeIndex, ct);

    public async ValueTask ChangeDose(CancellationToken ct) => await Confirming.UpdateAsync(_ => false, ct);

    private async ValueTask SaveAsync(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct)
    {
        var dose = new DoseGiven(Guid.NewGuid(), Clock.Now, selected.Id, selected.Name, calc.MgPerKg, calc.MgPerMl, calc.VolumeMl,
            (DoseRoute)Math.Clamp(routeIndex, 0, 3), calc.IsOutOfRange);
        try
        {
            await Store.UpdateAsync(Ref.Id, c => c with { Doses = c.Doses.Add(dose) }, ct);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException)
        {
            await Error.UpdateAsync(_ => Loc.F("Error_NotSaved", "Not saved: {0}", ex.Message), ct);
            return;
        }
        Feedback.Recorded(Ref.Id);
        await Navigator.NavigateBackAsync(this, cancellation: ct);
    }

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
