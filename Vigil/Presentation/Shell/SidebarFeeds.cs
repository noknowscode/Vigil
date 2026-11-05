namespace Vigil.Presentation;

/// <summary>
/// What the Sidebar control shows besides its commands: each item's state, the counts and the clinician. The shell and
/// the narrow-width menu drawer each expose these under the same names, so the Sidebar binds either model.
/// </summary>
public sealed class SidebarFeeds
{
    /// <param name="section">The highlighted item (<see cref="ShellSection"/> names).</param>
    public SidebarFeeds(ICaseStore store, IPreferences preferences, IClock clock, IFeed<string> section)
    {
        Cases = Feed.Async(async ct => (IImmutableList<Case>)await store.GetAllAsync(ct), store.Changed);
        TodayState = section.Select(s => ShellSection.StateFor(s, ShellSection.Today));
        PatientsState = section.Select(s => ShellSection.StateFor(s, ShellSection.Patients));
        AgendaState = section.Select(s => ShellSection.StateFor(s, ShellSection.Agenda));
        AnesthesiaState = section.Select(s => ShellSection.StateFor(s, ShellSection.Anesthesia));
        SettingsState = section.Select(s => ShellSection.StateFor(s, ShellSection.Settings));
        PatientsCount = Cases.Select(c => ShellSection.CountText(ShellSection.OnBoardToday(c, clock.Now)));
        AnesthesiaCount = Cases.Select(c => ShellSection.CountText(ShellSection.Live(c)));
        HasLive = Cases.Select(c => ShellSection.Live(c) > 0);
        ClinicianName = Feed.Async(async ct => { await Task.Yield(); return ShellSection.ClinicianLabel(preferences.ClinicianName); }, preferences.Changed);
        ClinicianInitials = Feed.Async(async ct => { await Task.Yield(); return ShellSection.Initials(preferences.ClinicianFirstName, preferences.ClinicianName); }, preferences.Changed);
    }

    /// <summary>Every case, reloaded on each store change.</summary>
    public IFeed<IImmutableList<Case>> Cases { get; }

    public IFeed<string> TodayState { get; }
    public IFeed<string> PatientsState { get; }
    public IFeed<string> AgendaState { get; }
    public IFeed<string> AnesthesiaState { get; }
    public IFeed<string> SettingsState { get; }

    public IFeed<string> PatientsCount { get; }
    public IFeed<string> AnesthesiaCount { get; }
    public IFeed<bool> HasLive { get; }

    public IFeed<string> ClinicianName { get; }
    public IFeed<string> ClinicianInitials { get; }
}
