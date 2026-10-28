namespace Vigil.Presentation;

public sealed partial class BoardPage : Page
{
    public BoardPage()
    {
        InitializeComponent();
        // Back from a record leaves "Patient care" checked; the board is always "Today's patients".
        Loaded += (_, _) => HomeTab.IsChecked = true;
    }
}
