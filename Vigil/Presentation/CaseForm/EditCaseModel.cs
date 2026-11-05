namespace Vigil.Presentation;

/// <summary>
/// Edit case details (route "!EditCase", from the Preparation drawer): the case form filled from the saved case. Save
/// corrects the patient, weight, procedure, team and schedule until the record is signed, logging each change
/// (<see cref="Case.WithDetailsFrom"/>). Everything calculated from the case follows: the dose calculator and its
/// reference range, the vitals alarms for the species, Today's timeline. Doses already given keep their volume.
/// </summary>
public partial record EditCaseModel(CaseRef Ref, ICaseStore Store, IClock Clock, INavigator Navigator, ILogger<EditCaseModel> Logger)
{
    public IState<CaseDraft> Draft => State.Async(this, async ct =>
        await Store.GetAsync(Ref.Id, ct) is { } c ? CaseDraft.From(c, Clock.Now) : CaseDraft.Empty);

    private CaseFormState Form => _form ??= new(this, Draft);
    private CaseFormState? _form;

    public string Heading => Loc.T("EditCase_Heading", "Case details");
    public string Intro => Loc.T("EditCase_Intro", "Correct the patient, the procedure or the schedule. Each change is kept in the record's history.");
    public string ActionLabel => Loc.T("EditCase_Action", "Save changes");

    /// <summary>Under the intro once doses were given: what a weight correction changes and what it leaves alone.</summary>
    public IFeed<string> Note => Feed.Async(async ct => await Store.GetAsync(Ref.Id, ct) is { Doses.Count: > 0 }
        ? Loc.T("EditCase_DoseNote", "Doses already given keep the volume drawn up at the time. New doses use the corrected weight.")
        : "");

    public IFeed<bool> HasNote => Note.Select(n => n.Length > 0);

    public IState<bool> Attempted => Form.Attempted;
    public IFeed<string> ErrorText => Form.ErrorText;
    public IState<string> SaveError => Form.SaveError;
    public IFeed<bool> HasErrors => Form.HasErrors;

    public async ValueTask Save(CaseDraft draft, CancellationToken ct)
    {
        if (!await Form.AcceptsAsync(draft, ct))
        {
            return;
        }
        try
        {
            await Store.UpdateAsync(Ref.Id, c => draft.ApplyTo(c, Clock.Now), ct);
        }
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            if (ex is not CaseRuleException)
            {
                Logger.LogWarning(ex, "Correcting case {CaseId} failed.", Ref.Id);
            }
            await SaveError.UpdateAsync(_ => Loc.F("EditCase_NotSaved", "Not saved: {0} Your entries are kept.", StoreErrors.Describe(ex)), ct);
            return;
        }
        await Navigator.NavigateBackAsync(this, cancellation: ct);
    }

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
