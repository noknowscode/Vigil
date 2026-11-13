namespace Vigil.Presentation;

/// <summary>The Medication record drawer (route "!Drugs"): doses given, Give a dose, and the fluid run (start, rate change, stop).</summary>
public partial record MedicationsModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, INavigator Navigator, ILogger<MedicationsModel> Logger)
    : CaseDrawerModel(Ref, Store, Clock, Preferences, Navigator, Logger)
{
    /// <summary>Give a dose: the dose drawer.</summary>
    public async ValueTask GiveDose(CancellationToken ct) => await HandBackAsync(RecordPane.Dose, ct);

    /// <summary>The fluid to start, prefilled with the usual maintenance run.</summary>
    public IState<string> FluidName => State.Value(this, () => "LRS");

    /// <summary>Rate in mL/kg/h, text while typing like the other numeric fields.</summary>
    public IState<string> FluidRate => State.Value(this, () => "5");

    public async ValueTask StartFluids(string fluidName, string fluidRate, CancellationToken ct)
    {
        if (VitalsDraft.ParseDec(fluidRate ?? "") is not decimal rate || rate <= 0 || rate > Domain.Case.MaxFluidRate || string.IsNullOrWhiteSpace(fluidName))
        {
            await ShowErrorAsync(Loc.F("Fluids_Invalid", "Enter a fluid and a rate above 0 and up to {0} mL/kg/h.", Domain.Case.MaxFluidRate), ct);
            return;
        }
        var at = Clock.Now;
        await SaveAsync(c => c.IsSigned ? c : c.WithFluidsStarted(fluidName, rate, at), ct);
    }

    public async ValueTask StopFluids(CancellationToken ct)
    {
        var at = Clock.Now;
        await SaveAsync(c => c.IsSigned ? c : c.WithFluidsStopped(at), ct);
    }
}
