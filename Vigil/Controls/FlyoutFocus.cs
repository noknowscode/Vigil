using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Vigil.Controls;

/// <summary>
/// Gives focus back to where it was when a flyout opened, so a keyboard user keeps their place: the record nav item
/// that opened a drawer has focus again once the drawer closes. Uno's flyout leaves focus on the page when it closes
/// (measured with <c>--vigil-focuslog</c>). Set <c>controls:FlyoutFocus.Restores="True"</c> on the Flyout.
/// </summary>
public static class FlyoutFocus
{
    public static readonly DependencyProperty RestoresProperty = DependencyProperty.RegisterAttached(
        "Restores", typeof(bool), typeof(FlyoutFocus), new PropertyMetadata(false, OnRestoresChanged));

    // The element focused when the flyout opened, and how it got focus (a keyboard focus comes back with its visual).
    private static readonly DependencyProperty OpenerProperty = DependencyProperty.RegisterAttached(
        "Opener", typeof(Control), typeof(FlyoutFocus), new PropertyMetadata(null));

    private static readonly DependencyProperty OpenerStateProperty = DependencyProperty.RegisterAttached(
        "OpenerState", typeof(FocusState), typeof(FlyoutFocus), new PropertyMetadata(FocusState.Programmatic));

    public static bool GetRestores(DependencyObject element) => (bool)element.GetValue(RestoresProperty);

    public static void SetRestores(DependencyObject element, bool value) => element.SetValue(RestoresProperty, value);

    private static void OnRestoresChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FlyoutBase flyout)
        {
            return;
        }
        flyout.Opening -= OnOpening;
        flyout.Closed -= OnClosed;
        if (e.NewValue is true)
        {
            flyout.Opening += OnOpening;
            flyout.Closed += OnClosed;
        }
    }

    private static void OnOpening(object? sender, object e)
    {
        // The flyout has no XamlRoot of its own until it shows; its placement target does.
        if (sender is FlyoutBase flyout && (flyout.XamlRoot ?? flyout.Target?.XamlRoot) is { } root && FocusManager.GetFocusedElement(root) is Control opener)
        {
            flyout.SetValue(OpenerProperty, opener);
            flyout.SetValue(OpenerStateProperty, opener.FocusState);
        }
    }

    private static void OnClosed(object? sender, object e)
    {
        if (sender is FlyoutBase flyout && flyout.GetValue(OpenerProperty) is Control opener)
        {
            var state = (FocusState)flyout.GetValue(OpenerStateProperty) == FocusState.Keyboard ? FocusState.Keyboard : FocusState.Programmatic;
            flyout.ClearValue(OpenerProperty);
            // After the close completes (the popup hands focus to the page as it goes, which would win over an earlier
            // call), and only when no other sheet or drawer has opened since: a flyout closes when focus leaves it.
            opener.DispatcherQueue.TryEnqueue(() =>
            {
                if (opener.XamlRoot is { } root && !VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Any(p => p.Child is not ToolTip))
                {
                    opener.Focus(state);
                }
            });
        }
    }
}
