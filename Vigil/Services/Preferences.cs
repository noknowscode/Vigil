using Uno.Extensions.Reactive;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vigil.Services;

/// <summary>Practice preferences that change app behaviour, persisted next to the cases.</summary>
public interface IPreferences
{
    TimeSpan ReadingInterval { get; }

    /// <summary>In-app setting, OR'ed with the platform's (Skia desktop always reports animations enabled).</summary>
    bool ReduceMotion { get; }

    /// <summary>Raised after a change; feeds that depend on preferences reload from it.</summary>
    Signal Changed { get; }

    ValueTask SetReadingIntervalAsync(int minutes, CancellationToken ct);

    ValueTask SetReduceMotionAsync(bool value, CancellationToken ct);

    /// <summary>Surname for the board greeting ("Good morning, Dr. Okafor"); empty greets without a name.</summary>
    string ClinicianName { get; }

    ValueTask SetClinicianNameAsync(string name, CancellationToken ct);

    /// <summary>Planned preparation time per case, for the care-time insight and the next surgery's prep start.</summary>
    int PlannedPrepMinutes { get; }

    ValueTask SetPlannedPrepMinutesAsync(int minutes, CancellationToken ct);
}

internal sealed record PreferencesFile(int ReadingIntervalMinutes, bool ReduceMotion, string ClinicianName = "", int PlannedPrepMinutes = Preferences.DefaultPlannedPrepMinutes);

[JsonSerializable(typeof(PreferencesFile))]
internal partial class PreferencesJsonContext : JsonSerializerContext;

public sealed class Preferences : IPreferences
{
    public static readonly int[] Intervals = [3, 5, 10];

    public const int DefaultPlannedPrepMinutes = 25;

    private readonly string? _path;
    private PreferencesFile _value = new(5, false);

    public Preferences(string? path = null)
    {
        _path = path;
        if (_path is not null && File.Exists(_path))
        {
            try
            {
                _value = JsonSerializer.Deserialize(File.ReadAllText(_path), PreferencesJsonContext.Default.PreferencesFile) ?? _value;
            }
            catch (JsonException)
            {
                // A damaged preferences file falls back to defaults; the record data is unaffected.
            }
        }
    }

    public TimeSpan ReadingInterval => TimeSpan.FromMinutes(Intervals.Contains(_value.ReadingIntervalMinutes) ? _value.ReadingIntervalMinutes : 5);

    public bool ReduceMotion => _value.ReduceMotion || !PlatformAnimationsEnabled();

    public Signal Changed { get; } = new();

    public ValueTask SetReadingIntervalAsync(int minutes, CancellationToken ct) => SaveAsync(_value with { ReadingIntervalMinutes = minutes }, ct);

    public ValueTask SetReduceMotionAsync(bool value, CancellationToken ct) => SaveAsync(_value with { ReduceMotion = value }, ct);

    public string ClinicianName => _value.ClinicianName ?? "";

    public ValueTask SetClinicianNameAsync(string name, CancellationToken ct) => SaveAsync(_value with { ClinicianName = name.Trim() }, ct);

    public int PlannedPrepMinutes => _value.PlannedPrepMinutes is >= 5 and <= 180 ? _value.PlannedPrepMinutes : DefaultPlannedPrepMinutes;

    public ValueTask SetPlannedPrepMinutesAsync(int minutes, CancellationToken ct) =>
        SaveAsync(_value with { PlannedPrepMinutes = Math.Clamp(minutes, 5, 180) }, ct);

    /// <summary>The user's own setting, without the platform's.</summary>
    public bool ReduceMotionSetting => _value.ReduceMotion;

    private async ValueTask SaveAsync(PreferencesFile value, CancellationToken ct)
    {
        if (value == _value)
        {
            return;
        }
        _value = value;
        if (_path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(value, PreferencesJsonContext.Default.PreferencesFile), ct);
        }
        Changed.Raise();
    }

    private static bool PlatformAnimationsEnabled()
    {
        try
        {
            return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
