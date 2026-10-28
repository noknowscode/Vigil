namespace Vigil.Presentation;

public static class PatientMedia
{
    private static readonly HashSet<string> Allowed = ["bella-english-bulldog.jpg", "bella-outdoor-hero.jpg", "milo-profile.jpg", "oscar-profile.jpg", "juniper-profile.jpg"];
    public static string? Photo(Patient patient) => Asset(patient.PhotoAsset);
    public static string? Hero(Patient patient) => Asset(patient.HeroAsset) ?? Photo(patient);
    private static string? Asset(string? asset) => asset is not null && Allowed.Contains(asset) ? $"ms-appx:///Assets/Pets/{asset}" : null;
}
