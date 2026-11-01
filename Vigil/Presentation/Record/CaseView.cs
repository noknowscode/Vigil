namespace Vigil.Presentation;

public partial record ReadingRow(string Time, string Hr, string Rr, string SpO2, string EtCo2, string Bp, string Map, string Temp, string Plane, string Alarms, string Note);

/// <summary>One lab panel in the Lab results pane.</summary>
public partial record LabRow(string Panel, string StateText, bool IsReady);

public partial record DoseRow(string Time, string Drug, string Dose, string Volume, string Route, string Flag);

/// <summary>
/// Read-only projection of a case for the record page, rebuilt when the case (or the open pane) changes. Values that
/// move with the clock are in <see cref="CaseTimers"/>, so the reading, dose and lab lists keep their items between ticks.
/// </summary>
public partial record CaseView(
    Guid Id,
    string Name,
    string Procedure,
    string Team,
    CaseStatus Status,
    string StatusText,
    decimal WeightKg,
    string WeightText,
    string Signalment,
    PreopCheck Preop,
    string PreopProgress,
    string InducedText,
    IImmutableList<ReadingRow> Readings,
    IImmutableList<DoseRow> Doses,
    int ReadingCount,
    string LastRecorded,
    string RecoveryAnchorLabel,
    string ExtubatedText,
    string SternalText,
    string SignedText) : CaseRef(Id)
{
    public string? Photo { get; init; }
    public string? HeroPhoto { get; init; }
    public string Breed { get; init; } = "";
    public string AgeOwner { get; init; } = "";
    public string WeightDate { get; init; } = "";
    public string VisitDate { get; init; } = "";
    public string LastHr { get; init; } = "—";
    public string LastSpo2 { get; init; } = "—";
    public string LastBp { get; init; } = "—";
    public string LastTemp { get; init; } = "—";
    public string LastEtCo2 { get; init; } = "—";
    public string LastMap { get; init; } = "—";
    public string LastRr { get; init; } = "—";
    public string Alert { get; init; } = "";
    public RecordChart Chart { get; init; } = null!;
    public Spark HrSpark { get; init; } = Spark.Empty;
    public Spark SpO2Spark { get; init; } = Spark.Empty;
    public string ProcedureMeta { get; init; } = "";
    public string OverdueDetail { get; init; } = "";
    public FluidsText Fluids { get; init; } = FluidsText.None;
    /// <summary>"Last score 1 at 12:35" under the pain score buttons.</summary>
    public string PainText { get; init; } = "";
    public IImmutableList<LabRow> LabRows { get; init; } = ImmutableList<LabRow>.Empty;
    public bool HasLabs => LabRows.Count > 0;
    public bool HasNoLabs => !HasLabs;
    /// <summary>"Discharge call to K. Gagnon · due 14:00" or "... · done 14:02"; empty when none is planned.</summary>
    public string DischargeText { get; init; } = "";
    public bool HasDischargeCall => DischargeText.Length > 0;
    public bool CanCompleteCall { get; init; }
    public string CirculationDetail => Loc.F("Record_BpMap", "BP {0} · MAP {1}", LastBp, LastMap);
    public string RespirationDetail => Loc.F("Record_EtCo2Rr", "EtCO₂ {0} mmHg · RR {1}", LastEtCo2, LastRr);
    public bool HasNoReadings => ReadingCount == 0;

    /// <summary>Pane behind the active record-nav item (the open pane, else Vitals; Preparation before induction).</summary>
    public string ActiveSection { get; init; } = "Monitor";
    public bool HasAlert => Alert.Length > 0;
    public string MedicationSummary => HasNoDoses ? Loc.T("Drugs_None", "No doses recorded") : Doses[0].Drug;
    public string MedicationDetail => HasNoDoses ? Loc.T("Drugs_NoneDetail", "Medication history stays with this visit.") : $"{Doses[0].Volume} · {Doses[0].Route} · {Doses[0].Time}";
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0]));
    public bool HasPhoto => !string.IsNullOrEmpty(HeroPhoto);
    public bool HasNoPhoto => !HasPhoto;
    public string PhotoName => Loc.F("Record_PhotoName", "{0}, {1}", Name, Breed);

    public bool IsRecovery => Status == CaseStatus.Recovery;

    public bool IsNotAnesthetized => !IsAnesthetized;

    public bool CanEditRecovery => Status == CaseStatus.Recovery;

    public bool IsNotExtubated => IsRecovery && ExtubatedText.Length == 0;

    public bool IsNotSternal => IsRecovery && SternalText.Length == 0;

    public bool IsScheduled => Status == CaseStatus.Scheduled;

    public bool IsAnesthetized => Status == CaseStatus.Anesthetized;

    public bool IsSigned => Status == CaseStatus.Signed;
    public bool HasAnaesthesiaRecord => !IsScheduled;

    public bool CanInduce => IsScheduled && Preop.IsComplete;

    /// <summary>Doses and readings can be added until the record is signed.</summary>
    public bool CanChange => !IsSigned;

    public bool HasNoDoses => Doses.Count == 0;
    public bool HasDoses => !HasNoDoses;

    public static CaseView From(Case c, DateTimeOffset now, TimeSpan interval) => From(c, now, interval, null);

    /// <param name="now">When the view is built: the chart's window and the local time zone for the times shown.</param>
    /// <param name="openPane">The open side pane, or null: picks the active record-nav item.</param>
    public static CaseView From(Case c, DateTimeOffset now, TimeSpan interval, string? openPane)
    {
        var p = c.Patient;
        return new CaseView(
            c.Id,
            p.Name,
            c.Procedure,
            $"{c.Veterinarian} · {c.Technician}",
            c.Status,
            RecordText.StatusLabel(c.Status),
            p.WeightKg,
            $"{p.WeightKg:0.0}",
            string.Join(" · ", new[] { p.SpeciesAndBreed, p.AgeYears > 0 ? Loc.F("Patient_AgeShort", "{0:0.#} y", p.AgeYears) : "", string.IsNullOrWhiteSpace(p.Owner) ? "" : Loc.F("Patient_OwnerShort", "owner {0}", p.Owner) }.Where(x => x.Length > 0)),
            c.Preop,
            Loc.F("Preop_Progress", "{0} of 5 checks", c.Preop.DoneCount),
            c.InducedAt is DateTimeOffset i ? Loc.F("Preop_InducedAt", "Induced at {0:HH:mm}", i.ToOffset(now.Offset)) : Loc.T("Preop_NotInduced", "Not induced"),
            c.Readings.Reverse().Select(r => Row(p.Species, r, now)).ToImmutableList(),
            c.Doses.Reverse().Select(d => DoseRowOf(d, now)).ToImmutableList(),
            c.Readings.Count,
            c.Readings.Count > 0 ? Loc.F("Readings_LastRecorded", "Last recorded {0:dd MMM · HH:mm}", c.Readings[^1].At.ToOffset(now.Offset)) : Loc.T("Readings_None", "No readings yet"),
            c.Recovery.ExtubatedAt is not null ? Loc.T("Recovery_SinceExtubation", "since extubation") : c.EndedAt is not null ? Loc.T("Recovery_SinceEnded", "since anesthesia ended (not extubated)") : Loc.T("Recovery_NotEnded", "anesthesia not ended"),
            c.Recovery.ExtubatedAt is DateTimeOffset ex ? Loc.F("Recovery_Extubated", "Extubated {0:HH:mm}", ex.ToOffset(now.Offset)) : "",
            c.Recovery.SternalAt is DateTimeOffset st ? Loc.F("Recovery_Sternal", "Sternal {0:HH:mm}", st.ToOffset(now.Offset)) : "",
            c.SignedBy is { } by ? Loc.F("Recovery_SignedBy", "Signed by {0} at {1:HH:mm}. The record is read-only.", by, c.SignedAt?.ToOffset(now.Offset)) : "")
        {
            Photo = PatientMedia.Photo(p), HeroPhoto = PatientMedia.Hero(p),
            Breed = string.IsNullOrWhiteSpace(p.Breed) ? Loc.Species(p.Species) : p.Breed,
            AgeOwner = $"{(p.AgeYears == 1 ? Loc.T("Patient_OneYear", "1 year") : Loc.F("Patient_AgeYears", "{0:0.#} years", p.AgeYears))} · {p.Owner}",
            WeightDate = Loc.F("Weight_RecordedAt", "Recorded {0:d MMM, HH:mm}", c.CreatedAt.ToOffset(now.Offset)), VisitDate = $"{c.CreatedAt.ToOffset(now.Offset):dd MMM yyyy}",
            // The window follows the readings and the due time, not the clock: building it with the case is enough.
            Chart = RecordChart.From(c, now, interval),
            HrSpark = Spark.HeartRate(c),
            SpO2Spark = Spark.From(c.Readings.Where(r => r.SpO2 is not null).Select(r => r.SpO2!.Value).TakeLast(13).ToList(), 4),
            ProcedureMeta = RecordText.ProcedureMeta(c, now),
            OverdueDetail = RecordText.OverdueDetail(c, now, interval),
            Fluids = RecordText.Fluids(c),
            LabRows = c.Labs.Select(l => new LabRow(l.Panel, l.State switch
            {
                LabState.Ready => Loc.T("Lab_Ready", "Results ready"),
                LabState.Reviewed => Loc.T("Lab_Reviewed", "Reviewed"),
                _ => Loc.F("Lab_Pending", "Pending · expected {0:HH:mm}", l.At.ToOffset(now.Offset)),
            }, l.State == LabState.Ready)).ToImmutableList(),
            DischargeText = c.DischargeCall is { } call
                ? call.DoneAt is DateTimeOffset done
                    ? Loc.F("Discharge_Done", "Discharge call to {0} · done {1:HH:mm}", p.Owner, done.ToOffset(now.Offset))
                    : Loc.F("Discharge_Due", "Discharge call to {0} · due {1:HH:mm}", p.Owner, call.DueAt.ToOffset(now.Offset))
                : "",
            CanCompleteCall = c.DischargeCall is { IsDone: false },
            PainText = c.Recovery.PainScores.LastOrDefault() is { } ps
                ? Loc.F("Recovery_LastPain", "Last score {0} at {1:HH:mm}", ps.Score, ps.At.ToOffset(now.Offset))
                : c.Recovery.PainScore is int legacy ? Loc.F("Recovery_PainOnly", "Last score {0}", legacy) : Loc.T("Recovery_NoPain", "Not scored yet"),
            ActiveSection = openPane ?? (Stage.Of(c) is CareStage.Booked or CareStage.Prep ? "Preop" : "Monitor"),
            LastHr = c.Readings.LastOrDefault()?.Hr?.ToString() ?? "—",
            LastSpo2 = c.Readings.LastOrDefault()?.SpO2?.ToString() ?? "—",
            LastBp = c.Readings.LastOrDefault() is { } bp ? $"{bp.Sys?.ToString() ?? "—"}/{bp.Dia?.ToString() ?? "—"}" : "—",
            LastTemp = c.Readings.LastOrDefault()?.TempC?.ToString("0.0") ?? "—",
            LastEtCo2 = c.Readings.LastOrDefault()?.EtCo2?.ToString() ?? "—",
            LastMap = c.Readings.LastOrDefault()?.EffectiveMap?.ToString() ?? "—",
            LastRr = c.Readings.LastOrDefault()?.Rr?.ToString() ?? "—",
            Alert = c.Readings.LastOrDefault() is { } last ? Row(p.Species, last, now).Alarms : ""
        };
    }

    private static ReadingRow Row(Species species, VitalsReading r, DateTimeOffset now)
    {
        static string N(int? v) => v?.ToString() ?? "—";
        var alarms = Vitals.Alarms(species, r)
            .Select(a => $"{Vitals.Label(a.Kind)} {(a.Class == RangeClass.High ? Loc.T("Alarm_High", "▲ high") : Loc.T("Alarm_Low", "▼ low"))}");
        return new ReadingRow(
            Today.Time(r.At, now), N(r.Hr), N(r.Rr), N(r.SpO2), N(r.EtCo2),
            r.Sys is null && r.Dia is null ? "—" : $"{N(r.Sys)}/{N(r.Dia)}",
            r.EffectiveMap is int m ? (r.MapIsCalculated ? Loc.F("Map_Calc", "{0} calc", m) : $"{m}") : "—",
            r.TempC is decimal t ? $"{t:0.0}" : "—",
            Loc.Plane(r.Plane),
            string.Join(" · ", alarms),
            r.Note);
    }

    private static DoseRow DoseRowOf(DoseGiven d, DateTimeOffset now) => new(
        Today.Time(d.At, now), d.DrugName, $"{d.MgPerKg:0.###} mg/kg", $"{d.VolumeMl:0.00} mL", d.Route.ToString(),
        d.OutOfRangeConfirmed ? Loc.T("Dose_OutOfRangeConfirmed", "Out of range, confirmed") : "");
}

/// <summary>
/// The record's values that move with the clock: the status card (its timer and overdue line), the due countdown, the
/// overdue banner, the next redose and the time in recovery. Built every second, apart from <see cref="CaseView"/>, and
/// a value equal to the last one is not pushed again (all members compare by value).
/// </summary>
public sealed partial record CaseTimers(StatusCard Care, Spark HrSpark, string ClockText, bool IsOverdue, string OverdueTitle, NextDoseText NextDose, string RecoveryAnchor)
{
    /// <param name="formulary">Drugs with their redose intervals (Next dose card).</param>
    public static CaseTimers From(Case c, DateTimeOffset now, TimeSpan interval, IReadOnlyList<Drug> formulary)
    {
        var due = Schedule.State(c, interval, now);
        var countdown = Schedule.Clock((Schedule.Remaining(c, interval, now) ?? TimeSpan.Zero).Duration());
        return new(
            StatusCard.From(c, now, interval),
            // The status card draws the heart-rate sparkline under its timer.
            Spark.HeartRate(c),
            // "next 2:41" while waiting, "overdue 0:40" once late: the sign is in the words, not hidden by an absolute value.
            due switch
            {
                DueState.Overdue => Loc.F("Clock_Overdue", "overdue {0}", countdown),
                DueState.None => "",
                _ => Loc.F("Clock_Next", "next {0}", countdown),
            },
            c.Status == CaseStatus.Anesthetized && due == DueState.Overdue,
            RecordText.OverdueTitle(c, now, interval),
            RecordText.NextDose(c, now, formulary),
            // A signed record stops its recovery clock at the signature.
            c.Recovery.ExtubatedAt is DateTimeOffset x ? Schedule.HoursMinutes((c.SignedAt ?? now) - x)
                : c.EndedAt is DateTimeOffset e ? Schedule.HoursMinutes((c.SignedAt ?? now) - e) : "—");
    }
}
