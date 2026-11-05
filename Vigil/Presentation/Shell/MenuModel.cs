namespace Vigil.Presentation;

/// <summary>
/// The sidebar as a drawer on phones (route "!Menu"). It exposes the same members the Sidebar control binds on
/// <see cref="ShellModel"/> (both from <see cref="SidebarFeeds"/>); every choice closes the drawer and hands the route
/// back to the shell, which owns the content frame (<see cref="ShellModel.OpenMenu"/>).
/// </summary>
public partial record MenuModel(MenuContext Context, INavigator Navigator, ICaseStore Store, IPreferences Preferences, IClock Clock)
{
    // Delivered as a feed: a value already present when the Sidebar binds reaches the nav items before their templates
    // exist, so utu:VisualStateManagerExtensions has no Active state to go to yet.
    private IFeed<string> Current { get; } = Feed.Async(async ct => { await Task.Yield(); return Context.Section; });

    private SidebarFeeds Sidebar => _sidebar ??= new(Store, Preferences, Clock, Current);
    private SidebarFeeds? _sidebar;

    public IFeed<string> TodayState => Sidebar.TodayState;
    public IFeed<string> PatientsState => Sidebar.PatientsState;
    public IFeed<string> AgendaState => Sidebar.AgendaState;
    public IFeed<string> AnesthesiaState => Sidebar.AnesthesiaState;
    public IFeed<string> SettingsState => Sidebar.SettingsState;
    public IFeed<string> PatientsCount => Sidebar.PatientsCount;
    public IFeed<string> AnesthesiaCount => Sidebar.AnesthesiaCount;
    public IFeed<bool> HasLive => Sidebar.HasLive;
    public IFeed<string> ClinicianName => Sidebar.ClinicianName;
    public IFeed<string> ClinicianInitials => Sidebar.ClinicianInitials;

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
