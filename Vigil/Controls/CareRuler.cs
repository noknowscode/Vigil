using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

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
    private readonly FontFamily _font = new("ms-appx:///Assets/Fonts/HankenGrotesk.ttf");
    private readonly FontFamily _mono = new("ms-appx:///Assets/Fonts/MartianMono.ttf");
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

    private SolidColorBrush Ink(string key, string fallback) => new(ThemePalette.Resolve(key, ActualTheme, Hex(fallback)));
    private static Color Hex(string value) => Color.FromArgb(255, Convert.ToByte(value.Substring(1, 2), 16), Convert.ToByte(value.Substring(3, 2), 16), Convert.ToByte(value.Substring(5, 2), 16));
    private static void Place(Canvas host, FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x); Canvas.SetTop(element, y); host.Children.Add(element);
    }
    private TextBlock Label(string text, double size, Brush ink, bool mono = false) => new()
    { Text = text, FontSize = size, Foreground = ink, FontFamily = mono ? _mono : _font, TextWrapping = TextWrapping.NoWrap };

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
            var text = Ink("OnSurfaceColor", "#203B2B");
            var quiet = Ink("OnSurfaceVariantColor", "#5E6B61");
            var line = Ink("OutlineColor", "#D9DED2");
            var primary = Ink("PrimaryColor", "#244936");
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
                    Place(_ticks, Label(day.Start.AddMinutes(q * 15).ToString("HH:mm"), 12, quiet, true), 0, y - 17);
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
                button.Background = Ink(span.Phase == CarePhase.Anaesthesia ? "CloverMint" : "CloverLavender", span.Phase == CarePhase.Anaesthesia ? "#D5ECE0" : "#E5E8F9");
                button.BorderBrush = Ink(span.Phase == CarePhase.Anaesthesia ? "CloverMintEdge" : "CloverLavenderEdge", "#E4E4E6");
                var content = new StackPanel { Spacing = 4 };
                var name = Label(span.Name, 15, text);
                name.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                content.Children.Add(name);
                // Short blocks fit the name only; the times stay in the tooltip and the accessible name.
                if (slot.Height >= 76) content.Children.Add(Label(span.PhaseLabel, 12, quiet));
                if (slot.Height >= 56)
                {
                    var time = Label(span.Times, 12, quiet); time.TextTrimming = TextTrimming.CharacterEllipsis;
                    content.Children.Add(time);
                }
                button.Content = content;
                AutomationProperties.SetName(button, span.AccessibleName);
                ToolTipService.SetToolTip(button, span.AccessibleName);
            }
            if (day.IsEmpty)
            {
                var empty = Label(Loc.T("Ruler_Empty", "Care intervals appear after anaesthesia begins."), 13, quiet);
                empty.TextWrapping = TextWrapping.Wrap; empty.Width = width - 72;
                Place(_present, empty, 64, 36);
            }
            var nowY = day.NowTop + 22;
            Place(_present, new Border { Width = width - 43, Height = 1.5, Background = primary }, 43, nowY);
            Place(_present, new Ellipse { Width = 6, Height = 6, Fill = primary }, 40, nowY - 2.5);
            var marker = new Border { Background = primary, CornerRadius = new CornerRadius(5), Padding = new Thickness(5, 3, 5, 3), Child = Label(Loc.T("Ruler_Now", "Now"), 12, Ink("OnPrimaryColor", "#FFFEF9")) };
            Place(_present, marker, 0, nowY - 10);
        }
        finally { _drawing = false; }
    }
}
