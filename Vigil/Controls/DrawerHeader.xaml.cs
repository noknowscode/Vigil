namespace Vigil.Controls;

/// <summary>Header of a record drawer: its title and Close. Pure dependency properties.</summary>
public sealed partial class DrawerHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DrawerHeader), new PropertyMetadata(""));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand), typeof(ICommand), typeof(DrawerHeader), new PropertyMetadata(null));

    public DrawerHeader()
    {
        this.InitializeComponent();
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public ICommand? CloseCommand { get => (ICommand?)GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
}
