namespace Vigil.Presentation;

/// <summary>
/// The sidebar as a drawer below 760 px (route "!Menu"). It exposes the same members the Sidebar control binds on
/// <see cref="ShellModel"/>; every choice closes the drawer and hands the route back to the shell, which owns the
/// content frame (<see cref="ShellModel.OpenMenu"/>).
/// </summary>
public partial record MenuModel(MenuContext Context, INavigator Navigator, ICaseStore Store, IPreferences Preferences)
{
    private IFeed<IImmutableList<Case>> Cases { get; } = Feed.Async(async ct => (IImmutableList<Case>)await Store.GetAllAsync(ct), Store.Changed);

    private IFeed<string> Surname { get; } = Feed.Async(async ct => { await Task.Yield(); return Preferences.ClinicianName; }, Preferences.Changed);

    // Delivered as a feed: a value already present when the Sidebar binds reaches the nav items before their templates
    // exist, so utu:VisualStateManagerExtensions has no Active state to go to yet.
    private IFeed<string> Current { get; } = Feed.Async(async ct => { await Task.Yield(); return Context.Section; });

    public IFeed<string> TodayState => Current.Select(s => ShellSection.StateFor(s, ShellSection.Today));
    public IFeed<string> PatientsState => Current.Select(s => ShellSection.StateFor(s, ShellSection.Patients));
    public IFeed<string> AgendaState => Current.Select(s => ShellSection.StateFor(s, ShellSection.Agenda));
    public IFeed<string> AnesthesiaState => Current.Select(s => ShellSection.StateFor(s, ShellSection.Anesthesia));
    public IFeed<string> SettingsState => Current.Select(s => ShellSection.StateFor(s, ShellSection.Settings));

    public IFeed<string> PatientsCount => Cases.Select(c => ShellSection.CountText(ShellSection.InClinic(c)));
    public IFeed<string> AnesthesiaCount => Cases.Select(c => ShellSection.CountText(ShellSection.Live(c)));
    public IFeed<bool> HasLive => Cases.Select(c => ShellSection.Live(c) > 0);

    public IFeed<string> ClinicianName => Surname.Select(ShellSection.ClinicianLabel);
    public IFeed<string> ClinicianInitials => Surname.Select(ShellSection.Initials);

    /// <summary>The drawer always shows the full sidebar.</summary>
    public bool IsExpanded => true;
    public bool IsCollapsed => false;

    public async ValueTask Go(string route, CancellationToken ct) =>
        await Navigator.NavigateBackWithResultAsync(this, data: new MenuChoice(route), cancellation: ct);

    public async ValueTask OpenAnesthesia(CancellationToken ct) => await Go(ShellSection.Anesthesia, ct);

    public async ValueTask NewPatient(CancellationToken ct) => await Go(MenuChoice.NewPatient, ct);

    /// <summary>The collapse button closes the drawer.</summary>
    public async ValueTask ToggleSidebar(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
