using Windows.Foundation;

namespace Vigil.Controls;

/// <summary>
/// Patient-card grid: as many equal columns as fit at <see cref="MinColumnWidth"/> (at most <see cref="MaxColumns"/>),
/// rows top-aligned and each as tall as its tallest card. <c>UniformGridLayout</c> gives every item the first item's
/// height, which clips a card whose details are open; CSS <c>grid-template-columns: repeat(3, 1fr); align-items: start</c>
/// is this. The column count follows the width it is given (auto-fit), so 1090 px holds 3, 834 px 2 and 655 px 1.
/// </summary>
public sealed partial class CardColumnsLayout : NonVirtualizingLayout
{
    public double MinColumnWidth { get; set; } = 320;

    public int MaxColumns { get; set; } = 3;

    public double ColumnSpacing { get; set; } = 24;

    public double RowSpacing { get; set; } = 24;

    /// <summary>Columns that fit <paramref name="width"/>; pure, so it is unit-tested.</summary>
    public static int ColumnsFor(double width, double minColumnWidth, double spacing, int maxColumns)
    {
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return maxColumns;
        }
        var columns = (int)Math.Floor((width + spacing) / (minColumnWidth + spacing));
        return Math.Clamp(columns, 1, Math.Max(1, maxColumns));
    }

    private (int Columns, double ColumnWidth) Grid(double width)
    {
        var columns = ColumnsFor(width, MinColumnWidth, ColumnSpacing, MaxColumns);
        var columnWidth = double.IsInfinity(width) ? MinColumnWidth : (width - ColumnSpacing * (columns - 1)) / columns;
        return (columns, columnWidth);
    }

    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var (columns, columnWidth) = Grid(availableSize.Width);
        var height = 0d;
        var rowHeight = 0d;
        var children = context.Children;
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(new Size(columnWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            if (i % columns == columns - 1 || i == children.Count - 1)
            {
                height += rowHeight + (i >= columns ? RowSpacing : 0);
                rowHeight = 0;
            }
        }
        var width = double.IsInfinity(availableSize.Width) ? columns * columnWidth + (columns - 1) * ColumnSpacing : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        var (columns, columnWidth) = Grid(finalSize.Width);
        var children = context.Children;
        var y = 0d;
        for (var row = 0; row * columns < children.Count; row++)
        {
            var rowHeight = 0d;
            for (var c = 0; c < columns && row * columns + c < children.Count; c++)
            {
                rowHeight = Math.Max(rowHeight, children[row * columns + c].DesiredSize.Height);
            }
            for (var c = 0; c < columns && row * columns + c < children.Count; c++)
            {
                var child = children[row * columns + c];
                child.Arrange(new Rect(c * (columnWidth + ColumnSpacing), y, columnWidth, child.DesiredSize.Height));
            }
            y += rowHeight + RowSpacing;
        }
        return finalSize;
    }
}
