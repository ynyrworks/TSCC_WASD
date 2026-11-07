using System.Windows;
using System.Windows.Media;

namespace TSCC_WASD.App;

/// <summary>Finds the elements a tour step points at and works out where its card goes.</summary>
public static class TourAnchors
{
    /// <summary>The step's target elements (those that exist). The page may be null while it is not created yet.</summary>
    public static IReadOnlyList<FrameworkElement> Find(TourStep step, FrameworkElement window, FrameworkElement? page)
    {
        var scope = step.InWindow ? window : page;
        if (scope is null) return [];
        return step.Targets.Select(scope.FindName).OfType<FrameworkElement>().ToList();
    }

    /// <summary>Laid out and not hidden anywhere up the tree.</summary>
    public static bool IsShown(FrameworkElement element)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>Bounds of all shown elements in <paramref name="relativeTo"/> coordinates, or null when none is shown.</summary>
    public static Rect? Union(IEnumerable<FrameworkElement> elements, Visual relativeTo)
    {
        Rect? union = null;
        foreach (var element in elements.Where(IsShown))
        {
            try
            {
                var bounds = element.TransformToVisual(relativeTo).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                union = union is { } u ? Rect.Union(u, bounds) : bounds;
            }
            catch (InvalidOperationException) { } // not in the same visual tree (yet)
        }
        return union;
    }

    /// <summary>Top-left of the card: below the target, else above, else to the right, else to the left, else inside the area.</summary>
    public static Point CardPosition(Rect target, Size card, Size area)
    {
        const double gap = 14, margin = 8;
        double X(double wanted) => Math.Max(margin, Math.Min(wanted, area.Width - card.Width - margin));
        double Y(double wanted) => Math.Max(margin, Math.Min(wanted, area.Height - card.Height - margin));
        if (target.Bottom + gap + card.Height + margin <= area.Height) return new Point(X(target.Left), target.Bottom + gap);
        if (target.Top - gap - card.Height >= margin) return new Point(X(target.Left), target.Top - gap - card.Height);
        if (target.Right + gap + card.Width + margin <= area.Width) return new Point(target.Right + gap, Y(target.Top));
        if (target.Left - gap - card.Width >= margin) return new Point(target.Left - gap - card.Width, Y(target.Top));
        return new Point(X(area.Width), Y(area.Height));
    }

    /// <summary>Top-left that centres the card in the area (steps without a target).</summary>
    public static Point Centered(Size card, Size area) =>
        new(Math.Max(0, (area.Width - card.Width) / 2), Math.Max(0, (area.Height - card.Height) / 2));
}
