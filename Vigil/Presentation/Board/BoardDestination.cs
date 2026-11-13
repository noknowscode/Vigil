namespace Vigil.Presentation;

public static class BoardDestination
{
    /// <summary>The case under anesthesia, which the sidebar's Anesthesia item opens.</summary>
    public static Case? Surgery(IEnumerable<Case> cases) => cases.FirstOrDefault(c => c.Status == CaseStatus.Anesthetized);
}
