namespace Vigil.Presentation;

/// <summary>The dose sheet: the shared <see cref="DoseEntry"/> for one case; Give saves and goes back.</summary>
public partial record DoseModel(CaseRef Ref, ICaseStore Store, IFormulary Formulary, IClock Clock, INavigator Navigator, DoseFeedback Feedback, ILogger<DoseModel> Logger)
{
    private IFeed<Case> Source { get; } = Feed<Case>.Async(
        async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(c) : Option.None<Case>(), Store.Changed);

    private DoseEntry Entry => _entry ??= new(this, Ref.Id, Source, Store, Formulary, Clock, Feedback, Logger);
    private DoseEntry? _entry;

    public IState<Drug> Selected => Entry.Selected;
    public IListFeed<Drug> Drugs => Entry.Drugs;
    public IState<string> MgPerKg => Entry.MgPerKg;
    public IState<int> RouteIndex => Entry.RouteIndex;
    public IFeed<DoseCalc> Calc => Entry.Calc;
    public IState<string> Error => Entry.Error;
    public IState<bool> Confirming => Entry.Confirming;
    public IFeed<bool> NotConfirming => Entry.NotConfirming;
    public IFeed<string> ConfirmTitle => Entry.ConfirmTitle;
    public IFeed<string> ConfirmText => Entry.ConfirmText;

    public IFeed<string> Patient => Source.Select(c => $"{c.Patient.Name} · {Loc.Species(c.Patient.Species)} · {c.Patient.WeightKg:0.0} kg");

    public async ValueTask Give(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct)
    {
        if (await Entry.GiveAsync(calc, selected, routeIndex, ct))
        {
            await Navigator.NavigateBackAsync(this, cancellation: ct);
        }
    }

    public async ValueTask GiveAnyway(DoseCalc calc, Drug selected, int routeIndex, CancellationToken ct)
    {
        if (await Entry.GiveAnywayAsync(calc, selected, routeIndex, ct))
        {
            await Navigator.NavigateBackAsync(this, cancellation: ct);
        }
    }

    public async ValueTask ChangeDose(CancellationToken ct) => await Entry.ChangeAsync(ct);

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
