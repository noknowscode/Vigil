using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace Vigil.Services;

/// <summary>How a CSV export ended.</summary>
public enum ExportStatus
{
    /// <summary>The file was written (<see cref="ExportResult.FileName"/>).</summary>
    Saved,

    /// <summary>The user closed the save dialog without a file.</summary>
    Cancelled,

    /// <summary>No save dialog could open on this device, so nothing was written.</summary>
    Unavailable,
}

public sealed record ExportResult(ExportStatus Status, string FileName = "");

/// <summary>Hands a signed record to the practice: CSV through the system save picker, summary through the clipboard.</summary>
public interface IRecordExporter
{
    ValueTask<ExportResult> ExportCsvAsync(Case record, CancellationToken ct);

    ValueTask CopySummaryAsync(Case record, CancellationToken ct);
}

/// <param name="window">The main window: pickers and the clipboard run on its UI thread, and WinAppSDK pickers need its handle.</param>
public sealed class RecordExporter(Window window) : IRecordExporter
{
    public async ValueTask<ExportResult> ExportCsvAsync(Case record, CancellationToken ct)
    {
        var csv = CsvExport.ToCsv(record);
        // With its extension: a picker that keeps the suggested name as typed (the Linux file chooser portal) saves it so.
        var name = SuggestedFileName(record);
        // Pickers must run on the UI thread; MVUX commands may not (runtime gotcha).
        return await OnUiThreadAsync(async () =>
        {
            var picker = new FileSavePicker { SuggestedFileName = name, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
#if WINDOWS
            // WinAppSDK pickers throw without an owner window.
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
#endif
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return new ExportResult(SaveDialogUnavailable() ? ExportStatus.Unavailable : ExportStatus.Cancelled);
            }
            await FileIO.WriteTextAsync(file, csv);
            return new ExportResult(ExportStatus.Saved, file.Name);
        });
    }

    public async ValueTask CopySummaryAsync(Case record, CancellationToken ct)
    {
        var text = CsvExport.ToSummary(record);
        await OnUiThreadAsync(() =>
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            return Task.FromResult(true);
        });
    }

    /// <summary>"Knuckles-2026-10-07-anesthesia.csv".</summary>
    public static string SuggestedFileName(Case record) => $"{Safe(record.Patient.Name)}-{record.CreatedAt:yyyy-MM-dd}-anesthesia.csv";

    /// <summary>
    /// True where the save dialog cannot have opened: on Linux desktop it is the desktop portal, reached over the session
    /// bus, and without a session bus address Uno's picker returns no file at once, as a cancel would (measured). Any
    /// other empty result is the user's cancel.
    /// </summary>
    private static bool SaveDialogUnavailable()
    {
#if __DESKTOP__
        return OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));
#else
        return false;
#endif
    }

    /// <summary>Runs <paramref name="work"/> on the window's UI thread: MVUX commands may run elsewhere.</summary>
    private Task<T> OnUiThreadAsync<T>(Func<Task<T>> work)
    {
        var queue = window.DispatcherQueue;
        if (queue.HasThreadAccess)
        {
            return work();
        }
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.TryEnqueue(async () =>
            {
                try
                {
                    tcs.SetResult(await work());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("The UI thread is not available."));
        }
        return tcs.Task;
    }

    private static string Safe(string s) => string.Concat(s.Where(char.IsLetterOrDigit)) is { Length: > 0 } x ? x : "record";
}
