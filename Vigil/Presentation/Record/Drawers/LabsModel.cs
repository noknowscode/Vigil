namespace Vigil.Presentation;

/// <summary>
/// The Lab results drawer (route "!Labs"): which panels are pending, ready and reviewed; Mark reviewed (before induction
/// it also ticks the pre-op bloodwork check); View preparation.
/// </summary>
public partial record LabsModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, INavigator Navigator, ILogger<LabsModel> Logger)
{
    private CaseDrawer Drawer => _drawer ??= new(this, Ref, Store, Clock, Preferences, Navigator, Logger);
    private CaseDrawer? _drawer;

    public IFeed<CaseView> Case => Drawer.View;
    public IState<string> SaveError => Drawer.Error;
    public IFeed<bool> HasSaveError => Drawer.HasError;
    public async ValueTask DismissError(CancellationToken ct) => await Drawer.DismissErrorAsync(ct);
    public void Retry() => Drawer.Retry();
    public async ValueTask Close(CancellationToken ct) => await Drawer.CloseAsync(ct);

    public async ValueTask ReviewLab(string panel, CancellationToken ct) => await Drawer.SaveAsync(c => c.WithLabReviewed(panel), ct);

    /// <summary>View preparation: the Preparation drawer.</summary>
    public async ValueTask ViewPreparation(CancellationToken ct) => await Drawer.HandBackAsync(RecordPane.Preop, ct);
}
