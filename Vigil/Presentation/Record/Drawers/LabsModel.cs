namespace Vigil.Presentation;

/// <summary>
/// The Lab results drawer (route "!Labs"): which panels are pending, ready and reviewed; Mark reviewed (before induction
/// it also ticks the pre-op bloodwork check); View preparation.
/// </summary>
public partial record LabsModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, INavigator Navigator, ILogger<LabsModel> Logger)
    : CaseDrawerModel(Ref, Store, Clock, Preferences, Navigator, Logger)
{
    public async ValueTask ReviewLab(string panel, CancellationToken ct) => await SaveAsync(c => c.WithLabReviewed(panel), ct);

    /// <summary>View preparation: the Preparation drawer.</summary>
    public async ValueTask ViewPreparation(CancellationToken ct) => await HandBackAsync(RecordPane.Preop, ct);
}
