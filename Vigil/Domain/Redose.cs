using System.Globalization;

namespace Vigil.Domain;

/// <summary>A drug that is due again: its last dose plus the formulary's redose interval.</summary>
public partial record NextDose(DoseGiven Last, Drug Drug, DateTimeOffset DueAt)
{
    /// <summary>"Cefazolin redose".</summary>
    public string Title => Loc.F("Redose_Title", "{0} redose", Last.DrugName);

    /// <summary>"4.05 mL · IV" (the last dose, repeated).</summary>
    public string Detail => $"{Last.VolumeMl.ToString("0.00", CultureInfo.CurrentCulture)} mL · {Last.Route}";
}

public static class Redose
{
    /// <summary>Every redosed drug given in this case, with its next dose time, earliest first.</summary>
    public static ImmutableList<NextDose> Of(Case c, IEnumerable<Drug> formulary)
    {
        var drugs = formulary.Where(d => d.RedoseMinutes is > 0).ToDictionary(d => d.Id);
        return c.Doses
            .Where(d => drugs.ContainsKey(d.DrugId))
            .GroupBy(d => d.DrugId)
            .Select(g =>
            {
                var last = g.OrderBy(d => d.At).Last();
                var drug = drugs[g.Key];
                return new NextDose(last, drug, last.At.AddMinutes(drug.RedoseMinutes!.Value));
            })
            .OrderBy(n => n.DueAt)
            .ToImmutableList();
    }

    /// <summary>The earliest next dose, or null when nothing given is redosed.</summary>
    public static NextDose? Next(Case c, IEnumerable<Drug> formulary) => Of(c, formulary).FirstOrDefault();

    /// <summary>"Cefazolin redose · in 28 min".</summary>
    public static string Describe(NextDose next, DateTimeOffset now) => $"{next.Title} · {DueTasks.Countdown(next.DueAt, now, lowercase: true)}";
}
