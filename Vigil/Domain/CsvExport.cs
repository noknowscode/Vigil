using System.Globalization;
using System.Text;

namespace Vigil.Domain;

/// <summary>Signed record → CSV (one row per event) and a plain-text summary for the patient file.</summary>
public static class CsvExport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string ToCsv(Case c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("time,event,hr,rr,spo2,etco2,sys,dia,map,map_calculated,temp_c,vaporizer_pct,o2_l_min,plane,drug,mg_per_kg,volume_ml,route,note");
        var rows = new List<(DateTimeOffset At, string Line)>();
        foreach (var r in c.Readings)
        {
            rows.Add((r.At, string.Join(',',
                Time(r.At), "reading", N(r.Hr), N(r.Rr), N(r.SpO2), N(r.EtCo2), N(r.Sys), N(r.Dia), N(r.EffectiveMap),
                r.MapIsCalculated ? "yes" : "no", D(r.TempC), D(r.VaporizerPct), D(r.O2LMin), r.Plane.ToString(), "", "", "", "", Q(r.Note))));
        }
        foreach (var d in c.Doses)
        {
            rows.Add((d.At, string.Join(',',
                Time(d.At), "dose", "", "", "", "", "", "", "", "", "", "", "", "", Q(d.DrugName), D(d.MgPerKg), D(d.VolumeMl), d.Route.ToString(),
                d.OutOfRangeConfirmed ? "out of range, confirmed" : "")));
        }
        foreach (var x in c.Corrections)
        {
            rows.Add((x.At, string.Join(',',
                Time(x.At), "correction", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", Q($"{x.Field}: {x.Before} -> {x.After}"))));
        }
        foreach (var (at, line) in rows.OrderBy(r => r.At))
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    public static string ToSummary(Case c)
    {
        var p = c.Patient;
        var sb = new StringBuilder();
        sb.AppendLine($"Anesthesia record: {p.Name} ({p.Species}, {p.Breed}), {p.WeightKg.ToString("0.0", Inv)} kg, owner {p.Owner}");
        sb.AppendLine($"Procedure: {c.Procedure}. Veterinarian: {c.Veterinarian}. Technician: {c.Technician}.");
        sb.AppendLine($"{c.Preop.AsaLabel}. Induced {Time(c.InducedAt)}, ended {Time(c.EndedAt)}, duration {Schedule.HoursMinutes(Schedule.Elapsed(c, c.EndedAt ?? c.CreatedAt))}.");
        sb.AppendLine($"Readings: {c.Readings.Count}. Doses: {c.Doses.Count}.");
        foreach (var d in c.Doses)
        {
            sb.AppendLine($"  {Time(d.At)} {d.DrugName} {d.MgPerKg.ToString("0.###", Inv)} mg/kg = {d.VolumeMl.ToString("0.00", Inv)} mL {d.Route}{(d.OutOfRangeConfirmed ? " (out of range, confirmed)" : "")}");
        }
        var r = c.Recovery;
        sb.AppendLine($"Recovery: extubated {Time(r.ExtubatedAt)}, sternal {Time(r.SternalAt)}, pain {(r.PainScore?.ToString(Inv) ?? "—")}/4, temp {(r.TempC?.ToString("0.0", Inv) ?? "—")} °C.");
        if (!string.IsNullOrWhiteSpace(r.Notes))
        {
            sb.AppendLine($"Notes: {r.Notes}");
        }
        foreach (var x in c.Corrections)
        {
            sb.AppendLine($"Corrected {Time(x.At)}: {x.Field} {(x.Before.Length == 0 ? "—" : x.Before)} -> {(x.After.Length == 0 ? "—" : x.After)}");
        }
        sb.AppendLine($"Signed by {c.SignedBy ?? "—"} at {Time(c.SignedAt)}.");
        return sb.ToString();
    }

    private static string Time(DateTimeOffset? t) => t?.ToString("yyyy-MM-dd HH:mm", Inv) ?? "—";

    private static string N(int? v) => v?.ToString(Inv) ?? "";

    private static string D(decimal? v) => v?.ToString(Inv) ?? "";

    private static string Q(string s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";
}
