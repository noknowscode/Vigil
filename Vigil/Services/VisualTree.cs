#if DEBUG || PAGE_TIMER
namespace Vigil.Diagnostics;

/// <summary>Visual-tree lookups for the Debug verification hooks (App) and the page-change timing run (PageTimer).</summary>
internal static class VisualTree
{
    /// <summary>The first <typeparamref name="T"/> at or below <paramref name="root"/>, depth first.</summary>
    public static T? Find<T>(DependencyObject? root) where T : DependencyObject =>
        root is null ? null : root as T ?? All<T>(root).FirstOrDefault();

    /// <summary>Every <typeparamref name="T"/> below <paramref name="parent"/>, depth first.</summary>
    public static IEnumerable<T> All<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T hit)
            {
                yield return hit;
            }
            foreach (var found in All<T>(child))
            {
                yield return found;
            }
        }
    }
}
#endif
