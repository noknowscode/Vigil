using System.Collections.Immutable;
using Vigil.Domain;
using Vigil.Services;

namespace Vigil.Presentation;

/// <summary>
/// Today's agenda: the care ruler, patients in preparation and the next observation reminder. One page, two hosts:
/// docked in the board's AgendaDock region from 1120 px, and opened as a "!Agenda" flyout below that.
/// </summary>
public partial record AgendaModel(ICaseStore Store, IClock Clock, IPreferences Preferences)
{
    private IFeed<IImmutableList<Case>> Cases { get; } = Feed.Async(async ct => (IImmutableList<Case>)await Store.GetAllAsync(ct), Store.Changed);

    private IFeed<DateTimeOffset> Now { get; } = Feed.AsyncEnumerable(Clock.Ticks);

    public IFeed<BoardAgenda> Agenda => Feed.Combine(Cases, Now.Select(n => n.Date)).Select(x => BoardAgenda.From(x.Item1, Clock.Now));
    public IFeed<AgendaReminder> AgendaNext => Feed.Combine(Cases, Now).Select(x => AgendaReminder.From(x.Item1, x.Item2, Preferences.ReadingInterval)!);
    public IFeed<CareDay> CareRhythm => Feed.Combine(Cases, Now.Select(n => new DateTimeOffset(n.Year, n.Month, n.Day, n.Hour, n.Minute, 0, n.Offset))).Select(x => CareDay.From(x.Item1, Clock.Now));
    public IFeed<string> AgendaClock => Now.Select(n => n.ToString("HH:mm"));
    public IFeed<string> AgendaDate => Now.Select(n => n.ToString("ddd, d MMM"));

    public void Retry() => Store.Reload();
}
