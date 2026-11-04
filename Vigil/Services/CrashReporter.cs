namespace Vigil.Services;

/// <summary>
/// Last-resort reporting for exceptions nothing else caught: on the UI thread (<see cref="Application.UnhandledException"/>),
/// on any thread (<see cref="AppDomain.UnhandledException"/>), and in faulted tasks nobody awaited
/// (<see cref="TaskScheduler.UnobservedTaskException"/>). Each one goes to the app log once the host exists and to
/// <c>crash.log</c> in the data folder: a desktop Uno app is a windowed process, so its console output is lost.
/// Reporting changes no behaviour: an exception that ended the app before still ends it.
/// </summary>
public static class CrashReporter
{
    private const string ReportedKey = "Vigil.CrashReporter.Reported";

    /// <summary>
    /// The app log; null until the host is built, when reports go to the console as well as the file. Static, like the
    /// reporter: its handlers are attached before the host (and its container) exists.
    /// </summary>
    public static ILogger? Logger { get; set; }

    /// <summary>The crash file, appended to; null writes no file.</summary>
    public static string? LogPath { get; set; }

    public static void Attach(Application app, string logPath)
    {
        LogPath = logPath;
        app.UnhandledException += (_, e) => Report("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report("Unobserved task", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Logs <paramref name="exception"/> and appends it to the crash file, once per exception instance.</summary>
    public static void Report(string source, Exception? exception)
    {
        if (exception is not null)
        {
            // OnLaunched reports and rethrows, and the rethrown exception reaches the handlers above as well.
            if (exception.Data.Contains(ReportedKey))
            {
                return;
            }
            exception.Data[ReportedKey] = true;
        }

        if (Logger is { } logger)
        {
            logger.LogCritical(exception, "Unhandled exception ({Source}).", source);
        }
        else
        {
            Console.Error.WriteLine($"Unhandled exception ({source}): {exception}");
        }

        if (LogPath is not { } path)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The report is already in the log; a crash path must not throw a second exception over the first.
        }
    }
}
