using Vigil.Domain;
using Vigil.Services;

namespace Vigil.Presentation;

/// <summary>
/// The board heading: a time-of-day greeting and a one-line recap of the day. <see cref="Lead"/> is the most urgent
/// fact (shown bold), <see cref="Rest"/> the remaining counts.
/// </summary>
public partial record BoardGreeting(string Title, string Lead, string Rest)
{
    public static BoardGreeting From(IEnumerable<Case> cases, DateTimeOffset now, TimeSpan interval, string clinician)
    {
        var name = Surname(clinician);
        var title = (now.Hour, name.Length > 0) switch
        {
            ( < 12, true) => Loc.F("Greeting_MorningNamed", "Good morning, Dr. {0}", name),
            ( < 18, true) => Loc.F("Greeting_AfternoonNamed", "Good afternoon, Dr. {0}", name),
            (_, true) => Loc.F("Greeting_EveningNamed", "Good evening, Dr. {0}", name),
            ( < 12, false) => Loc.T("Greeting_Morning", "Good morning"),
            ( < 18, false) => Loc.T("Greeting_Afternoon", "Good afternoon"),
            _ => Loc.T("Greeting_Evening", "Good evening"),
        };

        var list = cases.ToList();
        var surgery = list.Where(c => c.Status == CaseStatus.Anesthetized).ToList();
        var preparing = list.Count(c => c.Status == CaseStatus.Scheduled);
        var recovering = list.Count(c => c.Status == CaseStatus.Recovery);
        var signedToday = list.Count(c => c.Status == CaseStatus.Signed && c.SignedAt?.ToOffset(now.Offset).Date == now.Date);

        var parts = new List<string>();
        string lead;
        if (surgery.Count > 0)
        {
            lead = surgery.Count == 1
                ? Loc.T("Recap_OneInSurgery", "1 patient in surgery.")
                : Loc.F("Recap_InSurgery", "{0} patients in surgery.", surgery.Count);
            parts.AddRange(surgery.Where(c => Schedule.State(c, interval, now) == DueState.Overdue)
                .Select(c => Loc.F("Recap_Overdue", "{0}'s reading is overdue", c.Patient.Name)));
        }
        else if (preparing > 0)
        {
            lead = Loc.F("Recap_Preparing", "{0} waiting for preparation.", preparing);
            preparing = 0;
        }
        else if (recovering > 0)
        {
            lead = Loc.F("Recap_Recovering", "{0} in recovery.", recovering);
            recovering = 0;
        }
        else
        {
            lead = signedToday > 0
                ? Loc.T("Recap_AllSigned", "Every record from today is signed.")
                : Loc.T("Recap_Quiet", "No patients on the board yet.");
            signedToday = 0;
        }
        if (preparing > 0) parts.Add(Loc.F("Recap_PreparingCount", "{0} in preparation", preparing));
        if (recovering > 0) parts.Add(Loc.F("Recap_RecoveringCount", "{0} recovering", recovering));
        if (signedToday > 0) parts.Add(Loc.F("Recap_SignedCount", "{0} signed today", signedToday));

        return new BoardGreeting(title, lead, parts.Count > 0 ? " " + string.Join(" · ", parts) + "." : "");
    }

    /// <summary>"Dr. Okafor", "dr okafor" and "Okafor" all greet as "Dr. Okafor".</summary>
    private static string Surname(string clinician)
    {
        var name = clinician.Trim();
        foreach (var prefix in new[] { "Dr.", "Dr", "Dre", "Dre." })
        {
            if (name.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                return name[(prefix.Length + 1)..].Trim();
            }
        }
        return name;
    }
}
