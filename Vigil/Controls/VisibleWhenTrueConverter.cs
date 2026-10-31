using Microsoft.UI.Xaml.Data;

namespace Vigil.Controls;

/// <summary>
/// bool → Visible/Collapsed for <c>{Binding}</c>. Uno converts a bool to Visibility implicitly; WinUI does that only for
/// <c>x:Bind</c>, so without this every bool-bound Visibility would stay Visible on the WinAppSDK head.
/// A value that is already a Visibility passes through.
/// </summary>
public sealed class VisibleWhenTrueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        bool shown => shown ? Visibility.Visible : Visibility.Collapsed,
        Visibility visibility => visibility,
        _ => Visibility.Collapsed,
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is Visibility.Visible;
}
