namespace Vigil.Presentation;

/// <summary>
/// Patients (sidebar): every case on this device, newest first, as Today's patient cards (<see cref="PatientCard"/>,
/// drawn by PatientCardView). Each card's details toggle independently, as on Today.
/// </summary>
public partial record PatientsModel(ICaseStore Store, IClock Clock, IPreferences Preferences)
    : PatientCardsModel(Store, Preferences)
{
    private IFeed<DateTimeOffset> Minute { get; } = Feed.AsyncEnumerable(Clock.Ticks).Select(t => new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Offset));

    public IListFeed<PatientCard> All => Feed
        .Combine(Cases, Expanded, Minute, ReduceMotion)
        .Select(x => (IImmutableList<PatientCard>)x.Item1
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => PatientCard.From(c, x.Item3, x.Item2.Contains(c.Id), x.Item4))
            .ToImmutableList())
        .AsListFeed();

    public IFeed<string> Summary => Cases.Select(cases => Loc.F("Patients_Summary", "{0} in clinic · {1} on this device", ShellSection.InClinic(cases), cases.Count));
}
