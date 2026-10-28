using Microsoft.UI.Xaml.Input;
using Windows.System;
namespace Vigil.Presentation;
public sealed partial class RecordPage : Page
{
    private Control? _paneTrigger;
    public RecordPage()
    {
        InitializeComponent();
        Loaded += (_, _) => DispatcherQueue.TryEnqueue(() => RecordTabs.SelectedItem = CareTab);
    }
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
    private void OnWorkspaceKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && sender is SplitView { IsPaneOpen: true } split)
        {
            split.IsPaneOpen = false;
            e.Handled = true;
        }
    }
}
