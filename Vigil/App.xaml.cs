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
#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--vigil-crash=launch"))
        {
            throw new InvalidOperationException("Verification crash during launch (--vigil-crash=launch).");
        }
#endif
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
#if DEBUG
                    // Test hook (VIGIL_FAULT / --vigil-fault): Debug builds only; Release never injects a fault.
                    IFaultInjection fault = FaultInjection.FromEnvironment();
#else
                    IFaultInjection fault = NoFaultInjection.Instance;
#endif
                    services.AddSingleton(fault);
                    services.AddSingleton<IClock>(_ => PreviewClock() ?? new SystemClock());
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
#if DEBUG
        StartMemoryLog();
#endif

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
#if __DESKTOP__ || WINDOWS
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
#if DEBUG
        // After any --clover-size resize: a sheet opened before a resize would be dismissed by it.
        await ApplyVerificationViewAsync();
        RaiseVerificationCrash();
#endif
    }

#if DEBUG
    /// <summary>
    /// Screenshot-matrix hooks. <c>--vigil-theme=light|dark</c> sets the window content's theme (the switch the theme
    /// service flips). <c>--vigil-route=&lt;route&gt;[:&lt;pane&gt;]</c> opens a shell destination (Today, Patients, Agenda,
    /// Settings), a case page (Record, Redose, ParentPreview) or a sheet (!NewCase, !SignOff, !Menu) the way the app's own
    /// commands do: in the shell's content frame, with the case under anesthesia (or the first case) as data.
    /// <c>Record:Monitor</c> opens the record with that pane's drawer; <c>--vigil-scroll=&lt;px&gt;</c> then scrolls the page. Each step
    /// prints a <c>VERIFY</c> line to stderr. Sheets and panes close when the window resizes (light dismiss), so the
    /// matrix opens them per width with <c>--clover-size</c> instead of live-resizing.
    /// </summary>
    private async Task ApplyVerificationViewAsync()
    {
        static string? Arg(string name) => Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith($"--vigil-{name}=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
        try
        {
            if (Host is null || MainWindow?.Content is not FrameworkElement root)
            {
                return;
            }
            // The shell owns the content frame and the window's scoped services; wait for it and its navigator.
            ShellPage? shell = null;
            INavigator? navigator = null;
            for (var i = 0; i < 100 && navigator is null; i++)
            {
                await Task.Delay(100);
                shell = Diagnostics.VisualTree.Find<ShellPage>(root);
                navigator = shell?.Navigator();
            }
            if (Arg("theme") is { } theme)
            {
                // Through the window's theme service, as Settings does: Settings reads the service's theme back, so a
                // theme set around it showed Settings in the other one. The service persists the choice, like a user's.
                var dark = theme.Equals("dark", StringComparison.OrdinalIgnoreCase);
                var service = shell?.FindServiceProvider()?.GetService<Uno.Extensions.Toolkit.IThemeService>();
                if (service is not null)
                {
                    await service.SetThemeAsync(dark ? Uno.Extensions.Toolkit.AppTheme.Dark : Uno.Extensions.Toolkit.AppTheme.Light);
                }
                else
                {
                    root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
                }
                Console.Error.WriteLine($"VERIFY theme={(dark ? "Dark" : "Light")} service={service is not null}");
            }
            if (Arg("route") is not { } spec)
            {
                return;
            }
            if (shell is null || navigator is null)
            {
                Console.Error.WriteLine($"VERIFY route={spec} failed: no shell navigator");
                return;
            }
            var parts = spec.Split(':', 2);
            var route = parts[0];
            var name = route.TrimStart('!');
            var cases = await Host.Services.GetRequiredService<ICaseStore>().GetAllAsync(CancellationToken.None);
            var target = cases.FirstOrDefault(c => c.Status == CaseStatus.Anesthetized) ?? cases.FirstOrDefault();
            object? data = name switch
            {
                "Record" or "Redose" or "SignOff" or "ParentPreview" when target is not null =>
                    parts.Length == 2 ? new CaseRefPane(target.Id, parts[1]) : new CaseRef(target.Id),
                "Menu" => new MenuContext(ShellSection.Today),
                _ => null,
            };
            var qualifier = route.StartsWith('!') ? Qualifiers.None : name is "Today" or "Patients" or "Agenda" or "Settings"
                ? Qualifiers.Nested + Qualifiers.ClearBackStack
                : Qualifiers.Nested;
            // Sheets wait for a result that never comes here: do not await them.
            var navigation = navigator.NavigateRouteAsync(shell, route, qualifier: qualifier, data: data);
            if (route.StartsWith('!'))
            {
                await Task.Delay(500);
                Console.Error.WriteLine($"VERIFY route={spec} data={data} sheet=requested");
                return;
            }
            var response = await navigation;
            Console.Error.WriteLine($"VERIFY route={spec} data={data} success={response?.Success}");
            // --vigil-scroll=<px>: scroll the page below the fold (the page's outermost vertical ScrollViewer).
            if (Arg("scroll") is { } scroll && double.TryParse(scroll, System.Globalization.CultureInfo.InvariantCulture, out var offset))
            {
                await Task.Delay(3000);
                // The page's own scroller is the one with the tallest content (panes and cards hold smaller ones).
                var frame = Diagnostics.VisualTree.Find<Frame>(shell);
                var viewer = frame is null ? null : Diagnostics.VisualTree.All<ScrollViewer>(frame).OrderByDescending(v => v.ExtentHeight).FirstOrDefault();
                viewer?.ChangeView(null, offset, null, disableAnimation: true);
                Console.Error.WriteLine($"VERIFY scroll={offset} viewer={viewer?.Name} extent={viewer?.ExtentHeight}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"VERIFY failed: {ex}");
        }
    }

    /// <summary>
    /// Crash-report hooks, each landing in the log and in crash.log (<see cref="CrashReporter"/>):
    /// <c>--vigil-crash=launch</c> throws inside the launch, <c>--vigil-crash=ui</c> from an element's Loaded handler
    /// (Uno raises those as Application.UnhandledException), <c>--vigil-crash=task</c> leaves a faulted task unobserved.
    /// Key handlers and DispatcherQueue work are no probe: Uno 7 on desktop catches what they throw and only traces it
    /// (measured), so it never reaches a handler.
    /// </summary>
    private void RaiseVerificationCrash()
    {
        var crash = Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith("--vigil-crash=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
        if (crash is null)
        {
            return;
        }
        Console.Error.WriteLine($"VERIFY crash={crash} log={CrashReporter.LogPath}");
        if (crash == "ui" && MainWindow?.Content is FrameworkElement content)
        {
            var probe = new Border();
            probe.Loaded += (_, _) => throw new InvalidOperationException("Verification crash on the UI thread (--vigil-crash=ui).");
            _ = new Microsoft.UI.Xaml.Controls.Primitives.Popup { Child = probe, XamlRoot = content.XamlRoot, IsOpen = true };
        }
        else if (crash == "task")
        {
            _ = Task.Run(() => throw new InvalidOperationException("Verification crash in an unobserved task (--vigil-crash=task)."));
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            });
        }
    }

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
            if (string.IsNullOrWhiteSpace(prefs.ClinicianFirstName))
            {
                await prefs.SetClinicianFirstNameAsync("Amara", CancellationToken.None);
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
#if DEBUG || PAGE_TIMER
        // The page-change timing run keeps its sample day out of the real records.
        if (Diagnostics.PageTimer.OutputPath is not null)
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "vigil-perf");
#endif
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
