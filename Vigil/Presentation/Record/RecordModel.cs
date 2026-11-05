namespace Vigil.Presentation;

/// <summary>
/// The record page (route Main/Record): the hero tray, the overdue banner, the procedure header and the metric cards
/// for one case, and the way into its panes. Each pane is a drawer with its own model (<see cref="RecordPane"/>,
/// <see cref="CaseDrawerModel"/>); a drawer that closes on a step for the record (another pane, Sign off, the case details,
/// Delete case) hands it back here.
/// </summary>
public partial record RecordModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, INavigator Navigator, DoseFeedback Feedback, IFormulary Formulary, ILogger<RecordModel> Logger)
{
#if DEBUG
    private readonly Diagnostics.LiveCounter _alive = new(nameof(RecordModel));
#endif

    /// <summary>
    /// The pane whose drawer is open ("" when none): the record nav highlights it. A "Due next" action opens the record
    /// on its task's pane (<see cref="CaseRefPane"/>); this state's initializer is where that drawer is opened, once:
    /// two first subscribers can both run the initializer, which opened the drawer twice.
    /// </summary>
    public IState<string> Pane => State.Value(this, () =>
    {
        if (Ref is CaseRefPane landing)
        {
            if (Interlocked.Exchange(ref _landed, 1) == 0)
            {
                _ = LandAsync(landing.Pane);
            }
            return landing.Pane;
        }
        return "";
    });

    private int _landed;

    /// <summary>
    /// Opens a pane's drawer over the record; once it closes, <see cref="FollowAsync"/> carries out what it handed back.
    /// The command ends when the drawer shows, not when it closes: an MVUX command is disabled while it runs, and the
    /// record nav and header buttons that share it would sit disabled under the drawer, out of reach of the focus the
    /// drawer gives back (FlyoutFocus).
    /// </summary>
    public async ValueTask OpenPane(string section, CancellationToken ct)
    {
        await Pane.UpdateAsync(_ => section, ct);
        var drawer = await Navigator.NavigateRouteForResultAsync<PaneChoice>(this, "!" + section, data: new CaseRef(Ref.Id));
        _ = FollowAsync(section, drawer);
    }

    /// <summary>When the drawer closes: the next pane, the sign-off or case-details sheet, Delete case, or nothing.</summary>
    private async Task FollowAsync(string section, NavigationResultResponse<PaneChoice>? drawer)
    {
        try
        {
            var next = drawer?.Result is { } closing ? (await closing).SomeOrDefault()?.Next : null;
            await Pane.UpdateAsync(_ => "", CancellationToken.None);
            switch (next)
            {
                case null:
                    break;
                case PaneChoice.SignOff:
                    await Navigator.NavigateRouteAsync(this, "!SignOff", data: new CaseRef(Ref.Id));
                    break;
                case PaneChoice.EditCase:
                    await Navigator.NavigateRouteAsync(this, "!EditCase", data: new CaseRef(Ref.Id));
                    break;
                case PaneChoice.DeleteCase:
                    await DeleteCase(CancellationToken.None);
                    break;
                default:
                    await OpenPane(next, CancellationToken.None);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not awaited by the command: a failure is logged, and the record stays open with no drawer.
            Logger.LogWarning(ex, "Following the {Pane} pane of case {CaseId} failed.", section, Ref.Id);
        }
    }

    private async Task LandAsync(string pane)
    {
        await Task.Yield();
        try
        {
            await OpenPane(pane, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fire and forget from the state's initializer: a failure is logged, and the record stays open with no drawer.
            Logger.LogWarning(ex, "Opening the {Pane} pane of case {CaseId} failed.", pane, Ref.Id);
        }
    }

    public async ValueTask OpenParentPreview(CancellationToken ct) => await Navigator.NavigateRouteAsync(this, "ParentPreview", data: new CaseRef(Ref.Id), cancellation: ct);

    public IFeed<long> DosePulse => Now.Select(_ => Feedback.PulseFor(Ref.Id));
    public IFeed<bool> ShowAcknowledgement => DosePulse.Select(x => x > 0);

    // The stored case; null (deleted or never existed) renders the None template.
    private IFeed<Case> Source { get; } = Feed<Case>.Async(
        async ct => await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(c) : Option.None<Case>(), Store.Changed);

    private IFeed<DateTimeOffset> Now { get; } = Feed.AsyncEnumerable(Clock.Ticks);

    // Pane is a state (always a value), so it joins the combine without None propagation; the template reads the active
    // nav item from Data (Parent.State does not resolve reliably inside a FeedView template).
    // No clock here: the view (and its reading, dose and lab lists) is rebuilt when the case changes, not every second.
    public IFeed<CaseView> Case => Feed.Combine(Source, Pane).Select(x =>
        CaseView.From(x.Item1, Clock.Now, Preferences.ReadingInterval, string.IsNullOrEmpty(x.Item2) ? null : x.Item2));

    /// <summary>The values that move with the clock (timers, countdowns), next to <see cref="Case"/>; the page binds Parent.Timers.</summary>
    public IFeed<CaseTimers> Timers => CaseTimers.Ticking(Source, Now, Formulary, Preferences);

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

    /// <summary>A failed delete surfaces here (InfoBar); the panes report their own saves in their drawers.</summary>
    public IState<string> SaveError => State.Value(this, () => "");

    public IFeed<bool> HasSaveError => SaveError.Select(s => !string.IsNullOrEmpty(s));

    public void Retry() => Store.Reload();

    /// <summary>In-app setting OR'ed with the platform's; the care mark skips its acknowledgement turn.</summary>
    public IFeed<bool> ReduceMotion => Feed.Async(_ => ValueTask.FromResult(Preferences.ReduceMotion), Preferences.Changed);

    public async ValueTask DismissError(CancellationToken ct) => await SaveError.UpdateAsync(_ => "", ct);

    /// <summary>
    /// Delete case (from the Preparation drawer): deletes a booked case after a confirmation (booked by mistake, or the
    /// patient is not coming), then leaves the record. The store refuses a signed record.
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
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            Logger.LogWarning(ex, "Deleting case {CaseId} failed.", Ref.Id);
            await SaveError.UpdateAsync(_ => Loc.F("Record_NotDeleted", "Not deleted: {0}", StoreErrors.Describe(ex)), ct);
            return;
        }
        await GoBack(ct);
    }
}
