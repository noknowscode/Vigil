namespace Vigil.Presentation;

public static class BoardDestination
{
    public static Case? Surgery(IEnumerable<Case> cases) => cases.FirstOrDefault(c => c.Status == CaseStatus.Anesthetized);
    public static Case? Patient(IEnumerable<Case> cases)
    {
        var all = cases.ToArray();
        return Surgery(all) ?? all.FirstOrDefault(c => c.Status == CaseStatus.Recovery)
            ?? all.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
    }
}
