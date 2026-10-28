using System.Diagnostics.CodeAnalysis;
using Uno.Resizetizer;

namespace Vigil;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                    // Uno Platform namespace filter groups
                    // Uncomment individual methods to see more detailed logging
                    //// Generic Xaml events
                    //logBuilder.XamlLogLevel(LogLevel.Debug);
                    //// Layout specific messages
                    //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                    //// Storage messages
                    //logBuilder.StorageLogLevel(LogLevel.Debug);
                    //// Binding related messages
                    //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                    //// Binder memory references tracking
                    //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                    //// DevServer and HotReload related
                    //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                    //// Debug JS interop
                    //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                // Enable localization (see appsettings.json for supported languages)
                .UseLocalization()
                .UseThemeSwitching()
                .ConfigureServices((context, services) =>
                {
                    var fault = FaultInjection.FromEnvironment();
                    services.AddSingleton<IFaultInjection>(fault);
                    services.AddSingleton<IClock>(_ => PreviewClock() ?? new SystemClock());
                    services.AddSingleton<DoseFeedback>();
                    services.AddSingleton<IPreferences>(_ => new Preferences(
                        Path.Combine(DataRoot(), "preferences.json")));
                    services.AddSingleton<IFormulary, EmbeddedFormulary>();
                    services.AddSingleton<IRecordExporter, RecordExporter>();
                    services.AddSingleton<ICaseStore>(_ => new JsonCaseStore(
                        Path.Combine(DataRoot(), "cases"), fault));
                })
                .UseNavigation(
                    ReactiveViewModelMappings.ViewModelMappings,
                    RegisterRoutes,
                    // WASM: keep routes, dialog text and record ids out of the address bar and browser history (D5).
                    configure: config => config with { AddressBarUpdateEnabled = false })
            );
        MainWindow = builder.Window;
        UiThread.Queue = MainWindow.DispatcherQueue;
#if DEBUG
        StartMemoryLog();
#endif

#if DEBUG
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
                Loc.Localizer = host.Services.GetRequiredService<IStringLocalizer>();
#if DEBUG
                await SeedForVerificationAsync(host.Services);
#endif
                return host;
            },
            initialRoute: "Main"
        );
#if DESKTOP
#if DEBUG
        // Windows can restore the window maximized, and Resize does nothing on a maximized window: restore it first.
        if (Environment.GetCommandLineArgs().Contains("--clover-preview") && MainWindow.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Maximized } presenter)
            presenter.Restore();
        if (Environment.GetCommandLineArgs().Contains("--clover-preview"))
            MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 1000 });
        var previewSize = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--clover-size="));
        if (previewSize is not null)
        {
            var size = previewSize.Split('=')[1].Split('x');
            if (size.Length == 2 && int.TryParse(size[0], out var width) && int.TryParse(size[1], out var height))
                MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = Math.Clamp(width, 320, 1920), Height = Math.Clamp(height, 480, 1200) });
        }
#endif
#endif
    }

#if DEBUG
    /// <summary>
    /// Diagnosis hook for the break-it memory loop: <c>--vigil-memlog</c> prints the managed heap after a forced
    /// full collection every 10 s, which separates a real leak from a collector that has not run yet.
    /// </summary>
    private void StartMemoryLog()
    {
        if (!Environment.GetCommandLineArgs().Contains("--vigil-memlog"))
        {
            return;
        }
        var timer = MainWindow!.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(10);
        timer.Tick += (_, _) =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine(
            $"MEMLOG {DateTime.Now:HH:mm:ss} managed={GC.GetTotalMemory(forceFullCollection: true) / 1_048_576.0:0.0}MB private={Environment.WorkingSet / 1_048_576.0:0.0}MB {Diagnostics.LiveCounter.Report()}");
        };
        timer.Start();
    }

    /// <summary>Verification hook: <c>--vigil-seed=sample</c> loads the sample day, <c>--vigil-seed=empty</c> clears the store, <c>--vigil-seed=next</c> loads it without the case in surgery.</summary>
    private static async Task SeedForVerificationAsync(IServiceProvider services)
    {
        var seed = Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith("--vigil-seed=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
        if (Environment.GetCommandLineArgs().Contains("--clover-preview"))
        {
            // The design preview reproduces the prototype: sample day, Dr. Okafor (preview data folder only).
            seed ??= "sample";
            var prefs = services.GetRequiredService<IPreferences>();
            if (string.IsNullOrWhiteSpace(prefs.ClinicianName))
            {
                await prefs.SetClinicianNameAsync("Okafor", CancellationToken.None);
            }
        }
        if (seed is null)
        {
            return;
        }
        var store = services.GetRequiredService<ICaseStore>();
        var clock = services.GetRequiredService<IClock>();
        await store.ClearAsync(CancellationToken.None);
        if (seed is "sample" or "next")
        {
            // "next": the sample day without the case in surgery, for the hero's "Next surgery" state.
            foreach (var c in SampleDay.Create(clock.Now).Where(c => seed == "sample" || c.Status != CaseStatus.Anesthetized))
            {
                await store.SaveAsync(c, CancellationToken.None);
            }
        }
    }
#endif

    /// <summary>Verification hook: <c>--vigil-now=12:42:26</c> (or <c>--clover-preview</c>) pins the wall time the app starts at.</summary>
    private static IClock? PreviewClock()
    {
#if DEBUG
        var args = Environment.GetCommandLineArgs();
        var at = args.FirstOrDefault(a => a.StartsWith("--vigil-now=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
            ?? (args.Contains("--clover-preview") ? "12:42:26" : null);
        if (at is not null && TimeSpan.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            return ShiftedClock.At(time);
        }
#endif
        return null;
    }

    private static string DataRoot()
    {
#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--clover-preview"))
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "clover-design-preview");
#endif
        return ApplicationData.Current.LocalFolder.Path;
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<ShellPage, ShellModel>(),
            new ViewMap<BoardPage, BoardModel>(),
            new ViewMap<PatientsPage, PatientsModel>(),
            new ViewMap<AgendaPage, AgendaModel>(),
            new ViewMap<NewCasePage, NewCaseModel>(ResultData: typeof(CaseRef)),
            new DataViewMap<Presentation.MenuFlyout, MenuModel, MenuContext>(ResultData: typeof(MenuChoice)),
            new DataViewMap<RecordPage, RecordModel, CaseRef>(),
            new DataViewMap<ParentPreviewPage, ParentPreviewModel, CaseRef>(),
            new DataViewMap<DosePage, DoseModel, CaseRef>(),
            new DataViewMap<SignOffPage, SignOffModel, CaseRef>(),
            new ViewMap<SettingsPage, SettingsModel>()
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
                    new RouteMap("Dose", View: views.FindByViewModel<DoseModel>()),
                    new RouteMap("SignOff", View: views.FindByViewModel<SignOffModel>()),
                    new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>()),
                    new RouteMap("NewCase", View: views.FindByViewModel<NewCaseModel>()),
                    new RouteMap("Menu", View: views.FindByViewModel<MenuModel>()),
                ])
        );
    }
}
