namespace Vigil.Presentation;

public partial record RecordModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, IRecordExporter Exporter, INavigator Navigator, DoseFeedback Feedback, IFormulary Formulary)
{
#if DEBUG
    private readonly Diagnostics.LiveCounter _alive = new(nameof(RecordModel));
#endif

    // The stored case; null (deleted or never existed) renders the None template.
    // A "Due next" action opens the record on its task's pane (CaseRefPane); everything else opens it closed.
    public IState<bool> PaneOpen => State.Value(this, () => Ref is CaseRefPane);
    public IState<string> Pane => State.Value(this, () => Ref is CaseRefPane open ? open.Pane : "Preop");
    public IFeed<bool> PanePreop => Pane.Select(x => x == "Preop");
    public IFeed<bool> PaneDose => Pane.Select(x => x == "Dose");
    public IFeed<bool> PaneDrugs => Pane.Select(x => x == "Drugs");
    public IFeed<bool> PaneMonitor => Pane.Select(x => x == "Monitor");
    public IFeed<bool> PaneRecovery => Pane.Select(x => x == "Recovery");
    public IFeed<bool> PaneHistory => Pane.Select(x => x == "History");
    public IFeed<bool> PaneLabs => Pane.Select(x => x == "Labs");
    public IFeed<string> PaneTitle => Pane.Select(x => x switch { "Dose" => Loc.T("Pane_Dose", "Administer dose"), "Preop" => Loc.T("Pane_Preop", "Preoperative preparation"), "Drugs" => Loc.T("Pane_Drugs", "Medication record"), "Monitor" => Loc.T("Pane_Monitor", "Record observations"), "Recovery" => Loc.T("Pane_Recovery", "Recovery & handover"), "History" => Loc.T("Pane_History", "Visit history"), _ => Loc.T("Pane_Labs", "Lab results") });
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

    private IFeed<IImmutableList<Drug>> Drugs { get; } = Feed.Async(async ct => (IImmutableList<Drug>)await Formulary.GetDrugsAsync(ct));

    // Pane and PaneOpen are states (always a value), so they join the combine without None propagation; the template
    // reads the active nav item from Data (Parent.State does not resolve reliably inside a FeedView template).
    public IFeed<CaseView> Case => Feed.Combine(Source, Now, Drugs, Pane, PaneOpen).Select(x =>
        CaseView.From(x.Item1, x.Item2, Preferences.ReadingInterval, x.Item3, x.Item5 ? x.Item4 : null));

    /// <summary>Patient name for the breadcrumb (outside the FeedView, so it shows in every state).</summary>
    public IFeed<string> Name => Source.Select(c => c.Patient.Name);

    /// <summary>Back: the previous page; when the frame has no history (opened from the sidebar or New patient), Today.</summary>
    public async ValueTask GoBack(CancellationToken ct)
    {
        var response = await Navigator.NavigateBackAsync(this, cancellation: ct);
        if (response?.Success != true)
        {
            await Navigator.NavigateRouteAsync(this, "-/" + ShellSection.Today, cancellation: ct);
        }
    }

    /// <summary>Breadcrumb "Patients": the Patients list, as a sidebar pick would open it.</summary>
    public async ValueTask OpenPatients(CancellationToken ct) =>
        await Navigator.NavigateRouteAsync(this, "-/" + ShellSection.Patients, cancellation: ct);

    public IFeed<string> Title => Source.Select(c => $"{c.Patient.Name} · {c.Patient.WeightKg:0.0} kg · {c.Procedure}");

    /// <summary>
    /// The pre-op checklist, edited in place and saved on every change while the case is scheduled. A save writes only
    /// the fields the user changed, so a check set elsewhere (Mark reviewed ticks the bloodwork) is never written over.
    /// </summary>
    public IState<PreopCheck> Preop => State
        .Async(this, async ct => _preopShown = (await Store.GetAsync(Ref.Id, ct))?.Preop ?? PreopCheck.Empty)
        .ForEach(SavePreopAsync);

    // The checklist as last loaded or saved: the next change is measured against it.
    private PreopCheck _preopShown = PreopCheck.Empty;

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

    /// <summary>
    /// Preparation pane: deletes a booked case after a confirmation (booked by mistake, a mistyped weight, or the patient
    /// is not coming), then leaves the record. The store refuses a signed record.
    /// </summary>
    public async ValueTask DeleteCase(CancellationToken ct)
    {
        if (!await Navigator.ConfirmAsync(this, Loc.T("Record_DeleteConfirm", "Delete this case and everything recorded in it? This cannot be undone."), Loc.T("Record_DeleteTitle", "Delete case"), Loc.T("Record_DeleteAction", "Delete case"), ct))
        {
            return;
        }
        try
        {
            await Store.DeleteAsync(Ref.Id, ct);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            await SaveError.UpdateAsync(_ => Loc.T("Record_DeleteFailed", "The case could not be deleted. Try again."), ct);
            return;
        }
        await GoBack(ct);
    }

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

    /// <summary>Lab results: marks a ready panel as reviewed (before induction that also ticks the bloodwork check).</summary>
    public async ValueTask ReviewLab(string panel, CancellationToken ct)
    {
        await SaveAsync(c => c.WithLabReviewed(panel), ct);
        // The checklist is an edited copy of the stored one: show the tick there as well.
        if (await Store.GetAsync(Ref.Id, ct) is { } saved)
        {
            await Preop.UpdateAsync(p => (p ?? saved.Preop) with { BloodworkReviewed = saved.Preop.BloodworkReviewed }, ct);
        }
    }

    /// <summary>Recovery pane: the discharge call to the pet parent was made.</summary>
    public async ValueTask CompleteDischargeCall(CancellationToken ct)
    {
        var at = Clock.Now;
        await SaveAsync(c => c.WithDischargeCallDone(at), ct);
    }

    /// <summary>Medication record pane: the fluid to start, prefilled with the usual maintenance run.</summary>
    public IState<string> FluidName => State.Value(this, () => "LRS");

    /// <summary>Rate in mL/kg/h, text while typing like the other numeric fields.</summary>
    public IState<string> FluidRate => State.Value(this, () => "5");

    public async ValueTask StartFluids(string fluidName, string fluidRate, CancellationToken ct)
    {
        if (VitalsDraft.ParseDec(fluidRate ?? "") is not decimal rate || rate <= 0 || rate > Domain.Case.MaxFluidRate || string.IsNullOrWhiteSpace(fluidName))
        {
            await SaveError.UpdateAsync(_ => Loc.F("Fluids_Invalid", "Enter a fluid and a rate above 0 and up to {0} mL/kg/h.", Domain.Case.MaxFluidRate), ct);
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

    /// <summary>Records a pain score (button "0" to "4") at the current time, while the case is in recovery.</summary>
    public async ValueTask ScorePain(string score, CancellationToken ct)
    {
        if (int.TryParse(score, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 4)
        {
            var at = Clock.Now;
            await SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = c.Recovery.WithPainScore(value, at) } : c, ct);
        }
    }

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
        var before = _preopShown;
        _preopShown = preop;
        await SaveAsync(c => c.Status == CaseStatus.Scheduled ? c with { Preop = c.Preop.WithEdits(before, preop) } : c, ct);
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

    // The dose pane: the dose entry the Dose page uses too, under Dose* names. Give closes the pane once saved.
    private DoseEntry Dose => _dose ??= new(this, Ref.Id, Source, Store, Formulary, Clock, Feedback);
    private DoseEntry? _dose;

    public IState<Drug> DoseSelected => Dose.Selected;
    public IListFeed<Drug> DoseDrugs => Dose.Drugs;
    public IState<string> DoseMgPerKg => Dose.MgPerKg;
    public IState<int> DoseRouteIndex => Dose.RouteIndex;
    public IFeed<DoseCalc> DoseCalculation => Dose.Calc;
    public IState<string> DoseError => Dose.Error;
    public IState<bool> DoseConfirming => Dose.Confirming;
    public IFeed<bool> DoseNotConfirming => Dose.NotConfirming;
    public IFeed<string> DoseConfirmTitle => Dose.ConfirmTitle;
    public IFeed<string> DoseConfirmText => Dose.ConfirmText;

    public async ValueTask GiveDose(DoseCalc doseCalculation, Drug doseSelected, int doseRouteIndex, CancellationToken ct)
    {
        if (await Dose.GiveAsync(doseCalculation, doseSelected, doseRouteIndex, ct))
        {
            await ClosePane(ct);
        }
    }

    public async ValueTask GiveDoseAnyway(DoseCalc doseCalculation, Drug doseSelected, int doseRouteIndex, CancellationToken ct)
    {
        if (await Dose.GiveAnywayAsync(doseCalculation, doseSelected, doseRouteIndex, ct))
        {
            await ClosePane(ct);
        }
    }

    public async ValueTask ChangeRecordedDose(CancellationToken ct) => await Dose.ChangeAsync(ct);
}
