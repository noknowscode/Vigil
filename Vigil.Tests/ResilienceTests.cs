using Microsoft.Extensions.Logging.Abstractions;
using Vigil.Presentation;

namespace Vigil.Tests;

public class CrashReporterTests
{
    [Test]
    public void AnExceptionIsWrittenToTheCrashFileOnce()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "crash.log");
        try
        {
            CrashReporter.LogPath = path;
            var crash = new InvalidOperationException("The record view could not be built.");

            CrashReporter.Report("OnLaunched", crash);
            CrashReporter.Report("UI thread", crash);

            var text = File.ReadAllText(path);
            text.Should().Contain("OnLaunched").And.Contain("The record view could not be built.");
            text.Should().NotContain("UI thread", "the rethrown exception reaches the global handlers too, and is reported once");
        }
        finally
        {
            CrashReporter.LogPath = null;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Test]
    public void AnUnwritableCrashFileDoesNotThrow()
    {
        var file = Path.GetTempFileName();
        try
        {
            // The crash file's folder is a file: the write fails, the report must not throw a second exception.
            CrashReporter.LogPath = Path.Combine(file, "crash.log");
            var report = () => CrashReporter.Report("AppDomain", new InvalidOperationException("boom"));
            report.Should().NotThrow();
        }
        finally
        {
            CrashReporter.LogPath = null;
            File.Delete(file);
        }
    }
}

public class PreferenceSaveTests : ModelTestBase
{
    [Test]
    public async Task AFailedWriteKeepsTheFileAndStillAppliesTheValue()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "preferences.json");
        try
        {
            await new Preferences(path).SetPlannedPrepMinutesAsync(30, CancellationToken.None);
            var before = File.ReadAllText(path);
            // A folder where the temporary file goes: the write fails before the saved file is touched.
            Directory.CreateDirectory(path + ".tmp");

            var preferences = new Preferences(path);
            var save = async () => await preferences.SetPlannedPrepMinutesAsync(45, CancellationToken.None);

            await save.Should().ThrowAsync<Exception>().Where(ex => ex is IOException || ex is UnauthorizedAccessException);
            preferences.PlannedPrepMinutes.Should().Be(45, "the new value applies until the app closes");
            File.ReadAllText(path).Should().Be(before, "a failed write never replaces the saved file");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Test]
    public async Task SettingsSaysWhenAPreferenceWasNotSaved()
    {
        var file = Path.GetTempFileName();
        try
        {
            // The preferences folder is a file, so every save fails.
            var preferences = new Preferences(Path.Combine(file, "preferences.json"));
            var model = new SettingsModel(null!, null!, preferences, Store, Clock, null!, NullLogger<SettingsModel>.Instance);
            (await model.ClinicianName).Should().BeEmpty();

            await model.ClinicianName.UpdateAsync(_ => "Okafor", CancellationToken.None);
            await UntilAsync(async () => !string.IsNullOrEmpty(await model.SaveError));

            (await model.SaveError).Should().Be("This setting applies until Clover closes, but it could not be saved on this device.");
            preferences.ClinicianName.Should().Be("Okafor");

            await model.DismissSaveError(CancellationToken.None);
            (await model.SaveError).Should().BeEmpty();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
