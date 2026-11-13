namespace Vigil.Presentation;

/// <summary>What a failed store call tells the user. The exception's own text (file paths, record ids) is for the log.</summary>
public static class StoreErrors
{
    /// <summary>
    /// The failures a store call reports to the user, in a Not saved bar or a notice: the device's files (IO and access
    /// errors) and the record's rules (a signed or deleted record, a rule of its stage). Anything else is a bug and
    /// propagates to the crash reporter.
    /// </summary>
    public static bool IsReported(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException;

    public static string Describe(Exception ex) => ex switch
    {
        CaseRuleException rule => rule.Message,
        SignedRecordException => Loc.T("Error_Signed", "This record is signed and can no longer change."),
        KeyNotFoundException => Loc.T("Record_Gone", "This record no longer exists."),
        _ => Loc.T("Error_NotWritten", "It could not be written to this device. Try again."),
    };
}
