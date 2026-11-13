namespace Vigil.Domain;

/// <summary>A formulary entry. Sample reference data: a practice supplies its own concentrations and ranges.</summary>
public partial record Drug(
    string Id,
    string Name,
    string DrugClass,
    decimal MgPerMl,
    decimal DefaultMgPerKg,
    decimal CanineMinMgPerKg,
    decimal CanineMaxMgPerKg,
    decimal FelineMinMgPerKg,
    decimal FelineMaxMgPerKg,
    DoseRoute DefaultRoute)
{
    /// <summary>Minutes from the last dose to the next one (cefazolin 90); null when the drug is not redosed.</summary>
    public int? RedoseMinutes { get; init; }

    public VitalRange RangeFor(Species species) => species == Species.Feline
        ? new(FelineMinMgPerKg, FelineMaxMgPerKg)
        : new(CanineMinMgPerKg, CanineMaxMgPerKg);
}

public partial record DoseCalc(
    decimal WeightKg,
    decimal MgPerKg,
    decimal MgPerMl,
    decimal TotalMg,
    decimal VolumeMl,
    RangeClass Range,
    VitalRange Reference,
    string Formula)
{
    public bool IsOutOfRange => Range != RangeClass.Normal;

    public string VolumeText => $"{VolumeMl:0.00} mL";

    public string ReferenceText => $"{Reference.Min:0.###}–{Reference.Max:0.###} mg/kg";
}

public static class Dosing
{
    /// <summary>Volume to draw up, rounded to 0.01 mL (a 1 mL syringe reads to 0.01).</summary>
    public static decimal VolumeMl(decimal weightKg, decimal mgPerKg, decimal mgPerMl) =>
        mgPerMl <= 0 ? 0 : Math.Round(weightKg * mgPerKg / mgPerMl, 2, MidpointRounding.AwayFromZero);

    public static DoseCalc Calculate(Species species, decimal weightKg, Drug drug, decimal mgPerKg)
    {
        var totalMg = Math.Round(weightKg * mgPerKg, 3, MidpointRounding.AwayFromZero);
        var volume = VolumeMl(weightKg, mgPerKg, drug.MgPerMl);
        var reference = drug.RangeFor(species);
        var formula = $"{weightKg:0.##} kg × {mgPerKg:0.###} mg/kg = {totalMg:0.###} mg ÷ {drug.MgPerMl:0.##} mg/mL";
        return new DoseCalc(weightKg, mgPerKg, drug.MgPerMl, totalMg, volume, reference.Classify(mgPerKg), reference, formula);
    }
}
