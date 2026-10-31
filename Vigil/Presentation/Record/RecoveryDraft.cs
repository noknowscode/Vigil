namespace Vigil.Presentation;

/// <summary>
/// Editable text part of the recovery log. Times are stamped by commands, and pain scores are recorded one at a time
/// (<see cref="RecordModel.ScorePain"/>), so the draft never writes the score.
/// </summary>
public partial record RecoveryDraft(string TempC, string Notes)
{
    public static RecoveryDraft From(RecoveryLog r) =>
        new(r.TempC?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "", r.Notes);

    /// <summary>Applies the draft onto the stored log (temperature and notes).</summary>
    public RecoveryLog ApplyTo(RecoveryLog r) => r with
    {
        TempC = VitalsDraft.ParseDec(TempC),
        Notes = Notes.Trim(),
    };
}
