namespace Vigil.Presentation;

/// <summary>Actual case milestones; never a guessed appointment time.</summary>
public partial record AgendaEntry(Guid Id, string Name, string Detail, string Stage, string Time, DateTimeOffset At, string? Photo, string Initial, bool IsComplete) : CaseRef(Id)
{
    public bool HasNoPhoto => string.IsNullOrEmpty(Photo);
    public string Marker => IsComplete ? "✓" : "•";
    public string AccessibleName => $"{Name}, {Stage}, {Time}. {Detail}";
}

public partial record BoardAgenda(IImmutableList<AgendaEntry> Events, IImmutableList<AgendaEntry> Preparing)
{
    public bool HasEvents => Events.Count > 0;
    public bool NoEvents => !HasEvents;
    public bool HasPreparing => Preparing.Count > 0;
    public bool NoPreparing => !HasPreparing;
    public string Summary => Loc.F("Agenda_Summary", "{0} milestones · {1} preparing", Events.Count, Preparing.Count);

    public static BoardAgenda From(IEnumerable<Case> cases, DateTimeOffset now)
    {
        var relevant = cases.Where(c => c.CreatedAt.ToOffset(now.Offset).Date == now.Date ||
            c.SignedAt?.ToOffset(now.Offset).Date == now.Date || c.Status != CaseStatus.Signed).ToArray();
        var events = relevant.Where(c => c.Status != CaseStatus.Scheduled).Select(c =>
        {
            var at = c.Status switch
            {
                CaseStatus.Signed => c.SignedAt ?? c.EndedAt ?? c.CreatedAt,
                CaseStatus.Recovery => c.EndedAt ?? c.CreatedAt,
                _ => c.InducedAt ?? c.CreatedAt,
            };
            var stage = c.Status switch
            {
                CaseStatus.Signed => Loc.T("Agenda_Complete", "Record completed"),
                CaseStatus.Recovery => Loc.T("Agenda_Recovery", "Recovery started"),
                _ => Loc.T("Agenda_Induced", "Anaesthesia started"),
            };
            var local = at.ToOffset(now.Offset);
            return Entry(c, stage, local.Date == now.Date ? local.ToString("HH:mm") : local.ToString("dd MMM HH:mm"), at);
        }).OrderBy(e => e.At).ThenBy(e => e.Name).ToImmutableList();
        var preparing = relevant.Where(c => c.Status == CaseStatus.Scheduled).OrderBy(c => c.CreatedAt)
            .Select(c => Entry(c, c.Preop.IsComplete ? Loc.T("Agenda_Ready", "Pre-op complete") : Loc.T("Agenda_Preparing", "Preparing"), "—", c.CreatedAt)).ToImmutableList();
        return new(events, preparing);
    }

    private static AgendaEntry Entry(Case c, string stage, string time, DateTimeOffset at) =>
        new(c.Id, c.Patient.Name, c.Procedure, stage, time, at, PatientMedia.Photo(c.Patient),
            string.IsNullOrWhiteSpace(c.Patient.Name) ? "?" : c.Patient.Name[..1], c.IsSigned);
}

/// <summary>The earliest observation deadline, including overdue cases before later deadlines.</summary>
public partial record AgendaReminder(Guid Id, string Name, string DueAt, string Label, string Detail, bool IsOverdue) : CaseRef(Id)
{
    public string? Photo { get; init; }
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1];
    public bool HasNoPhoto => string.IsNullOrEmpty(Photo);
    public string AccessibleName => $"{Name}, {Label}. {Detail}";
    public bool IsWaiting => !IsOverdue;
    public static AgendaReminder? From(IEnumerable<Case> cases, DateTimeOffset now, TimeSpan interval)
    {
        var next = cases.Where(c => c.Status == CaseStatus.Anesthetized)
            .Select(c => (Case: c, Due: Schedule.NextDue(c, interval))).Where(x => x.Due.HasValue)
            .OrderBy(x => x.Due).FirstOrDefault();
        if (next.Case is null || next.Due is not { } due) return null;
        var overdue = due < now;
        return new(next.Case.Id, next.Case.Patient.Name, due.ToOffset(now.Offset).ToString("HH:mm"),
            overdue ? Loc.T("Agenda_Overdue", "Observation overdue") : Loc.T("Agenda_Next", "Next observation"),
            overdue ? Loc.F("Agenda_WasDue", "Due at {0:HH:mm} · open record", due.ToOffset(now.Offset)) : Loc.T("Agenda_Open", "Open record to add observations"), overdue) { Photo = PatientMedia.Photo(next.Case.Patient) };
    }
}
