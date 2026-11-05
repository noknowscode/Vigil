namespace Vigil.Presentation;

/// <summary>A recorded anesthesia or recovery phase on the Agenda ruler, clipped to today.</summary>
public sealed partial record CareSpan(Guid CaseId, string Name, CarePhaseKind Phase, DateTimeOffset Start, DateTimeOffset End, bool Ongoing)
{
    public DateTimeOffset RecordedStart { get; init; } = Start;
    public bool Continued { get; init; }
    public string Key => $"{CaseId}:{Phase}";
    public string PhaseLabel => Phase == CarePhaseKind.Anesthesia ? Loc.T("Ruler_Anaesthesia", "Anaesthesia") : Loc.T("Ruler_Recovery", "Recovery");
    public string Times => Continued
        ? $"{RecordedStart:dd MMM HH:mm} – {(Ongoing ? Loc.T("Ruler_Now", "Now") : End.ToString("HH:mm"))}"
        : Ongoing ? Loc.F("Ruler_Since", "Since {0:HH:mm}", RecordedStart) : $"{RecordedStart:HH:mm} – {End:HH:mm}";
    public string AccessibleName => $"{Name}, {PhaseLabel}, {Times}";
}
public sealed record CareSlot(CareSpan Span, int Lane, double Top, double Height);

/// <summary>Recorded intervals on a continuous time axis. Empty space is not staff availability.</summary>
public sealed record CareDay(DateTimeOffset Start, DateTimeOffset End, DateTimeOffset Now, IImmutableList<CareSpan> Spans)
{
    public const double PixelsPerHour = 96;
    public bool IsEmpty => Spans.Count == 0;
    public string Range => $"{Start:HH:mm} – {End:HH:mm}";
    public double Height => (End - Start).TotalHours * PixelsPerHour;
    public double NowTop => Math.Clamp((Now - Start).TotalHours * PixelsPerHour, 0, Height);

    public IImmutableList<CareSlot> Layout()
    {
        var ends = new List<double>();
        var slots = ImmutableList.CreateBuilder<CareSlot>();
        foreach (var span in Spans.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            var top = (span.Start - Start).TotalHours * PixelsPerHour;
            // Short intervals retain a usable target; the printed timestamps remain exact.
            var height = Math.Max(48, (span.End - span.Start).TotalHours * PixelsPerHour - 4);
            var lane = ends.FindIndex(end => end <= top + .01);
            if (lane < 0) { lane = ends.Count; ends.Add(0); }
            ends[lane] = top + height + 4;
            slots.Add(new(span, lane, top, height));
        }
        return slots.ToImmutable();
    }

    /// <summary>
    /// The recorded phases only: the anesthesia and recovery spans of <see cref="CarePhases"/> (the Today gantt adds
    /// waiting, prep and planned slots), clipped to midnight and now.
    /// </summary>
    public static CareDay From(IEnumerable<Case> cases, DateTimeOffset now)
    {
        var midnight = new DateTimeOffset(now.Date, now.Offset);
        var spans = ImmutableList.CreateBuilder<CareSpan>();
        foreach (var c in cases)
        {
            foreach (var phase in CarePhases.Of(c, now).Where(s => s.Phase is CarePhaseKind.Anesthesia or CarePhaseKind.Recovery))
            {
                var to = phase.End > now ? now : phase.End;
                if (to < midnight)
                {
                    continue;
                }
                var from = phase.Start < midnight ? midnight : phase.Start;
                spans.Add(new(c.Id, c.Patient.Name, phase.Phase, from, to, phase.Ongoing) { RecordedStart = phase.Start, Continued = phase.Start < midnight });
            }
        }
        var earliest = spans.Count > 0 ? spans.Min(x => x.Start) : now.AddHours(-1);
        if (earliest < midnight) earliest = midnight;
        var start = new DateTimeOffset(earliest.Year, earliest.Month, earliest.Day, earliest.Hour, 0, 0, now.Offset);
        var end = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset).AddHours(1);
        return new(start, end, now, spans.OrderBy(x => x.Start).ToImmutableList());
    }
}
