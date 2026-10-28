using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Microsoft.UI.Dispatching;

namespace Vigil.Controls;

/// <summary>Decorative photography only. Text, measurements, buttons and alerts are separate XAML siblings.</summary>
public sealed partial class PetBackdrop : SKCanvasElement
{
    public static readonly DependencyProperty PhotoProperty = DependencyProperty.Register(nameof(Photo), typeof(string), typeof(PetBackdrop), new PropertyMetadata(null, (d, _) => ((PetBackdrop)d).LoadPhoto()));
    public static readonly DependencyProperty PulseProperty = DependencyProperty.Register(nameof(Pulse), typeof(long), typeof(PetBackdrop), new PropertyMetadata(0L, (d, _) => ((PetBackdrop)d).StartPulse()));
    public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register(nameof(ReduceMotion), typeof(bool), typeof(PetBackdrop), new PropertyMetadata(false, (d, _) => ((PetBackdrop)d).StartPulse()));
    public static readonly DependencyProperty HeroProperty = DependencyProperty.Register(nameof(Hero), typeof(bool), typeof(PetBackdrop), new PropertyMetadata(false, (d, _) => { var b = (PetBackdrop)d; b.ResolveInks(); b.Invalidate(); }));
    private SKBitmap? _bitmap;
    private DispatcherQueueTimer? _timer;
    private int _loadVersion;
    private bool _loaded;
    private float _blur;
    private SKColor _base, _blobA, _blobB;
    public string? Photo { get => (string?)GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }
    public long Pulse { get => (long)GetValue(PulseProperty); set => SetValue(PulseProperty, value); }
    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    public bool Hero { get => (bool)GetValue(HeroProperty); set => SetValue(HeroProperty, value); }
    public PetBackdrop()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { _loaded = true; ResolveInks(); LoadPhoto(); StartPulse(); };
        ActualThemeChanged += (_, _) => { ResolveInks(); Invalidate(); };
        Unloaded += (_, _) => { _loaded = false; ++_loadVersion; _timer?.Stop(); _bitmap?.Dispose(); _bitmap = null; _blur = 0; };
    }
    // The hero keeps its light panel in both themes: its text inks are fixed dark colours over the photo.
    private void ResolveInks()
    {
        var theme = Hero ? ElementTheme.Light : ActualTheme;
        _base = Ink("CloverPhotoBase", theme);
        _blobA = Ink("CloverPhotoBlobA", theme);
        _blobB = Ink("CloverPhotoBlobB", theme);
    }
    private static SKColor Ink(string key, ElementTheme theme)
    {
        var c = ThemePalette.Resolve(key, theme, Microsoft.UI.Colors.Gray);
        return new SKColor(c.R, c.G, c.B, c.A);
    }
    private async void LoadPhoto()
    {
        if (!_loaded) return;
        var version = ++_loadVersion;
        _bitmap?.Dispose(); _bitmap = null; Invalidate();
        if (string.IsNullOrEmpty(Photo)) return;
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(Photo));
            using var stream = await file.OpenStreamForReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bitmap = SKBitmap.Decode(memory.ToArray());
            if (!_loaded || version != _loadVersion) { bitmap?.Dispose(); return; }
            _bitmap = bitmap;
            Invalidate();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        { System.Diagnostics.Debug.WriteLine($"Clover photo unavailable: {ex.Message}"); }
    }
    private void StartPulse()
    {
        _timer?.Stop(); _blur = 0;
        if (!_loaded || ReduceMotion || Pulse <= 0 || Environment.TickCount64 - Pulse >= 10_000) { Invalidate(); return; }
        if (_timer is null)
        {
            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += (_, _) => Tick();
        }
        _timer.Start(); Tick();
    }
    private void Tick()
    {
        var progress = Math.Clamp((Environment.TickCount64 - Pulse) / 10_000d, 0, 1);
        // Visible, unhurried blur-in / hold / return. No link to induction or clinical state.
        _blur = (float)(24 * Math.Pow(Math.Sin(progress * Math.PI), 0.8));
        if (progress >= 1 || ReduceMotion) { _blur = 0; _timer?.Stop(); }
        Invalidate();
    }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var w = (float)area.Width; var h = (float)area.Height;
        canvas.Clear(_base);
        using var paint = new SKPaint { IsAntialias = true };
        using var filter = _blur > .1f ? SKImageFilter.CreateBlur(_blur, _blur, SKShaderTileMode.Clamp) : null;
        paint.ImageFilter = filter;
        if (_bitmap is not null)
        {
            if (Hero && w / h > (float)_bitmap.Width / _bitmap.Height)
            {
                // Extend the outdoor atmosphere at wide ratios; keep the complete animal in frame.
                using var ambient = new SKPaint { IsAntialias = true };
                using var ambientBlur = SKImageFilter.CreateBlur(28, 28, SKShaderTileMode.Clamp);
                ambient.ImageFilter = ambientBlur;
                var cover = Math.Max(w / _bitmap.Width, h / _bitmap.Height);
                var coverW = _bitmap.Width * cover; var coverH = _bitmap.Height * cover;
                canvas.DrawBitmap(_bitmap, new SKRect((w-coverW)/2, (h-coverH)/2, (w+coverW)/2, (h+coverH)/2), ambient);
            }
            var scale = (Hero ? h / _bitmap.Height : Math.Max(w / _bitmap.Width, h / _bitmap.Height)) * (1 + .04f * _blur / 24);
            var bw = _bitmap.Width * scale; var bh = _bitmap.Height * scale;
            var imageRect = new SKRect((w-bw)/2, (h-bh)/2, (w+bw)/2, (h+bh)/2);
            if (Hero && bw < w)
            {
                // Feather the photo into the extended landscape instead of exposing a hard edge.
                canvas.SaveLayer(new SKRect(0, 0, w, h), null);
                canvas.DrawBitmap(_bitmap, imageRect, paint);
                using var edgeShader = SKShader.CreateLinearGradient(new SKPoint(imageRect.Left, 0), new SKPoint(imageRect.Right, 0),
                    [SKColors.Transparent, SKColors.White, SKColors.White, SKColors.Transparent], [0, .07f, .93f, 1], SKShaderTileMode.Clamp);
                using var mask = new SKPaint { Shader = edgeShader, BlendMode = SKBlendMode.DstIn };
                canvas.DrawRect(new SKRect(0, 0, w, h), mask);
                canvas.Restore();
            }
            else canvas.DrawBitmap(_bitmap, imageRect, paint);
        }
        else
        {
            paint.Color = _blobA;
            canvas.DrawOval(new SKRect(-w*.18f,h*.27f,w*.7f,h*1.3f),paint);
            paint.Color = _blobB;
            canvas.DrawOval(new SKRect(w*.55f,-h*.3f,w*1.3f,h*.6f),paint);
        }
        if (Hero)
        {
            paint.ImageFilter = null;
            using var shade = SKShader.CreateLinearGradient(new SKPoint(0,0),new SKPoint(w,0),[new SKColor(248,245,237,235),new SKColor(248,245,237,0),new SKColor(248,245,237,30)],[0,.43f,1],SKShaderTileMode.Clamp);
            paint.Shader = shade;
            canvas.DrawRect(new SKRect(0,0,w,h),paint);
        }
    }
}
