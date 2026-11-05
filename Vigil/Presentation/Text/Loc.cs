using System.Globalization;

namespace Vigil.Presentation;

/// <summary>
/// Localized text for strings built in code (statuses, countdowns, validation). XAML text uses x:Uid.
/// The English text sits beside each key, so tests and a missing resource still read correctly;
/// tools/localize.py collects every key into Strings/{en,fr}/Resources.resw.
/// </summary>
public static class Loc
{
    /// <summary>
    /// Set once from the host's IStringLocalizer at launch. It stays static because the text is built inside MVUX view
    /// records and XAML-created controls, which the container never constructs.
    /// </summary>
    public static IStringLocalizer? Localizer { get; internal set; }

    public static string T(string key, string english)
    {
        var s = Localizer?[key];
        return s is { ResourceNotFound: false } && !string.IsNullOrEmpty(s.Value) ? s.Value : english;
    }

    public static string F(string key, string english, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key, english), args);

    public static string Species(Species s) => s switch
    {
        Domain.Species.Canine => Loc.T("Species_Canine", "Canine"),
        Domain.Species.Feline => Loc.T("Species_Feline", "Feline"),
        _ => Loc.T("Species_Other", "Other"),
    };

    /// <summary>"Canine · Labrador retriever", skipping a blank breed.</summary>
    public static string SpeciesAndBreed(Patient p) => string.IsNullOrWhiteSpace(p.Breed) ? Species(p.Species) : $"{Species(p.Species)} · {p.Breed}";

    public static string Plane(Plane p) => p switch
    {
        Domain.Plane.Light => Loc.T("Plane_Light", "Light"),
        Domain.Plane.Deep => Loc.T("Plane_Deep", "Deep"),
        _ => Loc.T("Plane_Surgical", "Surgical"),
    };
}
