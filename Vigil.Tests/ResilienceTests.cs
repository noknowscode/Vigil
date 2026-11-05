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

/// <summary>Theme and language choices are saved by Uno's services; a save the device refuses is reported, not lost.</summary>
public class SettingsSaveTests : ModelTestBase
{
    [Test]
    public async Task AThemeThatCannotBeSavedSaysSo()
    {
        var model = new SettingsModel(new RefusingTheme(), new English(), new Preferences(), Store, Clock, Nav, NullLogger<SettingsModel>.Instance);
        (await model.ThemeIndex).Should().Be(0);

        await model.ThemeIndex.UpdateAsync(_ => 2, CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("This setting applies until Clover closes, but it could not be saved on this device.");
    }

    [Test]
    public async Task ALanguageThatCannotBeSavedSaysSoAndPromisesNoRestart()
    {
        var language = new English { Refuses = true };
        var model = new SettingsModel(new RefusingTheme(), language, new Preferences(), Store, Clock, Nav, NullLogger<SettingsModel>.Instance);
        (await model.LanguageIndex).Should().Be(0);

        await model.LanguageIndex.UpdateAsync(_ => 1, CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("This setting applies until Clover closes, but it could not be saved on this device.");
        (await model.LanguageNotice).Should().BeEmpty("nothing was saved, so a restart changes nothing");
    }

    [Test]
    public async Task ASavedLanguageAsksForARestart()
    {
        var language = new English();
        var model = new SettingsModel(new RefusingTheme(), language, new Preferences(), Store, Clock, Nav, NullLogger<SettingsModel>.Instance);
        (await model.LanguageIndex).Should().Be(0);

        await model.LanguageIndex.UpdateAsync(_ => 1, CancellationToken.None);

        (await SettledAsync(model.LanguageNotice, e => e != "")).Should().Be("Langue enregistrée : redémarrez Clover pour l'appliquer.");
        language.CurrentCulture.Name.Should().Be("fr");
    }

    [Test]
    public void AnUnreadablePreferencesFileFallsBackToTheDefaults()
    {
        Assume.That(OperatingSystem.IsLinux(), "a socket file stands in for an unreadable file (File.Exists is true, opening it fails)");
        var folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "preferences.json");
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        try
        {
            socket.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint(path));
            File.Exists(path).Should().BeTrue();

            var preferences = new Preferences(path);

            preferences.ReadingInterval.Should().Be(TimeSpan.FromMinutes(5));
            preferences.PlannedPrepMinutes.Should().Be(Preferences.DefaultPlannedPrepMinutes);
        }
        finally
        {
            socket.Dispose();
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A theme service whose save fails, as a full or read-only disk makes it.</summary>
    private sealed class RefusingTheme : Uno.Extensions.Toolkit.IThemeService
    {
        public bool IsDark => false;
        public Uno.Extensions.Toolkit.AppTheme Theme => Uno.Extensions.Toolkit.AppTheme.System;
        public event EventHandler<Uno.Extensions.Toolkit.AppTheme>? ThemeChanged { add { } remove { } }
        public Task<bool> SetThemeAsync(Uno.Extensions.Toolkit.AppTheme theme) => throw new IOException("No space left on device.");
        public Task InitializeAsync() => Task.CompletedTask;
    }

    /// <summary>English to start with; <see cref="Refuses"/> makes the save fail.</summary>
    private sealed class English : Uno.Extensions.Localization.ILocalizationService
    {
        public bool Refuses { get; init; }
        public System.Globalization.CultureInfo[] SupportedCultures { get; } = [new("en"), new("fr")];
        public System.Globalization.CultureInfo CurrentCulture { get; private set; } = new("en");
        public Task SetCurrentCultureAsync(System.Globalization.CultureInfo newCulture)
        {
            if (Refuses)
            {
                throw new UnauthorizedAccessException("Access to the settings file is denied.");
            }
            CurrentCulture = newCulture;
            return Task.CompletedTask;
        }
    }
}
