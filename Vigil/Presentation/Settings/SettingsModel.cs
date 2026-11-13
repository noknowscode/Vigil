namespace Vigil.Presentation;

public partial record SettingsModel(
    IThemeService Theme,
    ILocalizationService Localization,
    IPreferences Preferences,
    ICaseStore Store,
    IClock Clock,
    INavigator Navigator,
    ILogger<SettingsModel> Logger)
{
    private static readonly string[] Languages = ["en", "fr"];

    /// <summary>
    /// 0 system, 1 light (Paper), 2 dark (Theatre). Applied on user change only (post-load: theme gotcha). The theme
    /// service saves the choice; a save that fails says so under the settings, as every other preference does.
    /// </summary>
    public IState<int> ThemeIndex => State.Value(this, () => Theme.Theme switch { AppTheme.Light => 1, AppTheme.Dark => 2, _ => 0 })
        .ForEach(async (index, ct) => await SavePreferenceAsync(
            async _ => await Theme.SetThemeAsync(index switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.System }), ct));

    /// <summary>0 English, 1 French. Saved now, applied at the next start; the restart notice shows once it is saved.</summary>
    public IState<int> LanguageIndex => State.Value(this, () => Localization.CurrentCulture.TwoLetterISOLanguageName == "fr" ? 1 : 0)
        .ForEach(async (index, ct) =>
        {
            var culture = Languages[Math.Clamp(index, 0, 1)];
            if (Localization.CurrentCulture.TwoLetterISOLanguageName != culture
                && await SavePreferenceAsync(async _ => await Localization.SetCurrentCultureAsync(new System.Globalization.CultureInfo(culture)), ct))
            {
                await LanguageNotice.UpdateAsync(_ => culture == "fr" ? "Langue enregistrée : redémarrez Clover pour l'appliquer." : "Language saved: restart Clover to apply it.", ct);
            }
        });

    public IState<bool> ReduceMotion => State.Value(this, () => Preferences.ReduceMotionSetting)
        .ForEach(async (value, ct) => await SavePreferenceAsync(c => Preferences.SetReduceMotionAsync(value, c), ct));

    /// <summary>Index into 3 / 5 / 10 minutes.</summary>
    public IState<int> IntervalIndex => State.Value(this, () => Math.Max(0, Array.IndexOf(Services.Preferences.Intervals, (int)Preferences.ReadingInterval.TotalMinutes)))
        .ForEach(async (index, ct) => await SavePreferenceAsync(c => Preferences.SetReadingIntervalAsync(Services.Preferences.Intervals[Math.Clamp(index, 0, 2)], c), ct));

    /// <summary>
    /// Planned preparation per case, in minutes, as typed; saved when the field loses focus. A number out of range is
    /// saved clamped to 5-180 and anything else puts back the saved value, so the field always shows what applies.
    /// </summary>
    public IState<string> PlannedPrepMinutes => State.Value(this, () => Text(Preferences.PlannedPrepMinutes))
        .ForEach(async (text, ct) =>
        {
            if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out var minutes))
            {
                await SavePreferenceAsync(c => Preferences.SetPlannedPrepMinutesAsync(minutes, c), ct);
            }
            var saved = Text(Preferences.PlannedPrepMinutes);
            if (text != saved)
            {
                await PlannedPrepMinutes.UpdateAsync(_ => saved, ct);
            }
        });

    private static string Text(int minutes) => minutes.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Surname for the board greeting; saved when the field loses focus.</summary>
    public IState<string> ClinicianName => State.Value(this, () => Preferences.ClinicianName)
        .ForEach(async (name, ct) => await SavePreferenceAsync(c => Preferences.SetClinicianNameAsync(name ?? "", c), ct));

    /// <summary>First name, for the sidebar avatar initials only; saved when the field loses focus.</summary>
    public IState<string> ClinicianFirstName => State.Value(this, () => Preferences.ClinicianFirstName)
        .ForEach(async (name, ct) => await SavePreferenceAsync(c => Preferences.SetClinicianFirstNameAsync(name ?? "", c), ct));

    public IState<string> Notice => State.Value(this, () => "");

    /// <summary>Why the last preference change was not saved on the device (it still applies until the app closes); empty once one saves.</summary>
    public IState<string> SaveError => State.Value(this, () => "");

    public IFeed<bool> HasSaveError => SaveError.Select(s => !string.IsNullOrEmpty(s));

    public async ValueTask DismissSaveError(CancellationToken ct) => await SaveError.UpdateAsync(_ => "", ct);

    /// <summary>Saves one preference; a failure is logged and shown under the settings. True once saved.</summary>
    private async ValueTask<bool> SavePreferenceAsync(Func<CancellationToken, ValueTask> save, CancellationToken ct)
    {
        try
        {
            await save(ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(ex, "Saving a preference failed.");
            await SaveError.UpdateAsync(_ => Loc.T("Settings_PreferenceNotSaved", "This setting applies until Clover closes, but it could not be saved on this device."), ct);
            return false;
        }
        await SaveError.UpdateAsync(_ => "", ct);
        return true;
    }

    public IState<string> LanguageNotice => State.Value(this, () => "");

    public IFeed<bool> HasLanguageNotice => LanguageNotice.Select(s => !string.IsNullOrEmpty(s));

    public async ValueTask LoadSampleDay(CancellationToken ct)
    {
        if (!await Navigator.ConfirmAsync(this, Loc.T("Settings_SampleConfirm", "Replace every record on this tablet with the sample surgical day?"), Loc.T("Settings_SampleTitle", "Load sample day"), Loc.T("Settings_SampleAction", "Replace records"), ct))
        {
            return;
        }
        try
        {
            await Store.ReplaceAllAsync(SampleDay.Create(Clock.Now), ct);
        }
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            Logger.LogWarning(ex, "Loading the sample day failed.");
            await Notice.UpdateAsync(_ => Loc.T("Settings_SampleFailed", "The sample day could not be loaded. Your records are unchanged."), ct);
            return;
        }
        await Notice.UpdateAsync(_ => Loc.T("Settings_SampleLoadedFour", "Sample day loaded: 4 cases, one under anesthesia."), ct);
    }

    public async ValueTask ClearAll(CancellationToken ct)
    {
        if (!await Navigator.ConfirmAsync(this, Loc.T("Settings_ClearConfirm", "Delete every anesthesia record on this tablet? Signed records are deleted too. This cannot be undone."), Loc.T("Settings_ClearTitle", "Clear all records"), Loc.T("Settings_ClearAction", "Delete all"), ct))
        {
            return;
        }
        try
        {
            await Store.ClearAsync(ct);
        }
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            Logger.LogWarning(ex, "Clearing the records failed.");
            await Notice.UpdateAsync(_ => Loc.T("Settings_ClearFailed", "Some records could not be deleted. Try again."), ct);
            return;
        }
        await Notice.UpdateAsync(_ => Loc.T("Settings_Cleared", "All records deleted from this tablet."), ct);
    }
}
