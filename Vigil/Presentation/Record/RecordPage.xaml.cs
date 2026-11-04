using Microsoft.UI.Xaml.Input;
namespace Vigil.Presentation;
public sealed partial class RecordPage : Page
{
    private Control? _paneTrigger;
    public RecordPage()
    {
        InitializeComponent();
    }
    // xaml-lint: allow codebehind - SplitView.OpenPaneLength is a single number and the pane must be min(640, workspace width): a continuous clamp to the workspace (window minus sidebar), not a breakpoint {utu:Responsive} can express. The !Route drawer that would replace the SplitView is under Remaining in docs/UNO-AUDIT.md.
    private void OnWorkspaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is SplitView split) split.OpenPaneLength = Math.Min(640, e.NewSize.Width);
    }
    private void OnPaneOpening(SplitView sender, object args) => _paneTrigger = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as Control : null;
    private void OnPaneOpened(SplitView sender, object args)
    {
        if (sender.Pane is FrameworkElement pane && pane.FindName("DrawerClose") is Control close) close.Focus(FocusState.Programmatic);
    }
    private void OnPaneClosed(SplitView sender, object args)
    {
        _paneTrigger?.Focus(FocusState.Programmatic);
        _paneTrigger = null;
    }
}
