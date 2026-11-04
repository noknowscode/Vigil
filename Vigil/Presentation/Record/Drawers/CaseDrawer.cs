namespace Vigil.Presentation;

/// <summary>The record's panes, each a drawer route ("!Preop") over the record page, and the record nav's states.</summary>
public static class RecordPane
{
    public const string Preop = "Preop";
    public const string Dose = "Dose";
    public const string Drugs = "Drugs";
    public const string Monitor = "Monitor";
    public const string Recovery = "Recovery";
    public const string History = "History";
    public const string Labs = "Labs";

    /// <summary>The panes with an item in the record nav; the others (Dose, Recovery) highlight no item while open.</summary>
    public static bool IsNavItem(string pane) => pane is Preop or Monitor or History or Labs or Drugs;
}

/// <summary>
/// What a drawer hands back to the record when it closes on a step the record carries out: open another pane
/// (<see cref="RecordPane"/> names), or one of the record-level steps below. Closing without a choice hands back nothing.
/// </summary>
public sealed record PaneChoice(string Next)
{
    /// <summary>The sign-off sheet.</summary>
    public const string SignOff = "SignOff";

    /// <summary>The case-details sheet.</summary>
    public const string EditCase = "EditCase";

    /// <summary>Delete the booked case (the record asks first, then leaves).</summary>
    public const string DeleteCase = "DeleteCase";
}

/// <summary>
/// What every record drawer model shares: its case (None once it is gone), the record's view of it, a save that
/// reports a failure in the drawer's Not saved bar, and the two ways out (close, or hand a step back to the record).
/// Each drawer model owns one and exposes the members its view binds, as <see cref="DoseEntry"/> does for the dose.
/// </summary>
public sealed class CaseDrawer
{
    private readonly object _owner;
    private readonly Guid _caseId;
    private readonly ICaseStore _store;
    private readonly INavigator _navigator;
    private readonly ILogger _logger;

    public CaseDrawer(object owner, CaseRef caseRef, ICaseStore store, IClock clock, IPreferences preferences, INavigator navigator, ILogger logger)
    {
        _owner = owner;
        _caseId = caseRef.Id;
        _store = store;
        _navigator = navigator;
        _logger = logger;
        Source = Feed<Case>.Async(async ct => await store.GetAsync(_caseId, ct) is { } c ? Option.Some(c) : Option.None<Case>(), store.Changed);
        View = Source.Select(c => CaseView.From(c, clock.Now, preferences.ReadingInterval));
        Error = State.Value(owner, () => "");
        HasError = Error.Select(s => !string.IsNullOrEmpty(s));
    }

    /// <summary>The stored case, reloaded on every store change; None once deleted.</summary>
    public IFeed<Case> Source { get; }

    /// <summary>The record's read-only view of the case (the drawer's FeedView source).</summary>
    public IFeed<CaseView> View { get; }

    /// <summary>A failed save or a rule of the record, as a sentence (the drawer's Not saved bar).</summary>
    public IState<string> Error { get; }

    public IFeed<bool> HasError { get; }

    public void Retry() => _store.Reload();

    public async ValueTask ShowErrorAsync(string message, CancellationToken ct) => await Error.UpdateAsync(_ => message, ct);

    public async ValueTask DismissErrorAsync(CancellationToken ct) => await Error.UpdateAsync(_ => "", ct);

    /// <summary>Saves a change to the case; a failure shows in the Not saved bar (details in the log). True once saved.</summary>
    public async ValueTask<bool> SaveAsync(Func<Case, Case> update, CancellationToken ct)
    {
        try
        {
            await _store.UpdateAsync(_caseId, update, ct);
            await Error.UpdateAsync(_ => "", ct);
            return true;
        }
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            if (ex is not CaseRuleException)
            {
                _logger.LogWarning(ex, "Saving case {CaseId} failed.", _caseId);
            }
            await Error.UpdateAsync(_ => Loc.F("Error_NotSaved", "Not saved: {0}", StoreErrors.Describe(ex)), ct);
            return false;
        }
    }

    /// <summary>Closes the drawer (Close, Escape and a tap on the scrim do the same).</summary>
    public async ValueTask CloseAsync(CancellationToken ct) => await _navigator.NavigateBackAsync(_owner, cancellation: ct);

    /// <summary>Closes the drawer and hands <paramref name="next"/> to the record, which opens it.</summary>
    public async ValueTask HandBackAsync(string next, CancellationToken ct) =>
        await _navigator.NavigateBackWithResultAsync(_owner, data: new PaneChoice(next), cancellation: ct);
}
