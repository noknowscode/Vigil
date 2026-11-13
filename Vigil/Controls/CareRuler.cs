using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Shapes;

namespace Vigil.Controls;

/// <summary>Native, focusable care intervals over a continuous hourly ruler.</summary>
public sealed partial class CareRuler : UserControl
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(CareDay), typeof(CareRuler), new PropertyMetadata(null, (d, _) => ((CareRuler)d).Draw()));
    public CareDay? Data { get => (CareDay?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    private readonly Canvas _surface = new();
    private readonly Canvas _ticks = new() { IsHitTestVisible = false };
    private readonly Canvas _blocks = new();
    private readonly Canvas _present = new() { IsHitTestVisible = false };
    private readonly Dictionary<string, Button> _buttons = new();
    private bool _drawing;

    public CareRuler()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _surface.Children.Add(_ticks); _surface.Children.Add(_blocks); _surface.Children.Add(_present);
        Content = _surface;
        Loaded += (_, _) => Draw();
        SizeChanged += (_, _) => Draw();
        ActualThemeChanged += (_, _) => Draw();
    }

    // Every key exists in both theme dictionaries (Material roles in Styles/CloverPalette.xaml, ruler inks in Tokens.xaml).
    private SolidColorBrush Ink(string key) => new(ThemePalette.Resolve(key, ActualTheme, Microsoft.UI.Colors.Transparent));
    private static void Place(Canvas host, FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x); Canvas.SetTop(element, y); host.Children.Add(element);
    }
    /// <summary>A label in one of the app's type styles (Themes/Tokens.xaml: fonts, sizes, line heights).</summary>
    private static TextBlock Label(string text, string style, Brush ink) => new()
    { Text = text, Style = (Style)Application.Current.Resources[style], Foreground = ink };

    private void Draw()
    {
        if (_drawing || Data is not { } day) return;
        _drawing = true;
        try
        {
            var slots = day.Layout();
            var lanes = slots.Count > 0 ? slots.Max(s => s.Lane) + 1 : 1;
            MinWidth = 52 + lanes * 104;
            var width = Math.Max(ActualWidth > 0 ? ActualWidth : 290, MinWidth);
            var height = Math.Max(day.Height + 44, slots.Count > 0 ? slots.Max(s => s.Top + s.Height) + 34 : 160);
            Height = height; _surface.Width = width; _surface.Height = height;
            _ticks.Children.Clear(); _present.Children.Clear();
            // Color keys, not *Brush: only the colours sit in theme dictionaries, so a brush key resolves to the Light value under Theatre.
            var text = Ink("OnSurfaceColor");
            var quiet = Ink("OnSurfaceVariantColor");
            var line = Ink("OutlineColor");
            var primary = Ink("PrimaryColor");
            Place(_ticks, new Border { Width = 1, Height = day.Height, Background = line }, 43, 22);
            for (var q = 0; q <= (day.End - day.Start).TotalMinutes / 15; q++)
            {
                var y = 22 + q * CareDay.PixelsPerHour / 4;
                var hour = q % 4 == 0;
                Place(_ticks, new Border { Width = hour ? 10 : 5, Height = 1, Background = line }, hour ? 34 : 39, y);
                // An hour label the "Now" pill would cover is left out: the label spans y-17..y-1, the pill (22 px) now-10..now+12, plus 4 px air.
                var now = day.NowTop + 22;
                if (hour && (y <= now - 13 || y >= now + 33))
                {
                    Place(_ticks, Label(Presentation.Today.Time(day.Start.AddMinutes(q * 15), day.Now), "TypeMonoLabel", quiet), 0, y - 17);
                }
                if (hour)
                {
                    Place(_ticks, new Border { Width = width - 52, Height = 1, Background = line, Opacity = .45 }, 52, y);
                }
            }
            var keys = slots.Select(s => s.Span.Key).ToHashSet();
            foreach (var key in _buttons.Keys.Where(k => !keys.Contains(k)).ToArray())
            { _blocks.Children.Remove(_buttons[key]); _buttons.Remove(key); }
            var laneWidth = (width - 52) / lanes;
            foreach (var slot in slots)
            {
                var span = slot.Span;
                if (!_buttons.TryGetValue(span.Key, out var button))
                {
                    button = new Button { Style = (Style)Application.Current.Resources["CloverTimeBlock"], HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
                    Uno.Extensions.Navigation.UI.Navigation.SetRequest(button, "Record");
                    Uno.Extensions.Navigation.UI.Navigation.SetData(button, new CaseRef(span.CaseId));
                    _buttons[span.Key] = button; _blocks.Children.Add(button);
                }
                button.Width = laneWidth - 5; button.Height = slot.Height;
                Canvas.SetLeft(button, 52 + laneWidth * slot.Lane); Canvas.SetTop(button, slot.Top + 24);
                button.Background = Ink(span.Phase == CarePhaseKind.Anesthesia ? "CloverMint" : "CloverLavender");
                button.BorderBrush = Ink(span.Phase == CarePhaseKind.Anesthesia ? "CloverMintEdge" : "CloverLavenderEdge");
                var content = new StackPanel { Spacing = 4 };
                var name = Label(span.Name, "TypeTitleS", text);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                content.Children.Add(name);
                // Short blocks fit the name only; the times stay in the tooltip and the accessible name.
                if (slot.Height >= 76) content.Children.Add(Label(span.PhaseLabel, "TypeCaption", quiet));
                if (slot.Height >= 56)
                {
                    var time = Label(span.Times, "TypeCaption", quiet); time.TextTrimming = TextTrimming.CharacterEllipsis;
                    content.Children.Add(time);
                }
                button.Content = content;
                AutomationProperties.SetName(button, span.AccessibleName);
                ToolTipService.SetToolTip(button, span.AccessibleName);
            }
            if (day.IsEmpty)
            {
                var empty = Label(Loc.T("Ruler_Empty", "Care intervals appear after anaesthesia begins."), "TypeBodyS", quiet);
                empty.TextWrapping = TextWrapping.Wrap; empty.Width = width - 72;
                Place(_present, empty, 64, 36);
            }
            var nowY = day.NowTop + 22;
            Place(_present, new Border { Width = width - 43, Height = 1.5, Background = primary }, 43, nowY);
            Place(_present, new Ellipse { Width = 6, Height = 6, Fill = primary }, 40, nowY - 2.5);
            var marker = new Border { Background = primary, CornerRadius = (CornerRadius)Application.Current.Resources["Radius6"], Padding = new Thickness(5, 3, 5, 3), Child = Label(Loc.T("Ruler_Now", "Now"), "TypeCaption", Ink("OnPrimaryColor")) };
            Place(_present, marker, 0, nowY - 10);
        }
        finally { _drawing = false; }
    }
}
