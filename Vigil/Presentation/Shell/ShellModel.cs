namespace Vigil.Presentation;

/// <summary>
/// The app shell (route Main): sidebar state, counts and the commands behind each sidebar item. Every sidebar item
/// navigates the content frame through <see cref="Go"/> with <c>./-/</c> (nested + clear back stack): the
/// FrameNavigator drops a request for a route already in its stack, so plain region navigation strands a second
/// visit (uno-runtime-gotchas, "FrameNavigator ignores a route already in its FullRoute").
/// </summary>
public partial record ShellModel(INavigator Navigator, IRouteNotifier Routes, ICaseStore Store, IPreferences Preferences, IClock Clock)
{
    private const string ClearToFrame = Qualifiers.Nested + Qualifiers.ClearBackStack;

    /// <summary>
    /// The highlighted sidebar item. A pick sets it at once (<see cref="Go"/>); every route change in the content frame
    /// then sets it from the page shown (raised after the page loads), so detail pages move it too (Record: Patients).
    /// The state's initializer runs once per model, which makes it the one place to subscribe; the shell lives as long
    /// as the window, so the handler is never removed.
    /// </summary>
    public IState<string> Section => State.Value(this, () =>
    {
        Routes.RouteChanged += OnRouteChanged;
        return ShellSection.Today;
    });

    private SidebarFeeds Sidebar => _sidebar ??= new(Store, Preferences, Clock, Section);
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

    /// <summary>Read after each load of the cases: case files the store set aside as unreadable (the others loaded).</summary>
    public IFeed<string> StoreWarning => Sidebar.Cases.Select(_ => ShellSection.SetAsideText(Store.SetAside.Count));
    public IFeed<bool> HasStoreWarning => StoreWarning.Select(s => s.Length > 0);

    /// <summary>Full sidebar (true) or icon rail (false) from 1080 px; below that the rail is forced by width.</summary>
    public IState<bool> IsExpanded => State.Value(this, () => true);
    public IFeed<bool> IsCollapsed => IsExpanded.Select(x => !x);

    public async ValueTask ToggleSidebar(CancellationToken ct) => await IsExpanded.UpdateAsync(x => !x, ct);

    /// <summary>Opens a sidebar destination in the content frame and clears the frame's back stack.</summary>
    public async ValueTask Go(string route, CancellationToken ct)
    {
        await Section.UpdateAsync(_ => route, ct);
        await Navigator.NavigateRouteAsync(this, route, qualifier: ClearToFrame, cancellation: ct);
    }

    /// <summary>Anesthesia: the record of the case under anesthesia (the item is disabled when there is none).</summary>
    public async ValueTask OpenAnesthesia(CancellationToken ct)
    {
        if (BoardDestination.Surgery(await Store.GetAllAsync(ct)) is { } live)
        {
            await OpenRecordAsync(new CaseRef(live.Id), ct);
        }
    }

    /// <summary>
    /// Opens a record from the shell as Today/Record with a cleared frame: Back returns to Today, and the frame always
    /// creates a new RecordPage. Navigating Record to Record (Anesthesia or New patient while a record is open) reused
    /// the open page with the new model, and its FeedView kept showing the previous patient under the new name.
    /// </summary>
    private async ValueTask OpenRecordAsync(CaseRef record, CancellationToken ct)
    {
        await Go(ShellSection.Today, ct);
        await Navigator.NavigateRouteAsync(this, "Record", qualifier: Qualifiers.Nested, data: record, cancellation: ct);
    }

    /// <summary>Opens the New case sheet; on Create, opens the new record in the content frame.</summary>
    public async ValueTask NewPatient(CancellationToken ct)
    {
        var result = await Navigator.NavigateRouteForResultAsync<CaseRef>(this, "!NewCase", cancellation: ct).AsResult();
        if (result.SomeOrDefault() is CaseRef created)
        {
            await OpenRecordAsync(created, ct);
        }
    }

    /// <summary>On phones the sidebar opens as the "!Menu" drawer, which hands the chosen item back here.</summary>
    public async ValueTask OpenMenu(string section, CancellationToken ct)
    {
        var choice = await Navigator.NavigateRouteForResultAsync<MenuChoice>(this, "!Menu", data: new MenuContext(section), cancellation: ct).AsResult();
        switch (choice.SomeOrDefault()?.Route)
        {
            case null:
                return;
            case MenuChoice.NewPatient:
                await NewPatient(ct);
                break;
            case ShellSection.Anesthesia:
                await OpenAnesthesia(ct);
                break;
            case var route:
                await Go(route, ct);
                break;
        }
    }

    private void OnRouteChanged(object? sender, RouteChangedEventArgs e)
    {
        var route = e.Navigator?.Route;
        if (ShellSection.FromPath($"{route?.Base}/{route?.Path}") is { } section)
        {
            _ = Section.UpdateAsync(_ => section, CancellationToken.None);
        }
    }
}

/// <summary>What the menu drawer needs from the shell: the item to highlight.</summary>
public sealed record MenuContext(string Section);
