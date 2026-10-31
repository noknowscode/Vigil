using System.Globalization;
using Vigil.Domain;

namespace Vigil.Presentation;

/// <summary>
/// A sparkline of recorded readings in a 160 x 32 box (docs/design/patient-care-values.md §3.4, Cards C and D).
/// The Path stretches its geometry bounds to the element, so the record also gives the element's top and height
/// inside the 32 px box and the end dot's position; the stroke stays 2 px whatever the card width.
/// SPEC-v2 decision 3: drawn from recorded readings, never a decorative waveform.
/// </summary>
public sealed partial record Spark(string Data, double Top, double Height, double EndY)
{
    public const double BoxHeight = 32;

    public static Spark Empty { get; } = new("", 0, 0, 0);

    public bool HasLine => Data.Length > 0;

    public bool HasNoLine => !HasLine;

    /// <summary>Line element margin: 4 px in from each side (x 4 to 156 of 160), top at the highest point.</summary>
    public Thickness LineMargin => new(4, Top, 4, 0);

    /// <summary>8 px end dot centred on the last point (right edge, 4 px in).</summary>
    public Thickness DotMargin => new(0, EndY - 4, 0, 0);

    /// <summary>
    /// Points at x = 4 + i·152/(n-1); y = 16 - (v - mid)·k with k = min(<paramref name="maxScale"/>, 14.4 / range), so
    /// the prototype's HR series (90-96, k 2.4) and SpO₂ series (96-98, k 4) land exactly on its coordinates.
    /// </summary>
    public static Spark From(IReadOnlyList<int> values, double maxScale)
    {
        if (values.Count < 2)
        {
            return Empty;
        }
        var min = values.Min();
        var max = values.Max();
        var mid = (min + max) / 2.0;
        var k = max == min ? 0 : Math.Min(maxScale, 14.4 / (max - min));
        var points = values.Select((v, i) => (X: 4 + i * 152.0 / (values.Count - 1), Y: 16 - (v - mid) * k)).ToList();
        var top = points.Min(p => p.Y);
        var bottom = points.Max(p => p.Y);
        var data = "M" + string.Join(" L", points.Select(p => Num($"{p.X:0.##},{p.Y:0.##}")));
        if (bottom - top < 4)
        {
            // A flat (or nearly flat) line has no height to stretch, and a Path shorter than its stroke draws nothing:
            // pad the box to 4 px with a tick at the end point, hidden under the 8 px end dot.
            var pad = (4 - (bottom - top)) / 2;
            top -= pad;
            bottom += pad;
            data += Num($" M156,{top:0.##} V{bottom:0.##}");
        }
        return new Spark(data, top, bottom - top, points[^1].Y);
    }

    internal static string Num(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The anesthesia record chart (§3.4 Card A) in its 400 x 188 viewBox: HR line and points in the top band, BP bars
/// in the bottom band, a 75-minute window on 15-minute ticks, and the next-due band. Geometry strings are drawn by
/// XAML Paths inside a uniform Viewbox, so every value here is in viewBox units.
/// </summary>
public sealed partial record RecordChart(
    string HrLine,
    string HrPoints,
    string HrLast,
    string BpBars,
    string DueBand,
    string DueLine,
    string DueLabel,
    IImmutableList<string> XLabels,
    IImmutableList<string> HrTicks,
    IImmutableList<string> BpTicks,
    string AccessibleName)
{
    public const double Left = 34;
    public const double Right = 378;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(75);

    public bool HasDue => DueLine.Length > 0;
    public bool HasReadings => HrLine.Length > 0 || BpBars.Length > 0;
    public bool HasNoReadings => !HasReadings;

    public string X0 => XLabels[0];
    public string X1 => XLabels[1];
    public string X2 => XLabels[2];
    public string X3 => XLabels[3];
    public string X4 => XLabels[4];
    public string X5 => XLabels[5];
    public string Hr0 => HrTicks[0];
    public string Hr1 => HrTicks[1];
    public string Hr2 => HrTicks[2];
    public string Bp0 => BpTicks[0];
    public string Bp1 => BpTicks[1];
    public string Bp2 => BpTicks[2];

    /// <summary>Start of the window: induction (or the first reading, or now) on a 15-minute tick; it slides to keep the latest reading (and the due time under anesthesia) in view.</summary>
    public static DateTimeOffset WindowStart(Case c, DateTimeOffset now, TimeSpan interval)
    {
        var anchor = c.InducedAt ?? c.Readings.FirstOrDefault()?.At ?? now;
        var start = Floor15(anchor.ToOffset(now.Offset));
        // The due time while anesthetized, else the last reading: the window moves with the record, not with the clock.
        var latest = new[] { c.Status == CaseStatus.Anesthetized ? Schedule.NextDue(c, interval) : null, c.Readings.LastOrDefault()?.At }
            .Where(t => t is not null).Select(t => t!.Value).DefaultIfEmpty(start).Max();
        var end = Ceil15(latest.ToOffset(now.Offset));
        return end - start > Window ? end - Window : start;
    }

    public static RecordChart From(Case c, DateTimeOffset now, TimeSpan interval)
    {
        var start = WindowStart(c, now, interval);
        var end = start + Window;
        var unit = (Right - Left) / Window.TotalMinutes;
        double X(DateTimeOffset t) => Left + (t - start).TotalMinutes * unit;
        var readings = c.Readings.Where(r => r.At >= start && r.At <= end).ToList();

        var hr = readings.Where(r => r.Hr is not null).Select(r => (X: X(r.At), V: r.Hr!.Value, r.At)).ToList();
        var (hrLo, hrHi) = Band(hr.Select(p => p.V).ToList(), 80, 110, 15);
        double HrY(int v) => Math.Clamp(64 - (v - hrLo) * (52.0 / (hrHi - hrLo)), 12, 64);

        var bp = readings.Where(r => r.Sys is not null && r.Dia is not null).Select(r => (X: X(r.At), Sys: r.Sys!.Value, Dia: r.Dia!.Value)).ToList();
        var (bpLo, bpHi) = Band(bp.SelectMany(p => new[] { p.Sys, p.Dia }).ToList(), 40, 130, 45);
        double BpY(int v) => Math.Clamp(160 - (v - bpLo) * (64.0 / (bpHi - bpLo)), 96, 160);

        static string Dot(double x, double y, double r) => Spark.Num($"M{x - r:0.##},{y:0.##}a{r},{r} 0 1 0 {2 * r},0a{r},{r} 0 1 0 {-2 * r},0");

        var hrLine = hr.Count >= 2 ? "M" + string.Join(" L", hr.Select(p => Spark.Num($"{p.X:0.##},{HrY(p.V):0.##}"))) : "";
        var hrPoints = string.Concat(hr.Take(Math.Max(0, hr.Count - 1)).Select(p => Dot(p.X, HrY(p.V), 2.5)));
        var hrLast = hr.Count > 0 ? Dot(hr[^1].X, HrY(hr[^1].V), 4) : "";
        // A 4-unit bar with radius 2 is a 4-wide round-capped stroke, inset 2 at each end.
        var bars = string.Concat(bp.Select(p =>
        {
            var top = BpY(p.Sys) + 2;
            var bottom = Math.Max(top, BpY(p.Dia) - 2);
            return Spark.Num($"M{p.X:0.##},{top:0.##}V{bottom:0.##}");
        }));

        var due = c.Status == CaseStatus.Anesthetized ? Schedule.NextDue(c, interval) : null;
        var dueX = due is DateTimeOffset d && d >= start && d <= end ? X(d) : (double?)null;
        var dueBand = dueX is double bx ? Spark.Num($"M{bx - 4:0.##},8h8a4,4 0 0 1 4,4v144a4,4 0 0 1 -4,4h-8a4,4 0 0 1 -4,-4v-144a4,4 0 0 1 4,-4Z") : "";
        var dueLine = dueX is double lx ? Spark.Num($"M{lx:0.##},8V160") : "";
        var dueLabel = due is DateTimeOffset dl ? Loc.F("Chart_Due", "Due {0}", Today.Time(dl, now)) : "";

        var labels = Enumerable.Range(0, 6).Select(i => Today.Time(start.AddMinutes(15 * i), now)).ToImmutableList();
        var name = readings.Count == 0
            ? Loc.T("Chart_NoneName", "Anesthesia record, no readings yet.")
            : Loc.F("Chart_Name", "Anesthesia record, {0} to {1}. Heart rate {2} to {3} bpm. Blood pressure {4} to {5} mmHg.",
                Today.Time(readings[0].At, now), Today.Time(readings[^1].At, now),
                hr.Count > 0 ? hr.Min(p => p.V) : "—", hr.Count > 0 ? hr.Max(p => p.V) : "—",
                bp.Count > 0 ? $"{bp.Min(p => p.Sys)}/{bp.Min(p => p.Dia)}" : "—", bp.Count > 0 ? $"{bp.Max(p => p.Sys)}/{bp.Max(p => p.Dia)}" : "—")
              + (due is DateTimeOffset nd ? " " + Loc.F("Chart_NameDue", "Next reading due at {0}.", Today.Time(nd, now)) : "");

        return new RecordChart(hrLine, hrPoints, hrLast, bars, dueBand, dueLine, dueLabel, labels,
            [$"{hrHi}", $"{(hrLo + hrHi) / 2}", $"{hrLo}"], [$"{bpHi}", $"{(bpLo + bpHi) / 2}", $"{bpLo}"], name);
    }

    /// <summary>The prototype's band (80-110, 40-130) when the values fit; otherwise a band centred on them in steps of 5.</summary>
    public static (int Lo, int Hi) Band(IReadOnlyList<int> values, int lo, int hi, int minHalf)
    {
        if (values.Count == 0 || (values.Min() >= lo && values.Max() <= hi))
        {
            return (lo, hi);
        }
        var half = Math.Max(minHalf, (int)Math.Ceiling(((values.Max() - values.Min()) / 2.0 + 3) / 5) * 5);
        var mid = (int)Math.Round((values.Max() + values.Min()) / 2.0 / 5) * 5;
        return (mid - half, mid + half);
    }

    private static DateTimeOffset Floor15(DateTimeOffset t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute / 15 * 15, 0, t.Offset);

    private static DateTimeOffset Ceil15(DateTimeOffset t)
    {
        var f = Floor15(t);
        return f == t ? f : f.AddMinutes(15);
    }
}

/// <summary>Tone of the hero status card and the identity chip: green, blue, amber or neutral surfaces.</summary>
public enum CareTone { Surgery, Recovery, Prep, Neutral }

/// <summary>
/// The hero status card (§3.2 status column) for every stage of the record: under anesthesia (the prototype),
/// recovering, preparing, booked and signed out. Same layout in each; only the words, numbers and tone change.
/// </summary>
public sealed partial record StatusCard(
    CareTone Tone,
    string Chip,
    string Label,
    string Since,
    string Timer,
    string FirstLabel,
    string FirstValue,
    string SecondLabel,
    string SecondValue,
    string Alert,
    string Cta,
    string CtaPane)
{
    public string ToneState => Tone.ToString();
    public bool HasAlert => Alert.Length > 0;
    public bool IsSurgery => Tone == CareTone.Surgery;
    public bool IsRecovery => Tone == CareTone.Recovery;
    public bool IsPrep => Tone == CareTone.Prep;
    public bool IsNeutral => Tone == CareTone.Neutral;
    public string AccessibleName => string.Join(", ", new[] { Label, Since, Timer, $"{FirstLabel} {FirstValue}", $"{SecondLabel} {SecondValue}", Alert }.Where(s => s.Trim().Length > 0));

    /// <summary>"1:18": hours and minutes until <paramref name="at"/>, rounded up to the minute like the Today hero.</summary>
    public static string Countdown(DateTimeOffset at, DateTimeOffset now) =>
        Today.Clock(TimeSpan.FromMinutes(Math.Ceiling(Math.Max(0, (at - now).TotalMinutes))));

    public static StatusCard From(Case c, DateTimeOffset now, TimeSpan interval)
    {
        string T(DateTimeOffset? t) => t is DateTimeOffset v ? Today.Time(v, now) : "—";
        var preop = Loc.F("Care_PreopValue", "{0} of 5", c.Preop.DoneCount);
        switch (Stage.Of(c))
        {
            case CareStage.Surgery:
                var due = Schedule.NextDue(c, interval);
                var late = Schedule.State(c, interval, now) == DueState.Overdue && due is DateTimeOffset d ? now - d : (TimeSpan?)null;
                return new(CareTone.Surgery, Loc.T("Care_ChipSurgery", "Under anesthesia"), Loc.T("Care_Surgery", "UNDER ANESTHESIA"),
                    Loc.F("Care_Since", "since {0}", T(c.InducedAt)), Today.Clock(Schedule.Elapsed(c, now)),
                    Loc.T("Care_LastReading", "Last reading"), T(c.Readings.LastOrDefault()?.At),
                    Loc.T("Care_NextDue", "Next due"), T(due),
                    late is TimeSpan l ? Loc.F("Care_Overdue", "Reading overdue {0}", Schedule.Clock(l)) : "",
                    Loc.T("Care_CtaRecord", "Record observations"), "Monitor");
            case CareStage.Recovery:
                var ended = c.EndedAt ?? now;
                var pain = c.Recovery.PainScores.LastOrDefault()?.Score;
                return new(CareTone.Recovery, Loc.T("Care_ChipRecovery", "In recovery"), Loc.T("Care_Recovery", "IN RECOVERY"),
                    Loc.F("Care_Since", "since {0}", T(c.EndedAt)), Today.Clock(now - ended),
                    Loc.T("Care_Extubated", "Extubated"), T(c.Recovery.ExtubatedAt),
                    Loc.T("Care_Pain", "Pain score"), pain is int s ? Loc.F("Care_PainValue", "{0} of 4", s) : "—",
                    "", Loc.T("Care_CtaRecovery", "Update recovery"), "Recovery");
            case CareStage.Prep:
                return new(CareTone.Prep, Loc.T("Care_ChipPrep", "Preparing"), Loc.T("Care_Prep", "PREPARING"),
                    Loc.F("Care_Starts", "starts {0}", T(c.PlannedStart)), c.PlannedStart is DateTimeOffset ps ? Countdown(ps, now) : "—",
                    Loc.T("Care_Preop", "Pre-op checks"), preop,
                    Loc.T("Care_Fasting", "Fasting"), c.FastingSince is DateTimeOffset f ? Today.Clock(now - f) : "—",
                    "", Loc.T("Care_CtaPrep", "Review preparation"), "Preop");
            case CareStage.Booked:
                return new(CareTone.Neutral, Loc.T("Care_ChipBooked", "Booked"), Loc.T("Care_Booked", "BOOKED"),
                    Loc.F("Care_Starts", "starts {0}", T(c.PlannedStart)), c.PlannedStart is DateTimeOffset bs ? Countdown(bs, now) : "—",
                    Loc.T("Care_Preop", "Pre-op checks"), preop,
                    Loc.T("Care_Fasting", "Fasting"), c.FastingSince is DateTimeOffset bf ? Today.Clock(now - bf) : "—",
                    "", Loc.T("Care_CtaPrep", "Review preparation"), "Preop");
            default:
                return new(CareTone.Neutral, Loc.T("Care_ChipHome", "Signed out"), Loc.T("Care_Home", "SIGNED OUT"),
                    Loc.F("Care_At", "at {0}", T(c.SignedAt ?? c.DischargedAt)), Today.Clock(Schedule.Elapsed(c, now)),
                    Loc.T("Care_SignedBy", "Signed by"), c.SignedBy ?? "—",
                    Loc.T("Care_Readings", "Readings"), $"{c.Readings.Count}",
                    "", Loc.T("Care_CtaHome", "Review record"), "Recovery");
        }
    }
}

/// <summary>Next dose block of the Latest medication card: time and "Cefazolin redose · in 28 min".</summary>
public sealed partial record NextDoseText(string Time, string Detail)
{
    public static NextDoseText None { get; } = new("—", "");
}

/// <summary>Footer of the Latest medication card: the running fluid (green dot) or "No fluids running".</summary>
public sealed partial record FluidsText(string Text, bool IsRunning)
{
    public static FluidsText None { get; } = new("", false);
    public bool IsStopped => !IsRunning;

    /// <summary>The start button reads as a rate change while a run is going.</summary>
    public string StartLabel => IsRunning ? Loc.T("Fluids_ChangeRate", "Change rate") : Loc.T("Fluids_Start", "Start fluids");
}

/// <summary>Record page sentences, built from the case and the time (unit-tested in RecordViewTests).</summary>
public static class RecordText
{
    /// <summary>The record's status pill: "Under anesthesia", "Recovery", "Signed", "Scheduled".</summary>
    public static string StatusLabel(CaseStatus s) => s switch
    {
        CaseStatus.Anesthetized => Loc.T("Status_Anesthetized", "Under anesthesia"),
        CaseStatus.Recovery => Loc.T("Status_Recovery", "Recovery"),
        CaseStatus.Signed => Loc.T("Status_Signed", "Signed"),
        _ => Loc.T("Status_Scheduled", "Scheduled"),
    };

    /// <summary>"Started 11:30 · OR 1 · Dr. Okafor, S. Tremblay" (or "Planned 14:00 · …" before induction).</summary>
    public static string ProcedureMeta(Case c, DateTimeOffset now)
    {
        var when = c.InducedAt is DateTimeOffset i ? Loc.F("Record_Started", "Started {0}", Today.Time(i, now))
            : c.PlannedStart is DateTimeOffset p ? Loc.F("Record_Planned", "Planned {0}", Today.Time(p, now)) : "";
        var team = string.Join(", ", new[] { c.Veterinarian, c.Technician }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.Join(" · ", new[] { when, c.OperatingRoom ?? "", team }.Where(s => s.Length > 0));
    }

    /// <summary>"Reading overdue by 1:26", or empty when the reading is not overdue.</summary>
    public static string OverdueTitle(Case c, DateTimeOffset now, TimeSpan interval) =>
        c.Status == CaseStatus.Anesthetized && Schedule.State(c, interval, now) == DueState.Overdue && Schedule.NextDue(c, interval) is DateTimeOffset due
            ? Loc.F("Record_OverdueBy", "Reading overdue by {0}", Schedule.Clock(now - due))
            : "";

    /// <summary>"Last recorded 28 Oct, 12:36 · readings due every 5 minutes".</summary>
    public static string OverdueDetail(Case c, DateTimeOffset now, TimeSpan interval)
    {
        var every = Loc.F("Record_DueEvery", "readings due every {0} minutes", (int)interval.TotalMinutes);
        return c.Readings.LastOrDefault() is { } last
            ? $"{Loc.F("Record_LastRecordedAt", "Last recorded {0:d MMM, HH:mm}", last.At.ToOffset(now.Offset))} · {every}"
            : $"{Loc.T("Readings_None", "No readings yet")} · {every}";
    }

    public static NextDoseText NextDose(Case c, DateTimeOffset now, IEnumerable<Drug> formulary) =>
        Redose.Next(c, formulary) is { } next
            ? new(Today.Time(next.DueAt, now), Redose.Describe(next, now))
            : new("—", Loc.T("Record_NoRedose", "No redose scheduled"));

    public static FluidsText Fluids(Case c) =>
        c.Fluids.LastOrDefault(f => f.IsRunning) is { } run
            ? new(Loc.F("Record_FluidsRunning", "Fluids running · {0}", run.Label), true)
            : new(Loc.T("Record_NoFluids", "No fluids running"), false);
}
