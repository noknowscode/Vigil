using Windows.Foundation;

namespace Vigil.Controls;

/// <summary>A child positioned on a 0..1 axis: a span (<see cref="Length"/> &gt; 0) or a point centred on <see cref="Start"/>.</summary>
public interface ISpan
{
    /// <summary>Left edge (span) or centre (point), as a fraction of the panel width.</summary>
    double Start { get; }

    /// <summary>Width as a fraction of the panel width; 0 for a point.</summary>
    double Length { get; }

    /// <summary>Pixels taken off a span's right edge (a 2 px gap before the next segment).</summary>
    double Gap { get; }
}

/// <summary>
/// Lays children out along a time (or percentage) axis: the gantt segments, now dots, hour gridlines and axis labels,
/// and the care-time bars. A child is placed from its item (<see cref="ISpan"/> data context, as an ItemsControl hands
/// its containers) or from the attached <see cref="StartProperty"/> / <see cref="LengthProperty"/>. A child that
/// stretches vertically fills the panel height; any other child is centred on it, so a 16 px dot sits on a 12 px track.
/// Pure layout: CSS positions these with <c>left: x%</c> and <c>width: w%</c>, which no stock panel expresses.
/// </summary>
public sealed partial class SpanPanel : Panel
{
    public static readonly DependencyProperty StartProperty = DependencyProperty.RegisterAttached(
        "Start", typeof(double), typeof(SpanPanel), new PropertyMetadata(double.NaN, OnSpanChanged));

    public static readonly DependencyProperty LengthProperty = DependencyProperty.RegisterAttached(
        "Length", typeof(double), typeof(SpanPanel), new PropertyMetadata(0d, OnSpanChanged));

    public static double GetStart(DependencyObject o) => (double)o.GetValue(StartProperty);

    public static void SetStart(DependencyObject o, double value) => o.SetValue(StartProperty, value);

    public static double GetLength(DependencyObject o) => (double)o.GetValue(LengthProperty);

    public static void SetLength(DependencyObject o, double value) => o.SetValue(LengthProperty, value);

    private static void OnSpanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement { Parent: SpanPanel panel })
        {
            panel.InvalidateArrange();
        }
    }

    private static (double Start, double Length, double Gap) SpanOf(UIElement child)
    {
        var start = GetStart(child);
        if (!double.IsNaN(start))
        {
            return (start, GetLength(child), 0);
        }
        var item = (child as FrameworkElement)?.DataContext as ISpan
            ?? ((child as ContentPresenter)?.Content as ISpan);
        return item is null ? (0, 0, 0) : (item.Start, item.Length, item.Gap);
    }

    // An ItemsControl hands its containers their item after they join the panel; a span has no intrinsic width, so
    // its desired size never changes and nothing would re-run the layout. Re-measure when an item arrives.
    private readonly HashSet<FrameworkElement> _watched = [];

    private void Watch(UIElement child)
    {
        if (child is FrameworkElement fe && _watched.Add(fe))
        {
            fe.DataContextChanged += (_, _) => InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var height = 0d;
        foreach (var child in Children)
        {
            Watch(child);
            var (_, length, gap) = SpanOf(child);
            var w = length > 0 ? Math.Max(0, length * width - gap) : double.PositiveInfinity;
            child.Measure(new Size(w, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, double.IsInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var (start, length, gap) = SpanOf(child);
            var stretch = child is FrameworkElement { VerticalAlignment: VerticalAlignment.Stretch } fe && double.IsNaN(fe.Height);
            var h = stretch ? finalSize.Height : child.DesiredSize.Height;
            var y = (finalSize.Height - h) / 2;
            if (length > 0)
            {
                child.Arrange(new Rect(start * finalSize.Width, y, Math.Max(0, length * finalSize.Width - gap), h));
            }
            else
            {
                var w = child.DesiredSize.Width;
                child.Arrange(new Rect(start * finalSize.Width - w / 2, y, w, h));
            }
        }
        return finalSize;
    }
}
