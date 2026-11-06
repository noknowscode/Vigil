using Windows.UI;

namespace Vigil.Controls;

/// <summary>
/// Resolves a theme colour for a specific element theme. Elements built in code have no {ThemeResource}, so the care
/// ruler takes a snapshot of its colours on load and on every ActualThemeChanged (never through a converter: a
/// converter snapshots the app theme once and is never re-asked).
/// </summary>
internal static class ThemePalette
{
    public static Color Resolve(string key, ElementTheme theme, Color fallback)
    {
        var themeKey = theme == ElementTheme.Dark ? "Dark" : "Light";
        // Theme dictionaries first, depth-first through merged dictionaries. A plain TryGetValue on the app
        // dictionary answers with the application-level theme (Light), not this element's (measured: Paper colours
        // under Theatre). Merged dictionaries are walked last-first: later ones win in WinUI.
        return FindThemed(Application.Current.Resources, key, themeKey)
            ?? FindDirect(Application.Current.Resources, key)
            ?? fallback;
    }

    private static Color? FindThemed(ResourceDictionary dictionary, string key, string themeKey)
    {
        foreach (var name in themeKey == "Light" ? new[] { "Light", "Default" } : new[] { "Dark", "Default" })
        {
            if (dictionary.ThemeDictionaries.TryGetValue(name, out var t) && t is ResourceDictionary td && Value(td, key) is Color c)
            {
                return c;
            }
        }
        foreach (var merged in dictionary.MergedDictionaries.Reverse())
        {
            if (FindThemed(merged, key, themeKey) is Color found)
            {
                return found;
            }
        }
        return null;
    }

    private static Color? FindDirect(ResourceDictionary dictionary, string key)
    {
        foreach (var merged in dictionary.MergedDictionaries.Reverse())
        {
            if (FindDirect(merged, key) is Color found)
            {
                return found;
            }
        }
        return Value(dictionary, key);
    }

    // Explicit Color? arms: the iOS compiler typed the switch as Color and rejected the null arm (CS0037).
    private static Color? Value(ResourceDictionary d, string key) =>
        d.TryGetValue(key, out var v) ? v switch
        {
            Color c => (Color?)c,
            SolidColorBrush b => (Color?)b.Color,
            _ => (Color?)null,
        } : null;
}
