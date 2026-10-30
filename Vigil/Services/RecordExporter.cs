using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace Vigil.Services;

/// <summary>Hands a signed record to the practice: CSV through the system save picker, summary through the clipboard.</summary>
public interface IRecordExporter
{
    /// <summary>Returns a user-facing outcome ("Saved Knuckles-2026-10-07.csv" or "Export cancelled").</summary>
    ValueTask<string> ExportCsvAsync(Case record, CancellationToken ct);

    ValueTask<string> CopySummaryAsync(Case record, CancellationToken ct);
}

public sealed class RecordExporter : IRecordExporter
{
    public async ValueTask<string> ExportCsvAsync(Case record, CancellationToken ct)
    {
        var csv = CsvExport.ToCsv(record);
        var name = $"{Safe(record.Patient.Name)}-{record.CreatedAt:yyyy-MM-dd}-anesthesia";
        // Pickers must run on the UI thread; MVUX commands may not (runtime gotcha).
        return await UiThread.RunAsync(async () =>
        {
            var picker = new FileSavePicker { SuggestedFileName = name, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
#if WINDOWS
            // WinAppSDK pickers throw without an owner window.
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(UiThread.Window));
#endif
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return Loc.T("Export_Cancelled", "Export cancelled.");
            }
            await FileIO.WriteTextAsync(file, csv);
            return Loc.F("Export_Saved", "Saved {0}.", file.Name);
        });
    }

    public async ValueTask<string> CopySummaryAsync(Case record, CancellationToken ct)
    {
        var text = CsvExport.ToSummary(record);
        return await UiThread.RunAsync(() =>
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            return Task.FromResult(Loc.T("Export_Copied", "Summary copied: paste it into the patient file."));
        });
    }

    private static string Safe(string s) => string.Concat(s.Where(char.IsLetterOrDigit)) is { Length: > 0 } x ? x : "record";
}
