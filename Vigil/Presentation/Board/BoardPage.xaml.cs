using Microsoft.UI.Xaml.Input;
using Windows.System;
namespace Vigil.Presentation;

public sealed partial class BoardPage : Page
{
    private bool _agendaOpen;
    private bool WideAgenda => ActualWidth >= 1120;
    public BoardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { HomeTab.IsChecked = true; ArrangeShell(); };
    }
    private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e) => ArrangeShell();
    private void OnHomeSizeChanged(object sender, SizeChangedEventArgs e) => ArrangeAgenda();
    private void ArrangeShell()
    {
        if (HomeShell is null || TopTabs is null) return;
        var compact = ActualWidth < 760;
        AppFrame.Margin = ActualWidth < 600 ? new Thickness(8) : new Thickness(20, 8, 20, 20);
        Grid.SetColumn(TopTabs, compact ? 0 : 1);
        Grid.SetColumnSpan(TopTabs, compact ? 3 : 1);
        Grid.SetRow(TopTabs, compact ? 1 : 0);
        HomeShell.CornerRadius = new CornerRadius(compact ? 24 : 32);
        ArrangeAgenda();
    }
    private void ArrangeAgenda()
    {
        if (HomeWorkspace is null || AgendaPanel is null) return;
        var wide = WideAgenda;
        HomeWorkspace.ColumnDefinitions[0].Width = new GridLength(wide ? 352 : 0);
        HomeWorkspace.ColumnSpacing = wide ? 20 : 0;
        Grid.SetColumn(AgendaPanel, wide ? 0 : 1);
        AgendaPanel.MaxWidth = wide ? double.PositiveInfinity : 560;
        AgendaPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        AgendaPanel.Visibility = wide || _agendaOpen ? Visibility.Visible : Visibility.Collapsed;
        HomeShell.Visibility = wide || !_agendaOpen ? Visibility.Visible : Visibility.Collapsed;
        AgendaToggle.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        AgendaClose.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
    }
    private void OnOpenAgenda(object sender, RoutedEventArgs e)
    {
        _agendaOpen = true; ArrangeAgenda();
        if (WideAgenda) AgendaScroll.Focus(FocusState.Programmatic);
        else AgendaClose.Focus(FocusState.Programmatic);
    }
    private void OnLiveBandSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid || grid.Children.Count < 2) return;
        var compact = e.NewSize.Width < 540;
        grid.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(grid.Children[1], compact ? 0 : 1);
        Grid.SetRow(grid.Children[1], compact ? 1 : 0);
    }
    private void OnShowPatients(object sender, RoutedEventArgs e)
    {
        _agendaOpen = false; ArrangeAgenda(); HomeTab.IsChecked = true;
    }
    private void OnCloseAgenda(object sender, RoutedEventArgs e) => CloseAgenda();
    private void CloseAgenda()
    {
        _agendaOpen = false; ArrangeAgenda(); AgendaToggle.Focus(FocusState.Programmatic);
    }
    private void OnHomeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && _agendaOpen && !WideAgenda)
        { CloseAgenda(); e.Handled = true; }
    }
}
