#if DEBUG
namespace Vigil;

/// <summary>
/// Debug-only verification hooks, read from the command line and printed as <c>VERIFY</c> lines to stderr. The file is
/// compiled in Debug only: in Release the partial methods <see cref="App"/> calls have no body, so the calls compile away.
/// <c>--vigil-seed</c>, <c>--vigil-now</c>, <c>--vigil-theme</c>, <c>--vigil-route</c>, <c>--vigil-scroll</c>,
/// <c>--vigil-focuslog</c>, <c>--vigil-crash</c>, <c>--vigil-memlog</c>, <c>--clover-preview</c> and <c>--clover-size</c>.
/// </summary>
public partial class App
{
    private static string? Arg(string name) => Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.StartsWith($"--vigil-{name}=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];

    private static bool HasFlag(string flag) => Environment.GetCommandLineArgs().Contains(flag);

    /// <summary><c>--vigil-crash=launch</c>: throws inside the launch, which reports it and rethrows.</summary>
    partial void OnLaunching()
    {
        if (Arg("crash") == "launch")
        {
            throw new InvalidOperationException("Verification crash during launch (--vigil-crash=launch).");
        }
    }

    /// <summary>The VIGIL_FAULT / --vigil-fault test hook; Release never injects a fault.</summary>
    static partial void UseVerificationFault(ref IFaultInjection fault) => fault = FaultInjection.FromEnvironment();

    /// <summary><c>--vigil-now=12:42:26</c> (or <c>--clover-preview</c>) pins the wall time the app starts at.</summary>
    static partial void UseVerificationClock(ref IClock clock)
    {
        var at = Arg("now") ?? (HasFlag("--clover-preview") ? "12:42:26" : null);
        if (at is not null && TimeSpan.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            clock = ShiftedClock.At(time);
        }
    }

    /// <summary>The design preview (<c>--clover-preview</c>) keeps its sample day out of the real records.</summary>
    static partial void UseVerificationDataRoot(ref string root)
    {
        if (HasFlag("--clover-preview"))
        {
            root = Path.Combine(ApplicationData.Current.LocalFolder.Path, "clover-design-preview");
        }
    }

    /// <summary>
    /// <c>--vigil-seed=sample</c> loads the sample day, <c>--vigil-seed=empty</c> clears the store, <c>--vigil-seed=next</c>
    /// loads it without the case in surgery. The design preview seeds the sample day and Dr. Okafor.
    /// </summary>
    private static async Task SeedForVerificationAsync(IServiceProvider services)
    {
        var seed = Arg("seed");
        if (HasFlag("--clover-preview"))
        {
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

    /// <summary>
    /// <c>--vigil-memlog</c>, for the break-it memory loop: the managed heap after a forced full collection every 10 s,
    /// which separates a real leak from a collector that has not run yet.
    /// </summary>
    partial void OnWindowCreated(Window window)
    {
        if (!HasFlag("--vigil-memlog"))
        {
            return;
        }
        var timer = window.DispatcherQueue.CreateTimer();
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

    /// <summary>Once the first page shows: the window size, then the theme, route and scroll, the crash probes and the focus log.</summary>
    partial void OnLaunchCompleted() => _ = VerifyAfterLaunchAsync();

    private async Task VerifyAfterLaunchAsync()
    {
        ResizeForVerification();
        // After any --clover-size resize: a sheet opened before a resize would be dismissed by it.
        await ApplyVerificationViewAsync();
        RaiseVerificationCrash();
        StartFocusLog();
    }

    /// <summary><c>--clover-preview</c> sizes the window 1440 x 1000, <c>--clover-size=WxH</c> to that size (desktop and Windows).</summary>
    private void ResizeForVerification()
    {
#if __DESKTOP__ || WINDOWS
        if (MainWindow is null)
        {
            return;
        }
        // Windows can restore the window maximized, and Resize does nothing on a maximized window: restore it first.
        if (HasFlag("--clover-preview") && MainWindow.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Maximized } presenter)
            presenter.Restore();
        if (HasFlag("--clover-preview"))
            MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1440, Height = 1000 });
        var previewSize = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--clover-size="));
        if (previewSize is not null)
        {
            var size = previewSize.Split('=')[1].Split('x');
            if (size.Length == 2 && int.TryParse(size[0], out var width) && int.TryParse(size[1], out var height))
                MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = Math.Clamp(width, 320, 1920), Height = Math.Clamp(height, 480, 1200) });
        }
#endif
    }

    /// <summary>
    /// Screenshot-matrix hooks. <c>--vigil-theme=light|dark</c> sets the window content's theme (the switch the theme
    /// service flips). <c>--vigil-route=&lt;route&gt;[:&lt;pane&gt;]</c> opens a shell destination (Today, Patients, Agenda,
    /// Settings), a case page (Record, Redose, ParentPreview) or a sheet (!NewCase, !EditCase, !SignOff, !Menu) the way the app's own
    /// commands do: in the shell's content frame, with the case under anesthesia (or the first case) as data.
    /// <c>Record:Monitor</c> opens the record with that pane's drawer; <c>--vigil-scroll=&lt;px&gt;</c> then scrolls the page. Each step
    /// prints a <c>VERIFY</c> line to stderr. Sheets and panes close when the window resizes (light dismiss), so the
    /// matrix opens them per width with <c>--clover-size</c> instead of live-resizing.
    /// </summary>
    private async Task ApplyVerificationViewAsync()
    {
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
                "Record" or "Redose" or "SignOff" or "EditCase" or "ParentPreview" when target is not null =>
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
    /// Keyboard check hook: <c>--vigil-focuslog</c> prints each element that gets focus (type and automation name) as a
    /// <c>VERIFY focus=</c> line, so a keyboard-only walk can be read back without a focus visual in the capture.
    /// </summary>
    private static void StartFocusLog()
    {
        if (!HasFlag("--vigil-focuslog"))
        {
            return;
        }
        Microsoft.UI.Xaml.Input.FocusManager.GotFocus += (_, e) => Console.Error.WriteLine(
            $"VERIFY focus={e.NewFocusedElement?.GetType().Name} name=\"{(e.NewFocusedElement is DependencyObject d ? Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(d) : "")}\"");
    }

    /// <summary>
    /// Crash-report hooks, each landing in the log and in crash.log (<see cref="CrashReporter"/>):
    /// <c>--vigil-crash=launch</c> throws inside the launch (<see cref="OnLaunching"/>), <c>--vigil-crash=ui</c> from an
    /// element's Loaded handler (Uno raises those as Application.UnhandledException), <c>--vigil-crash=task</c> leaves a
    /// faulted task unobserved. Key handlers and DispatcherQueue work are no probe: Uno 7 on desktop catches what they
    /// throw and only traces it (measured), so it never reaches a handler.
    /// </summary>
    private void RaiseVerificationCrash()
    {
        var crash = Arg("crash");
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
}
#endif
