using Uno.Extensions.Toolkit;

namespace Vigil.Presentation;

public partial record SettingsModel(
    IThemeService Theme,
    ILocalizationService Localization,
    IPreferences Preferences,
    ICaseStore Store,
    IClock Clock,
    INavigator Navigator)
{
    private static readonly string[] Languages = ["en", "fr"];

    /// <summary>0 system, 1 light (Paper), 2 dark (Theatre). Applied on user change only (post-load: theme gotcha).</summary>
    public IState<int> ThemeIndex => State.Value(this, () => Theme.Theme switch { AppTheme.Light => 1, AppTheme.Dark => 2, _ => 0 })
        .ForEach(async (index, ct) => await Theme.SetThemeAsync(index switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.System }));

    /// <summary>0 English, 1 French. Saved now, applied at the next start.</summary>
    public IState<int> LanguageIndex => State.Value(this, () => Localization.CurrentCulture.TwoLetterISOLanguageName == "fr" ? 1 : 0)
        .ForEach(async (index, ct) =>
        {
            var culture = Languages[Math.Clamp(index, 0, 1)];
            if (Localization.CurrentCulture.TwoLetterISOLanguageName != culture)
            {
                await Localization.SetCurrentCultureAsync(new System.Globalization.CultureInfo(culture));
                await LanguageNotice.UpdateAsync(_ => culture == "fr" ? "Langue enregistrée : redémarrez Clover pour l'appliquer." : "Language saved: restart Clover to apply it.", ct);
            }
        });

    public IState<bool> ReduceMotion => State.Value(this, () => (Preferences as Preferences)?.ReduceMotionSetting ?? Preferences.ReduceMotion)
        .ForEach(async (value, ct) => await Preferences.SetReduceMotionAsync(value, ct));

    /// <summary>Index into 3 / 5 / 10 minutes.</summary>
    public IState<int> IntervalIndex => State.Value(this, () => Math.Max(0, Array.IndexOf(Services.Preferences.Intervals, (int)Preferences.ReadingInterval.TotalMinutes)))
        .ForEach(async (index, ct) => await Preferences.SetReadingIntervalAsync(Services.Preferences.Intervals[Math.Clamp(index, 0, 2)], ct));

    /// <summary>Surname for the board greeting; saved when the field loses focus.</summary>
    public IState<string> ClinicianName => State.Value(this, () => Preferences.ClinicianName)
        .ForEach(async (name, ct) => await Preferences.SetClinicianNameAsync(name ?? "", ct));

    /// <summary>First name, for the sidebar avatar initials only; saved when the field loses focus.</summary>
    public IState<string> ClinicianFirstName => State.Value(this, () => Preferences.ClinicianFirstName)
        .ForEach(async (name, ct) => await Preferences.SetClinicianFirstNameAsync(name ?? "", ct));

    public IState<string> Notice => State.Value(this, () => "");

    public IState<string> LanguageNotice => State.Value(this, () => "");

    public async ValueTask LoadSampleDay(CancellationToken ct)
    {
        if (!await ConfirmAsync(Loc.T("Settings_SampleConfirm", "Replace every record on this tablet with the sample surgical day?"), Loc.T("Settings_SampleTitle", "Load sample day"), Loc.T("Settings_SampleAction", "Replace records"), ct))
        {
            return;
        }
        await Store.ClearAsync(ct);
        foreach (var c in SampleDay.Create(Clock.Now))
        {
            await Store.SaveAsync(c, ct);
        }
        await Notice.UpdateAsync(_ => Loc.T("Settings_SampleLoaded", "Sample day loaded: 5 cases, one under anesthesia."), ct);
    }

    public async ValueTask ClearAll(CancellationToken ct)
    {
        if (!await ConfirmAsync(Loc.T("Settings_ClearConfirm", "Delete every anesthesia record on this tablet? Signed records are deleted too. This cannot be undone."), Loc.T("Settings_ClearTitle", "Clear all records"), Loc.T("Settings_ClearAction", "Delete all"), ct))
        {
            return;
        }
        await Store.ClearAsync(ct);
        await Notice.UpdateAsync(_ => Loc.T("Settings_Cleared", "All records deleted from this tablet."), ct);
    }

    private async ValueTask<bool> ConfirmAsync(string content, string title, string action, CancellationToken ct)
    {
        var confirmed = false;
        // The choice comes back through DialogAction callbacks, not the awaited result (runtime gotcha).
        await Navigator.ShowMessageDialogAsync<object>(
            this,
            content: content,
            title: title,
            defaultButtonIndex: 1,
            cancelButtonIndex: 1,
            buttons:
            [
                new DialogAction(action, () => confirmed = true),
                new DialogAction(Loc.T("Common_Cancel", "Cancel")),
            ],
            cancellation: ct);
        return confirmed;
    }
}
