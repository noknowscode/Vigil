using System.Diagnostics;
using System.Windows.Input;
using Microsoft.UI.Xaml.Media;

namespace Vigil.Diagnostics;

/// <summary>
/// Page-change timing run, in every configuration (Release is the one that matters). With <c>VIGIL_PERF=&lt;csv path&gt;</c>
/// the app uses its own data folder (<c>vigil-perf</c>, seeded with the sample day), steps through the sidebar
/// destinations twice through the shell's own commands (no input is injected), writes one CSV row per page change and
/// exits. Per change: request to frame navigated, page Loaded, first rendered frame after Loaded, and settled (the
/// page's element count unchanged for 1.5 s, so a loading skeleton does not count as settled; the time is the last change).
/// </summary>
internal static class PageTimer
{
    public static string? OutputPath => Environment.GetEnvironmentVariable("VIGIL_PERF") is { Length: > 0 } path ? path : null;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static async Task RunAsync(Window window, IServiceProvider services, string output)
    {
        var rows = new List<string> { "run,step,page,navigated_ms,loaded_ms,first_frame_ms,settled_ms,elements" };
        try
        {
            var store = services.GetRequiredService<ICaseStore>();
            if ((await store.GetAllAsync(CancellationToken.None)).Count == 0)
            {
                foreach (var c in SampleDay.Create(services.GetRequiredService<IClock>().Now))
                {
                    await store.SaveAsync(c, CancellationToken.None);
                }
            }

            var shell = await PollAsync(() => Find<ShellPage>(window.Content));
            var frame = await PollAsync(() => Find<Frame>(shell));
            var board = await PollAsync(() => frame.Content as BoardPage);
            var (settled, count) = await SettleAsync(board, Stopwatch.StartNew());
            rows.Add($"0,startup,{nameof(BoardPage)},,,,{(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds:0},{count}");

            var vm = (ShellViewModel)shell.DataContext;
            (string Step, ICommand Command, object? Parameter)[] steps =
            [
                (ShellSection.Patients, vm.Go, ShellSection.Patients),
                (ShellSection.Settings, vm.Go, ShellSection.Settings),
                (ShellSection.Agenda, vm.Go, ShellSection.Agenda),
                (ShellSection.Today, vm.Go, ShellSection.Today),
                ("Record", vm.OpenAnesthesia, null),
                (ShellSection.Today + " (back)", vm.Go, ShellSection.Today),
            ];
            for (var run = 1; run <= 2; run++)
            {
                foreach (var (step, command, parameter) in steps)
                {
                    rows.Add($"{run},{step},{await MeasureAsync(frame, command, parameter)}");
                }
            }
        }
        catch (Exception ex)
        {
            rows.Add($"error,,,,,,,\"{ex.GetType().Name}: {ex.Message.Replace('"', '\'')}\"");
        }
        await File.WriteAllLinesAsync(output, rows);
        Application.Current.Exit();
    }

    private static async Task<string> MeasureAsync(Frame frame, ICommand command, object? parameter)
    {
        var navigated = new TaskCompletionSource<Page>();
        void OnNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                navigated.TrySetResult(page);
            }
        }
        frame.Navigated += OnNavigated;
        var clock = Stopwatch.StartNew();
        command.Execute(parameter);
        var page = await navigated.Task.WaitAsync(Timeout);
        var navigatedMs = clock.Elapsed.TotalMilliseconds;
        frame.Navigated -= OnNavigated;

        if (!page.IsLoaded)
        {
            var loaded = new TaskCompletionSource();
            page.Loaded += (_, _) => loaded.TrySetResult();
            await loaded.Task.WaitAsync(Timeout);
        }
        var loadedMs = clock.Elapsed.TotalMilliseconds;
        await NextFrameAsync();
        var firstFrameMs = clock.Elapsed.TotalMilliseconds;
        var (settledMs, count) = await SettleAsync(page, clock);
        return $"{page.GetType().Name},{navigatedMs:0},{loadedMs:0},{firstFrameMs:0},{settledMs:0},{count}";
    }

    /// <summary>Waits until the element count under <paramref name="root"/> holds for 1.5 s; returns the last change time.</summary>
    private static async Task<(double At, int Count)> SettleAsync(DependencyObject root, Stopwatch clock)
    {
        var count = Count(root);
        var changedAt = clock.Elapsed.TotalMilliseconds;
        while (clock.Elapsed.TotalMilliseconds - changedAt < 1500 && clock.Elapsed < Timeout)
        {
            await Task.Delay(50);
            var now = Count(root);
            if (now != count)
            {
                count = now;
                changedAt = clock.Elapsed.TotalMilliseconds;
            }
        }
        return (changedAt, count);
    }

    private static Task NextFrameAsync()
    {
        var rendered = new TaskCompletionSource();
        void OnRendering(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnRendering;
            rendered.TrySetResult();
        }
        CompositionTarget.Rendering += OnRendering;
        return rendered.Task.WaitAsync(Timeout);
    }

    private static async Task<T> PollAsync<T>(Func<T?> find) where T : class
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < Timeout)
        {
            if (find() is { } found)
            {
                return found;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"{typeof(T).Name} did not appear within {Timeout.TotalSeconds:0} s.");
    }

    private static T? Find<T>(DependencyObject? root) where T : class, DependencyObject
    {
        if (root is null)
        {
            return null;
        }
        if (root is T match)
        {
            return match;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (Find<T>(VisualTreeHelper.GetChild(root, i)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static int Count(DependencyObject root)
    {
        var n = 1;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            n += Count(VisualTreeHelper.GetChild(root, i));
        }
        return n;
    }
}
