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
        Loaded += (_, _) => { HomeTab.IsChecked = true; ArrangeAgenda(); };
    }
    private void OnHomeSizeChanged(object sender, SizeChangedEventArgs e) => ArrangeAgenda();
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
