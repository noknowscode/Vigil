using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Microsoft.UI.Dispatching;
using Windows.UI;

namespace Vigil.Controls;

/// <summary>
/// The five-minute strip in the paper record's notation: heart rate dots, systolic ∨ / diastolic ∧ chevrons,
/// SpO2 rings in their own lane, EtCO2 crosses, temperature squares, a drug row on top, and the current column
/// drawn as a yellow slot that fills like a vaporizer sight glass as the next reading comes due.
/// Redraws only when <see cref="Data"/> or the theme changes (no frame loop).
/// </summary>
// xaml-lint: allow codebehind - Skia invalidation and theme snapshot have no XAML surface
public sealed partial class VitalsStrip : SKCanvasElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(StripData), typeof(VitalsStrip), new PropertyMetadata(StripData.Empty, (d, _) => ((VitalsStrip)d).OnDataChanged()));

    public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register(
        nameof(ReduceMotion), typeof(bool), typeof(VitalsStrip), new PropertyMetadata(false));

    private StripRenderer _renderer = new();
    private DispatcherQueueTimer? _fadeTimer;
    private int _lastCount = -1;

    public VitalsStrip()
    {
        Loaded += async (_, _) =>
        {
            // Re-entering the visual tree (navigation back) after Unloaded disposed the renderer.
            if (_renderer.IsDisposed)
            {
                _renderer = new StripRenderer { Data = Data ?? StripData.Empty };
            }
            SnapshotPalette();
            Invalidate();
            await LoadTypefaceAsync();
            Invalidate();
        };
        ActualThemeChanged += (_, _) => { SnapshotPalette(); Invalidate(); };
        Unloaded += (_, _) =>
        {
            // Native paints and fonts are released with the page, not left to the finalizer queue.
            _fadeTimer?.Stop();
            _renderer.Dispose();
        };
    }

    public StripData Data
    {
        get => (StripData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public bool ReduceMotion
    {
        get => (bool)GetValue(ReduceMotionProperty);
        set => SetValue(ReduceMotionProperty, value);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area) =>
        _renderer.Render(canvas, (float)area.Width, (float)area.Height);

    private void OnDataChanged()
    {
        var data = Data ?? StripData.Empty;
        // A new reading fades its column in over 200 ms (EaseOut), drawn final under reduced motion.
        if (_lastCount >= 0 && data.Readings.Count > _lastCount && !ReduceMotion)
        {
            StartFade();
        }
        _lastCount = data.Readings.Count;
        _renderer.Data = data;
        Invalidate();
    }

    private void StartFade()
    {
        var started = Environment.TickCount64;
        _renderer.NewestAlpha = 0;
        _fadeTimer ??= DispatcherQueue.CreateTimer();
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(16);
        _fadeTimer.IsRepeating = true;
        _fadeTimer.Tick += Step;
        _fadeTimer.Start();

        void Step(DispatcherQueueTimer t, object _)
        {
            var p = Math.Clamp((Environment.TickCount64 - started) / 200.0, 0, 1);
            _renderer.NewestAlpha = (float)(1 - Math.Pow(1 - p, 3));
            Invalidate();
            if (p >= 1)
            {
                t.Stop();
                t.Tick -= Step;
                _renderer.NewestAlpha = 1;
            }
        }
    }

    private void SnapshotPalette()
    {
        var theme = ActualTheme;
        SKColor C(string key) => ToSk(ThemePalette.Resolve(key, theme, Microsoft.UI.Colors.Gray));
        _renderer.Palette = new StripPalette(
            Ink: C("OnSurfaceColor"),
            Muted: C("OnSurfaceVariantColor"),
            Grid: C("OutlineColor"),
            Surface: C("SurfaceColor"),
            Primary: C("PrimaryColor"),
            Error: C("ErrorColor"),
            Drug: C("SecondaryColor"),
            Hr: C("VitalHrColor"),
            Bp: C("VitalBpColor"),
            SpO2: C("VitalSpO2Color"),
            EtCo2: C("VitalEtCo2Color"),
            Temp: C("VitalTempColor"));
    }

    private static SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);

    /// <summary>Plex Mono for clock labels, read once from the package (underscore file name: Android renames hyphens).</summary>
    private static async Task LoadTypefaceAsync()
    {
        if (StripRenderer.Typeface is not null)
        {
            return;
        }
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/MartianMono_Medium.ttf"));
            await using var stream = await file.OpenStreamForReadAsync();
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            StripRenderer.Typeface = SKTypeface.FromStream(buffer);
        }
        catch (Exception)
        {
            // Labels fall back to the default typeface; the strip still draws.
        }
    }
}

internal sealed record StripPalette(
    SKColor Ink, SKColor Muted, SKColor Grid, SKColor Surface, SKColor Primary, SKColor Error, SKColor Drug,
    SKColor Hr, SKColor Bp, SKColor SpO2, SKColor EtCo2, SKColor Temp);
