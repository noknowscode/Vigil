using System.Diagnostics.CodeAnalysis;

namespace Vigil;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CrashReporter.Attach(this, Path.Combine(DataRoot(), "crash.log"));
        try
        {
            await LaunchAsync(args);
        }
        catch (Exception ex)
        {
            // async void: a launch failure would vanish unrecorded (Uno's dispatcher swallows it on desktop; WinAppSDK ends the process).
            CrashReporter.Report(nameof(OnLaunched), ex);
            throw;
        }
    }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        OnLaunching();
        var builder = this.CreateBuilder(args)
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning);
                }, enableUnoLogging: true)
                // appsettings.json carries the localization cultures (en, fr).
                .UseConfiguration(configure: configBuilder => configBuilder.EmbeddedSource<App>())
                .UseLocalization()
                .UseThemeSwitching()
                .ConfigureServices((context, services) =>
                {
                    IFaultInjection fault = NoFaultInjection.Instance;
                    UseVerificationFault(ref fault);
                    services.AddSingleton(fault);
                    services.AddSingleton<IClock>(_ =>
                    {
                        IClock clock = new SystemClock();
                        UseVerificationClock(ref clock);
                        return clock;
                    });
                    services.AddSingleton<DoseFeedback>();
                    services.AddSingleton<IPreferences>(_ => new Preferences(
                        Path.Combine(DataRoot(), "preferences.json")));
                    services.AddSingleton<IFormulary, EmbeddedFormulary>();
                    services.AddSingleton<IRecordExporter, RecordExporter>();
                    services.AddSingleton<ICaseStore>(sp => new JsonCaseStore(
                        Path.Combine(DataRoot(), "cases"), fault, sp.GetRequiredService<ILogger<JsonCaseStore>>()));
                })
                .UseNavigation(
                    ReactiveViewModelMappings.ViewModelMappings,
                    RegisterRoutes,
                    // WASM: keep routes, dialog text and record ids out of the address bar and browser history (D5).
                    configure: config => config with { AddressBarUpdateEnabled = false })
            );
        MainWindow = builder.Window;
        UiThread.Queue = MainWindow.DispatcherQueue;
        UiThread.Window = MainWindow;
        OnWindowCreated(MainWindow);

#if DEBUG && !NO_UNO_STUDIO
        // UseStudio() carries the App MCP connection on desktop; headless capture runs opt out.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();
        MainWindow.Title = "Clover";

        Host = await MainWindow.InitializeNavigationAsync(
            async () =>
            {
                var host = builder.Build();
                CrashReporter.Logger = host.Services.GetRequiredService<ILogger<App>>();
                Loc.Localizer = host.Services.GetRequiredService<IStringLocalizer>();
#if DEBUG
                await SeedForVerificationAsync(host.Services);
#endif
                return host;
            },
            initialRoute: "Main"
        );
#if DEBUG || PAGE_TIMER
        if (Diagnostics.PageTimer.OutputPath is { } perfOutput)
        {
            _ = Diagnostics.PageTimer.RunAsync(MainWindow, Host.Services, perfOutput);
        }
#endif
        OnLaunchCompleted();
    }

    private static string DataRoot()
    {
#if DEBUG || PAGE_TIMER
        // The page-change timing run keeps its sample day out of the real records.
        if (Diagnostics.PageTimer.OutputPath is not null)
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "vigil-perf");
#endif
        var root = ApplicationData.Current.LocalFolder.Path;
        UseVerificationDataRoot(ref root);
        return root;
    }

    // Debug verification hooks (App.Verification.cs, compiled in Debug only). In Release these partial methods have no
    // body, so the compiler removes the calls and nothing of the harness ships.
    partial void OnLaunching();

    static partial void UseVerificationFault(ref IFaultInjection fault);

    static partial void UseVerificationClock(ref IClock clock);

    static partial void UseVerificationDataRoot(ref string root);

    partial void OnWindowCreated(Window window);

    partial void OnLaunchCompleted();

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<ShellPage, ShellModel>(),
            new ViewMap<BoardPage, BoardModel>(),
            new ViewMap<PatientsPage, PatientsModel>(),
            new ViewMap<AgendaPage, AgendaModel>(),
            new ViewMap<CaseFormPage, NewCaseModel>(ResultData: typeof(CaseRef)),
            new DataViewMap<CaseFormPage, EditCaseModel, CaseRef>(),
            new DataViewMap<Presentation.MenuFlyout, MenuModel, MenuContext>(ResultData: typeof(MenuChoice)),
            new DataViewMap<RecordPage, RecordModel, CaseRef>(),
            new DataViewMap<ParentPreviewPage, ParentPreviewModel, CaseRef>(),
            new DataViewMap<DosePage, DoseModel, CaseRef>(),
            new DataViewMap<SignOffPage, SignOffModel, CaseRef>(),
            new ViewMap<SettingsPage, SettingsModel>(),
            // The record's panes: drawers (Flyout views) over the record page, each with its own model; Dose shares the
            // Redose page's model. Each hands its next step back to RecordModel as a PaneChoice.
            new DataViewMap<PreopDrawer, PreopModel, CaseRef>(ResultData: typeof(PaneChoice)),
            new DataViewMap<DoseDrawer, DoseModel, CaseRef>(ResultData: typeof(PaneChoice)),
            new DataViewMap<MedicationsDrawer, MedicationsModel, CaseRef>(ResultData: typeof(PaneChoice)),
            new DataViewMap<VitalsDrawer, VitalsModel, CaseRef>(ResultData: typeof(PaneChoice)),
            new DataViewMap<RecoveryDrawer, RecoveryModel, CaseRef>(ResultData: typeof(PaneChoice)),
            new DataViewMap<HistoryDrawer, HistoryModel, CaseRef>(ResultData: typeof(PaneChoice)),
            new DataViewMap<LabsDrawer, LabsModel, CaseRef>(ResultData: typeof(PaneChoice))
        );

        // Main = ShellPage (sidebar + content Frame). Sidebar destinations and detail pages all open in that Frame,
        // so the sidebar stays; the sidebar drives it through ShellModel.Go ("./-/<route>").
        routes.Register(
            new RouteMap("Main", View: views.FindByViewModel<ShellModel>(), IsDefault: true,
                Nested:
                [
                    new RouteMap("Today", View: views.FindByViewModel<BoardModel>(), IsDefault: true),
                    new RouteMap("Patients", View: views.FindByViewModel<PatientsModel>()),
                    new RouteMap("Agenda", View: views.FindByViewModel<AgendaModel>()),
                    new RouteMap("Record", View: views.FindByViewModel<RecordModel>()),
                    new RouteMap("ParentPreview", View: views.FindByViewModel<ParentPreviewModel>()),
                    // DoseModel has two views (the Redose page and the record's Dose drawer): their routes find the map by
                    // view. A route must take the registry's map, which carries the MVUX bindable model, not a new one.
                    new RouteMap("Redose", View: views.FindByView<DosePage>()),
                    new RouteMap("SignOff", View: views.FindByViewModel<SignOffModel>()),
                    new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>()),
                    new RouteMap("NewCase", View: views.FindByViewModel<NewCaseModel>()),
                    new RouteMap("EditCase", View: views.FindByViewModel<EditCaseModel>()),
                    new RouteMap("Menu", View: views.FindByViewModel<MenuModel>()),
                    new RouteMap(RecordPane.Preop, View: views.FindByViewModel<PreopModel>()),
                    new RouteMap(RecordPane.Dose, View: views.FindByView<DoseDrawer>()),
                    new RouteMap(RecordPane.Drugs, View: views.FindByViewModel<MedicationsModel>()),
                    new RouteMap(RecordPane.Monitor, View: views.FindByViewModel<VitalsModel>()),
                    new RouteMap(RecordPane.Recovery, View: views.FindByViewModel<RecoveryModel>()),
                    new RouteMap(RecordPane.History, View: views.FindByViewModel<HistoryModel>()),
                    new RouteMap(RecordPane.Labs, View: views.FindByViewModel<LabsModel>()),
                ])
        );
    }
}
