namespace Vigil.Presentation;

/// <summary>
/// The Recovery and handover drawer (route "!Recovery"): End anesthesia while the case is under anesthesia, then the
/// recovery log (extubation, sternal, timed pain scores, temperature and notes), Sign off, the discharge call, and
/// Export CSV and Copy summary once signed.
/// </summary>
public partial record RecoveryModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, IFormulary Formulary, IRecordExporter Exporter, INavigator Navigator, ILogger<RecoveryModel> Logger)
{
    private CaseDrawer Drawer => _drawer ??= new(this, Ref, Store, Clock, Preferences, Navigator, Logger);
    private CaseDrawer? _drawer;

    public IFeed<CaseView> Case => Drawer.View;
    public IState<string> SaveError => Drawer.Error;
    public IFeed<bool> HasSaveError => Drawer.HasError;
    public async ValueTask DismissError(CancellationToken ct) => await Drawer.DismissErrorAsync(ct);
    public void Retry() => Drawer.Retry();
    public async ValueTask Close(CancellationToken ct) => await Drawer.CloseAsync(ct);

    private IFeed<DateTimeOffset> Now { get; } = Feed.AsyncEnumerable(Clock.Ticks);

    private IFeed<IImmutableList<Drug>> Drugs { get; } = Feed.Async(async ct => (IImmutableList<Drug>)await Formulary.GetDrugsAsync(ct));

    /// <summary>Time in recovery, moving with the clock.</summary>
    public IFeed<CaseTimers> Timers => Feed.Combine(Drawer.Source, Now, Drugs).Select(x =>
        CaseTimers.From(x.Item1, x.Item2, Preferences.ReadingInterval, x.Item3));

    /// <summary>The editable part of the recovery log, saved on every change while the case is in recovery.</summary>
    public IState<RecoveryDraft> Recovery => State
        .Async(this, async ct => RecoveryDraft.From((await Store.GetAsync(Ref.Id, ct))?.Recovery ?? RecoveryLog.Empty))
        .ForEach(async (draft, ct) =>
        {
            if (draft is not null)
            {
                await Drawer.SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = draft.ApplyTo(c.Recovery) } : c, ct);
            }
        });

    /// <summary>Outcome of the last export, shown under the export buttons.</summary>
    public IState<string> Notice => State.Value(this, () => "");

    public async ValueTask EndAnesthesia(CancellationToken ct) =>
        await Drawer.SaveAsync(c => c.Status == CaseStatus.Anesthetized ? c with { Status = CaseStatus.Recovery, EndedAt = Clock.Now } : c, ct);

    public async ValueTask Extubate(CancellationToken ct) =>
        await Drawer.SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = c.Recovery with { ExtubatedAt = Clock.Now } } : c, ct);

    public async ValueTask Sternal(CancellationToken ct) =>
        await Drawer.SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = c.Recovery with { SternalAt = Clock.Now } } : c, ct);

    /// <summary>Records a pain score (button "0" to "4") at the current time, while the case is in recovery.</summary>
    public async ValueTask ScorePain(string score, CancellationToken ct)
    {
        if (int.TryParse(score, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 4)
        {
            var at = Clock.Now;
            await Drawer.SaveAsync(c => c.Status == CaseStatus.Recovery ? c with { Recovery = c.Recovery.WithPainScore(value, at) } : c, ct);
        }
    }

    /// <summary>The discharge call to the pet parent was made.</summary>
    public async ValueTask CompleteDischargeCall(CancellationToken ct)
    {
        var at = Clock.Now;
        await Drawer.SaveAsync(c => c.WithDischargeCallDone(at), ct);
    }

    /// <summary>Sign off: the sign-off sheet, opened by the record.</summary>
    public async ValueTask SignOff(CancellationToken ct) => await Drawer.HandBackAsync(PaneChoice.SignOff, ct);

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
            Logger.LogWarning(ex, "Export of case {CaseId} failed.", Ref.Id);
            outcome = Loc.T("Export_FailedRetry", "The export failed. Nothing was shared; try again.");
        }
        await Notice.UpdateAsync(_ => outcome, ct);
    }
}
