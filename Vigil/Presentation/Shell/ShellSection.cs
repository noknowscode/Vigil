using System.Collections.Immutable;

namespace Vigil.Presentation;

/// <summary>
/// Which sidebar item a route belongs to, and the counts the sidebar shows. Pure functions, so the shell and the
/// narrow-width menu flyout read the same answers.
/// </summary>
public static class ShellSection
{
    public const string Today = "Today";
    public const string Patients = "Patients";
    public const string Agenda = "Agenda";
    public const string Anesthesia = "Anesthesia";
    public const string Settings = "Settings";

    /// <summary>Visual state names for NavItemButtonStyle's NavStates group.</summary>
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    /// <summary>
    /// Maps a navigation path ("/Today", "/Today/Record", "/Settings") to the sidebar item it highlights: the last
    /// segment that names a frame page wins, so a record opened from Today highlights Patients (as in the Patient care
    /// render). Returns null for paths with no frame page (dialogs, the shell itself), so the highlight stays put.
    /// </summary>
    public static string? FromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        string? section = null;
        foreach (var raw in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = raw.TrimStart('!', '-', '.');
            var q = segment.IndexOf('?');
            if (q >= 0)
            {
                segment = segment[..q];
            }
            section = segment switch
            {
                "Board" or Today => Today,
                Patients or "Record" or "Dose" or "SignOff" or "ParentPreview" => Patients,
                Agenda => Agenda,
                Settings => Settings,
                _ => section,
            };
        }
        return section;
    }

    public static string StateFor(string current, string item) => current == item ? Active : Inactive;

    /// <summary>Cases still in the clinic: everything not yet signed out.</summary>
    public static int InClinic(IEnumerable<Case> cases) => cases.Count(c => c.Status != CaseStatus.Signed);

    /// <summary>Everyone on today's board (the Today "All" tab): the sidebar Patients count.</summary>
    public static int OnBoardToday(IEnumerable<Case> cases, DateTimeOffset now) => cases.Count(c => Vigil.Presentation.Today.Matches(c, CardFilter.All, now));

    /// <summary>Cases under anesthesia right now.</summary>
    public static int Live(IEnumerable<Case> cases) => cases.Count(c => c.Status == CaseStatus.Anesthetized);

    /// <summary>Count label beside a nav item: blank for zero, so an empty clinic shows no "0".</summary>
    public static string CountText(int count) => count > 0 ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) : "";

    /// <summary>"Dr. Okafor" from the Settings surname, "Clinician" when none is set.</summary>
    public static string ClinicianLabel(string surname) => string.IsNullOrWhiteSpace(surname)
        ? Loc.T("Shell_Clinician", "Clinician")
        : Loc.F("Shell_Doctor", "Dr. {0}", surname.Trim());

    /// <summary>
    /// Avatar initials: first name and surname initials ("Amara", "Okafor" gives "AO"). Without a first name, the first
    /// letters of up to two words of the surname ("Okafor" gives "O").
    /// </summary>
    public static string Initials(string firstName, string surname)
    {
        static string First(string s) => char.ToUpper(s.Trim()[0], System.Globalization.CultureInfo.CurrentCulture).ToString();
        var words = surname.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (string.IsNullOrWhiteSpace(firstName), words.Length) switch
        {
            (false, > 0) => First(firstName) + First(words[^1]),
            (false, 0) => First(firstName),
            (true, > 0) => string.Concat(words.Take(2).Select(First)),
            _ => Loc.T("Shell_ClinicianInitial", "C"),
        };
    }
}

/// <summary>A sidebar choice made in the narrow-width menu flyout, handed back to the shell to carry out.</summary>
public sealed record MenuChoice(string Route)
{
    /// <summary>The "New patient" button: the shell opens the New case sheet.</summary>
    public const string NewPatient = "NewCase";
}
