namespace Vigil.Presentation;

/// <summary>
/// The Preparation drawer (route "!Preop"): weight and signalment, the pre-op checklist and ASA status saved field by
/// field while the case is booked, Induce, and the hand-backs for premedication and Delete case.
/// </summary>
public partial record PreopModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, INavigator Navigator, ILogger<PreopModel> Logger)
{
    private CaseDrawer Drawer => _drawer ??= new(this, Ref, Store, Clock, Preferences, Navigator, Logger);
    private CaseDrawer? _drawer;

    public IFeed<CaseView> Case => Drawer.View;
    public IState<string> SaveError => Drawer.Error;
    public IFeed<bool> HasSaveError => Drawer.HasError;
    public async ValueTask DismissError(CancellationToken ct) => await Drawer.DismissErrorAsync(ct);
    public void Retry() => Drawer.Retry();
    public async ValueTask Close(CancellationToken ct) => await Drawer.CloseAsync(ct);

    /// <summary>
    /// The pre-op checklist, edited in place and saved on every change while the case is scheduled. A save writes only
    /// the fields the user changed, so a check set elsewhere (Mark reviewed ticks the bloodwork) is never written over.
    /// </summary>
    public IState<PreopCheck> Preop => State
        .Async(this, async ct => _preopShown = (await Store.GetAsync(Ref.Id, ct))?.Preop ?? PreopCheck.Empty)
        .ForEach(SavePreopAsync);

    // The checklist as last loaded or saved: the next change is measured against it.
    private PreopCheck _preopShown = PreopCheck.Empty;

    /// <summary>Starts anesthesia (the reading clock) once the checks are complete, then closes the drawer.</summary>
    public async ValueTask Induce(CancellationToken ct)
    {
        if (await Drawer.SaveAsync(c => c.Status == CaseStatus.Scheduled ? c with { Status = CaseStatus.Anesthetized, InducedAt = Clock.Now } : c, ct))
        {
            await Drawer.CloseAsync(ct);
        }
    }

    /// <summary>Give premedication: the dose drawer.</summary>
    public async ValueTask GivePremedication(CancellationToken ct) => await Drawer.HandBackAsync(RecordPane.Dose, ct);

    /// <summary>A booked case made by mistake: the record confirms, deletes it and leaves.</summary>
    public async ValueTask DeleteCase(CancellationToken ct) => await Drawer.HandBackAsync(PaneChoice.DeleteCase, ct);

    private async ValueTask SavePreopAsync(PreopCheck? preop, CancellationToken ct)
    {
        if (preop is null)
        {
            return;
        }
        var before = _preopShown;
        _preopShown = preop;
        await Drawer.SaveAsync(c => c.Status == CaseStatus.Scheduled ? c with { Preop = c.Preop.WithEdits(before, preop) } : c, ct);
    }
}
