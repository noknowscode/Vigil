namespace Vigil.Presentation;

/// <summary>The Visit history drawer (route "!History"): the visit, its team and every reading in this record, newest first.</summary>
public partial record HistoryModel(CaseRef Ref, ICaseStore Store, IClock Clock, IPreferences Preferences, INavigator Navigator, ILogger<HistoryModel> Logger)
    : CaseDrawerModel(Ref, Store, Clock, Preferences, Navigator, Logger);
