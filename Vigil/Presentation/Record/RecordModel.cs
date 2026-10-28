namespace Vigil.Presentation;

public partial record ReadingRow(string Time, string Hr, string Rr, string SpO2, string EtCo2, string Bp, string Map, string Temp, string Plane, string Alarms, string Note);

public partial record DoseRow(string Time, string Drug, string Dose, string Volume, string Route, string Flag);

/// <summary>Read-only projection of a case for the record page, refreshed by store changes and the 1 Hz clock.</summary>
public partial record CaseView(
    Guid Id,
    string Name,
    string Band,
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
    string Elapsed,
    string NextLabel,
    string NextText,
    string DueStateName,
    double ProgressPercent,
    IImmutableList<ReadingRow> Readings,
    IImmutableList<DoseRow> Doses,
    int ReadingCount,
    StripData Strip,
    string StripSummary,
    string LastRecorded,
    string RecoveryAnchor,
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
    public bool HasAlert => Alert.Length > 0;
    public bool IsOverdue => IsAnesthetized && DueStateName == nameof(DueState.Overdue);
    public string MedicationSummary => HasNoDoses ? "No doses recorded" : Doses[0].Drug;
    public string MedicationDetail => HasNoDoses ? "Medication history stays with this visit." : $"{Doses[0].Volume} · {Doses[0].Route} · {Doses[0].Time}";
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0]));
    public bool HasNoPhoto => string.IsNullOrEmpty(Photo);

    public bool IsRecovery => Status == CaseStatus.Recovery;

    /// <summary>"next 2:41" while waiting, "overdue 0:40" once late: the sign is in the words, not hidden by an absolute value.</summary>
    public string ClockText => DueStateName switch
    {
        nameof(DueState.Overdue) => Loc.F("Clock_Overdue", "overdue {0}", NextText),
        nameof(DueState.None) => "",
        _ => Loc.F("Clock_Next", "next {0}", NextText),
    };

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

    public const int MonitorTab = 2;

    public const int RecoveryTab = 3;

    /// <summary>Tab the record opens on: Monitor while the patient is asleep, Recovery after, Pre-op before.</summary>
    public static int TabFor(CaseStatus status) => status switch
    {
        CaseStatus.Anesthetized => MonitorTab,
        CaseStatus.Recovery or CaseStatus.Signed => RecoveryTab,
        _ => 0,
    };

    public static CaseView From(Case c, DateTimeOffset now, TimeSpan interval)
    {
        var p = c.Patient;
        var due = Schedule.State(c, interval, now);
        var remaining = Schedule.Remaining(c, interval, now) ?? TimeSpan.Zero;
        var strip = StripData.From(c, now, interval);
        return new CaseView(
            c.Id,
            p.Name,
            $"{p.Species} · {p.WeightKg:0.0} kg · {c.Preop.AsaLabel}",
            c.Procedure,
            $"{c.Veterinarian} · {c.Technician}",
            c.Status,
            CaseSummary.StatusLabel(c.Status),
            p.WeightKg,
            $"{p.WeightKg:0.0}",
            string.Join(" · ", new[] { p.SpeciesAndBreed, p.AgeYears > 0 ? $"{p.AgeYears:0.#} y" : "", string.IsNullOrWhiteSpace(p.Owner) ? "" : $"owner {p.Owner}" }.Where(x => x.Length > 0)),
            c.Preop,
            Loc.F("Preop_Progress", "{0} of 5 checks", c.Preop.DoneCount),
            c.InducedAt is DateTimeOffset i ? Loc.F("Preop_InducedAt", "Induced at {0:HH:mm}", i) : Loc.T("Preop_NotInduced", "Not induced"),
            Schedule.HoursMinutes(Schedule.Elapsed(c, now)),
            due switch { DueState.Overdue => Loc.T("Due_Overdue", "Reading overdue"), DueState.None => Loc.T("Due_None", "No reading due"), _ => Loc.T("Due_Next", "Next reading") },
            due == DueState.None ? "—" : Schedule.Clock(remaining.Duration()),
            due.ToString(),
            Schedule.Progress(c, interval, now) * 100,
            c.Readings.Reverse().Select(r => Row(p.Species, r)).ToImmutableList(),
            c.Doses.Reverse().Select(DoseRowOf).ToImmutableList(),
            c.Readings.Count,
            strip,
            strip.Summary(p.Species),
            c.Readings.Count > 0 ? $"Last recorded {c.Readings[^1].At:dd MMM · HH:mm}" : Loc.T("Readings_None", "No readings yet"),
            // A signed record stops its recovery clock at the signature.
            c.Recovery.ExtubatedAt is DateTimeOffset x ? Schedule.HoursMinutes((c.SignedAt ?? now) - x) : c.EndedAt is DateTimeOffset e ? Schedule.HoursMinutes((c.SignedAt ?? now) - e) : "—",
            c.Recovery.ExtubatedAt is not null ? Loc.T("Recovery_SinceExtubation", "since extubation") : c.EndedAt is not null ? Loc.T("Recovery_SinceEnded", "since anesthesia ended (not extubated)") : Loc.T("Recovery_NotEnded", "anesthesia not ended"),
            c.Recovery.ExtubatedAt is DateTimeOffset ex ? Loc.F("Recovery_Extubated", "Extubated {0:HH:mm}", ex) : "",
            c.Recovery.SternalAt is DateTimeOffset st ? Loc.F("Recovery_Sternal", "Sternal {0:HH:mm}", st) : "",
            c.SignedBy is { } by ? Loc.F("Recovery_SignedBy", "Signed by {0} at {1:HH:mm}. The record is read-only.", by, c.SignedAt) : "")
        {
            Photo = PatientMedia.Photo(p), HeroPhoto = PatientMedia.Hero(p),
            Breed = string.IsNullOrWhiteSpace(p.Breed) ? Loc.Species(p.Species) : p.Breed,
            AgeOwner = $"{(p.AgeYears == 1 ? Loc.T("Patient_OneYear", "1 year") : Loc.F("Patient_AgeYears", "{0:0.#} years", p.AgeYears))} · {p.Owner}",
            WeightDate = $"Recorded {c.CreatedAt:dd MMM yyyy}", VisitDate = $"{c.CreatedAt:dd MMM yyyy}",
            LastHr = c.Readings.LastOrDefault()?.Hr?.ToString() ?? "—",
            LastSpo2 = c.Readings.LastOrDefault()?.SpO2?.ToString() ?? "—",
            LastBp = c.Readings.LastOrDefault() is { } bp ? $"{bp.Sys?.ToString() ?? "—"}/{bp.Dia?.ToString() ?? "—"}" : "—",
            LastTemp = c.Readings.LastOrDefault()?.TempC?.ToString("0.0") ?? "—",
            LastEtCo2 = c.Readings.LastOrDefault()?.EtCo2?.ToString() ?? "—",
            LastMap = c.Readings.LastOrDefault()?.EffectiveMap?.ToString() ?? "—",
            LastRr = c.Readings.LastOrDefault()?.Rr?.ToString() ?? "—",
            Alert = c.Readings.LastOrDefault() is { } last ? Row(p.Species, last).Alarms : ""
        };
    }

    private static ReadingRow Row(Species species, VitalsReading r)
    {
        static string N(int? v) => v?.ToString() ?? "—";
        var alarms = Vitals.Alarms(species, r)
            .Select(a => $"{Vitals.Label(a.Kind)} {(a.Class == RangeClass.High ? Loc.T("Alarm_High", "▲ high") : Loc.T("Alarm_Low", "▼ low"))}");
        return new ReadingRow(
            $"{r.At:HH:mm}", N(r.Hr), N(r.Rr), N(r.SpO2), N(r.EtCo2),
            r.Sys is null && r.Dia is null ? "—" : $"{N(r.Sys)}/{N(r.Dia)}",
            r.EffectiveMap is int m ? (r.MapIsCalculated ? Loc.F("Map_Calc", "{0} calc", m) : $"{m}") : "—",
            r.TempC is decimal t ? $"{t:0.0}" : "—",
            Loc.Plane(r.Plane),
            string.Join(" · ", alarms),
            r.Note);
    }

    private static DoseRow DoseRowOf(DoseGiven d) => new(
        $"{d.At:HH:mm}", d.DrugName, $"{d.MgPerKg:0.###} mg/kg", $"{d.VolumeMl:0.00} mL", d.Route.ToString(),
        d.OutOfRangeConfirmed ? Loc.T("Dose_OutOfRangeConfirmed", "Out of range, confirmed") : "");
}

public partial record RecordModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, IRecordExporter Exporter, INavigator Navigator, DoseFeedback Feedback, IFormulary Formulary)
{
#if DEBUG
    private readonly Diagnostics.LiveCounter _alive = new(nameof(RecordModel));
#endif

    // The stored case; null (deleted or never existed) renders the None template.
    public IState<bool> PaneOpen => State.Value(this, () => false);
    public IState<string> Pane => State.Value(this, () => "Preop");
    public IFeed<bool> PanePreop => Pane.Select(x => x == "Preop");
    public IFeed<bool> PaneDose => Pane.Select(x => x == "Dose");
    public IFeed<bool> PaneDrugs => Pane.Select(x => x == "Drugs");
    public IFeed<bool> PaneMonitor => Pane.Select(x => x == "Monitor");
    public IFeed<bool> PaneRecovery => Pane.Select(x => x == "Recovery");
    public IFeed<bool> PaneHistory => Pane.Select(x => x == "History");
    public IFeed<bool> PaneLabs => Pane.Select(x => x == "Labs");
    public IFeed<string> PaneTitle => Pane.Select(x => x switch { "Dose" => "Administer dose", "Preop" => "Preoperative preparation", "Drugs" => "Medication record", "Monitor" => "Record observations", "Recovery" => "Recovery & handover", "History" => "Visit history", _ => "Lab results" });
    public async ValueTask OpenPane(string section, CancellationToken ct)
    {
        await Pane.UpdateAsync(_ => section, ct);
        await PaneOpen.UpdateAsync(_ => true, ct);
    }
    public async ValueTask ClosePane(CancellationToken ct) => await PaneOpen.UpdateAsync(_ => false, ct);
    public async ValueTask OpenParentPreview(CancellationToken ct) => await Navigator.NavigateRouteAsync(this, "ParentPreview", data: new CaseRef(Ref.Id), cancellation: ct);
    public async ValueTask OpenSignOff(CancellationToken ct) => await Navigator.NavigateRouteAsync(this, "!SignOff", data: new CaseRef(Ref.Id), cancellation: ct);
    public IFeed<long> DosePulse => Now.Select(_ => Feedback.PulseFor(Ref.Id));
    public IFeed<bool> ShowAcknowledgement => DosePulse.Select(x => x > 0);

    private IFeed<Case> Source { get; } = Feed<Case>.Async(
        async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(c) : Option.None<Case>(), Store.Changed);

    private IFeed<DateTimeOffset> Now { get; } = Feed.AsyncEnumerable(Clock.Ticks);

    public IFeed<CaseView> Case => Feed.Combine(Source, Now).Select(x => CaseView.From(x.Item1, x.Item2, Preferences.ReadingInterval));

    public IFeed<string> Title => Source.Select(c => $"{c.Patient.Name} · {c.Patient.WeightKg:0.0} kg · {c.Procedure}");

    /// <summary>The pre-op checklist, edited in place and saved on every change while the case is scheduled.</summary>
    public IState<PreopCheck> Preop => State
        .Async(this, async ct => (await Store.GetAsync(Ref.Id, ct))?.Preop ?? PreopCheck.Empty)
        .ForEach(SavePreopAsync);

    /// <summary>The selected tab. Opens where the case is (Monitor while asleep); the user owns it after that.</summary>
    public IState<int> Tab => State.Async(this, async ct =>
        (await Store.GetAsync(Ref.Id, ct)) is { } c ? CaseView.TabFor(c.Status) : 0);

    /// <summary>Store write failures surface here (InfoBar) with the action to retry.</summary>
    public IState<string> SaveError => State.Value(this, () => "");

    public IFeed<bool> HasSaveError => SaveError.Select(s => !string.IsNullOrEmpty(s));

    public void Retry() => Store.Reload();

    /// <summary>The Monitor pad, pre-filled from the last reading. Single writer: the pad; reset by RecordReading.</summary>
    public IState<VitalsDraft> Draft => State.Async(this, async ct =>
        VitalsDraft.From((await Store.GetAsync(Ref.Id, ct))?.Readings.LastOrDefault()));

    public IFeed<DraftFlags> Flags => Feed.Combine(Draft, Source).Select(x => DraftFlags.From(x.Item1, x.Item2.Patient.Species));

    /// <summary>Strip or table: the table is the accessible alternative to the drawing.</summary>
    public IState<bool> ShowTable => State.Value(this, () => false);

    public IFeed<bool> ShowStrip => ShowTable.Select(t => !t);

    /// <summary>In-app setting OR'ed with the platform's; the strip draws new columns final instead of fading them.</summary>
    public IFeed<bool> ReduceMotion => Feed.Async(_ => ValueTask.FromResult(Preferences.ReduceMotion), Preferences.Changed);

    public async ValueTask Step(string arg, CancellationToken ct) =>
        await Draft.UpdateAsync(d => (d ?? VitalsDraft.Empty).Step(arg), ct);

    public async ValueTask ToggleTable(CancellationToken ct) => await ShowTable.UpdateAsync(v => !v, ct);

    public async ValueTask RecordReading(VitalsDraft draft, CancellationToken ct)
    {
        var errors = draft.Validate();
        if (errors.Count > 0)
        {
            await SaveError.UpdateAsync(_ => string.Join(" ", errors), ct);
            return;
        }
        var reading = draft.ToReading(Clock.Now);
        var saved = false;
        await SaveAsync(c =>
        {
            if (c.Status != CaseStatus.Anesthetized)
            {
                throw new InvalidOperationException(Loc.T("Error_InduceFirst", "Induce before recording vitals."));
            }
            saved = true;
            return c with { Readings = c.Readings.Add(reading) };
        }, ct);
        if (saved)
        {
            // Next reading starts from this one: an unchanged patient is a single tap.
            await Draft.UpdateAsync(_ => VitalsDraft.From(reading), ct);
        }
    }

    public async ValueTask Induce(CancellationToken ct)
    {
        await SaveAsync(c => c.Status == CaseStatus.Scheduled
            ? c with { Status = CaseStatus.Anesthetized, InducedAt = Clock.Now }
            : c, ct);
        await Tab.UpdateAsync(_ => CaseView.MonitorTab, ct);

        await PaneOpen.UpdateAsync(_ => false, ct);
    }

    public async ValueTask DismissError(CancellationToken ct) => await SaveError.UpdateAsync(_ => "", ct);

    /// <summary>The editable part of the recovery log, saved on every change while the case is in recovery.</summary>
    public IState<RecoveryDraft> Recovery => State
        .Async(this, async ct => RecoveryDraft.From((await Store.GetAsync(Ref.Id, ct))?.Recovery ?? RecoveryLog.Empty))
        .ForEach(async (draft, ct) =>
        {
            if (draft is not null)
            {
                await SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = draft.ApplyTo(c.Recovery) } : c, ct);
            }
        });

    /// <summary>Outcome of the last export, shown under the export buttons.</summary>
    public IState<string> Notice => State.Value(this, () => "");

    public async ValueTask EndAnesthesia(CancellationToken ct)
    {
        await SaveAsync(c => c.Status == CaseStatus.Anesthetized ? c with { Status = CaseStatus.Recovery, EndedAt = Clock.Now } : c, ct);
        await Tab.UpdateAsync(_ => CaseView.RecoveryTab, ct);
    }

    public async ValueTask Extubate(CancellationToken ct) =>
        await SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = c.Recovery with { ExtubatedAt = Clock.Now } } : c, ct);

    public async ValueTask Sternal(CancellationToken ct) =>
        await SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = c.Recovery with { SternalAt = Clock.Now } } : c, ct);

    public async ValueTask Export(CancellationToken ct) => await HandOffAsync(Exporter.ExportCsvAsync, ct);

    public async ValueTask CopySummary(CancellationToken ct) => await HandOffAsync(Exporter.CopySummaryAsync, ct);

    private async ValueTask HandOffAsync(Func<Case, CancellationToken, ValueTask<string>> action, CancellationToken ct)
    {
        string outcome;
        try
        {
            outcome = await Store.GetAsync(Ref.Id, ct) is { } c ? await action(c, ct) : Loc.T("Record_Gone", "This record no longer exists.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            outcome = Loc.F("Export_Failed", "Export failed: {0}", ex.Message);
        }
        await Notice.UpdateAsync(_ => outcome, ct);
    }

    private async ValueTask SavePreopAsync(PreopCheck? preop, CancellationToken ct)
    {
        if (preop is null)
        {
            return;
        }
        await SaveAsync(c => c.Status == CaseStatus.Scheduled ? c with { Preop = preop } : c, ct);
    }

    private async ValueTask SaveAsync(Func<Case, Case> update, CancellationToken ct)
    {
        try
        {
            await Store.UpdateAsync(Ref.Id, update, ct);
            await SaveError.UpdateAsync(_ => "", ct);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException)
        {
            await SaveError.UpdateAsync(_ => Loc.F("Error_NotSaved", "Not saved: {0}", ex.Message), ct);
        }
    }

    public IState<Drug> DoseSelected => State<Drug>.Empty(this).ForEach(async (drug, ct) =>
    {
        if (drug is null) return;
        await DoseMgPerKg.UpdateAsync(_ => drug.DefaultMgPerKg.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), ct);
        await DoseRouteIndex.UpdateAsync(_ => (int)drug.DefaultRoute, ct);
    });
    public IListFeed<Drug> DoseDrugs => ListFeed.Async(async ct => (IImmutableList<Drug>)await Formulary.GetDrugsAsync(ct)).Selection(DoseSelected);
    public IState<string> DoseMgPerKg => State.Value(this, () => "").ForEach(async (_, ct) => await DoseConfirming.UpdateAsync(_ => false, ct));
    public IState<int> DoseRouteIndex => State.Value(this, () => 0);
    public IFeed<DoseCalc> DoseCalculation => Feed.Combine(Source, DoseSelected, DoseMgPerKg).Select(x =>
        VitalsDraft.ParseDec(x.Item3) is decimal mg && mg > 0 ? Dosing.Calculate(x.Item1.Patient.Species, x.Item1.Patient.WeightKg, x.Item2, mg) : null!);
    public IState<string> DoseError => State.Value(this, () => "");
    public IState<bool> DoseConfirming => State.Value(this, () => false);
    public IFeed<bool> DoseNotConfirming => DoseConfirming.Select(x => !x);
    public IFeed<string> DoseConfirmTitle => DoseCalculation.Select(c => Loc.F("Dose_ConfirmTitle", "Give {0:0.###} mg/kg?", c.MgPerKg));
    public IFeed<string> DoseConfirmText => Feed.Combine(DoseCalculation, DoseSelected).Select(x => Loc.F("Dose_ConfirmText", "The reference range for {0} is {1}. {2:0.###} mg/kg is {3}.", x.Item2.Name, x.Item1.ReferenceText, x.Item1.MgPerKg, x.Item1.VolumeText));
    public async ValueTask GiveDose(DoseCalc doseCalculation, Drug doseSelected, int doseRouteIndex, CancellationToken ct)
    {
        if (doseCalculation.IsOutOfRange) { await DoseConfirming.UpdateAsync(_ => true, ct); return; }
        await SaveDrawerDose(doseCalculation, doseSelected, doseRouteIndex, ct);
    }
    public ValueTask GiveDoseAnyway(DoseCalc doseCalculation, Drug doseSelected, int doseRouteIndex, CancellationToken ct) => SaveDrawerDose(doseCalculation, doseSelected, doseRouteIndex, ct);
    public async ValueTask ChangeRecordedDose(CancellationToken ct) => await DoseConfirming.UpdateAsync(_ => false, ct);
    private async ValueTask SaveDrawerDose(DoseCalc calculation, Drug drug, int route, CancellationToken ct)
    {
        var dose = new DoseGiven(Guid.NewGuid(), Clock.Now, drug.Id, drug.Name, calculation.MgPerKg, calculation.MgPerMl, calculation.VolumeMl, (DoseRoute)Math.Clamp(route, 0, 3), calculation.IsOutOfRange);
        try { await Store.UpdateAsync(Ref.Id, c => c with { Doses = c.Doses.Add(dose) }, ct); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException)
        {
            await DoseError.UpdateAsync(_ => Loc.F("Error_NotSaved", "Not saved: {0}", ex.Message), ct);
            return;
        }
        Feedback.Recorded(Ref.Id);
        await DoseError.UpdateAsync(_ => "", ct);
        await DoseConfirming.UpdateAsync(_ => false, ct);
        await ClosePane(ct);
    }

}
