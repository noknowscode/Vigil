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

    public string TodayState => ShellSection.StateFor(Context.Section, ShellSection.Today);
    public string PatientsState => ShellSection.StateFor(Context.Section, ShellSection.Patients);
    public string AgendaState => ShellSection.StateFor(Context.Section, ShellSection.Agenda);
    public string AnesthesiaState => ShellSection.StateFor(Context.Section, ShellSection.Anesthesia);
    public string SettingsState => ShellSection.StateFor(Context.Section, ShellSection.Settings);

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
