namespace Vigil.Presentation;

/// <summary>What a failed store call tells the user. The exception's own text (file paths, record ids) is for the log.</summary>
public static class StoreErrors
{
    public static string Describe(Exception ex) => ex switch
    {
        CaseRuleException rule => rule.Message,
        SignedRecordException => Loc.T("Error_Signed", "This record is signed and can no longer change."),
        KeyNotFoundException => Loc.T("Record_Gone", "This record no longer exists."),
        _ => Loc.T("Error_NotWritten", "It could not be written to this device. Try again."),
    };
}
