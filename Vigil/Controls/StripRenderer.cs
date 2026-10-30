using SkiaSharp;

namespace Vigil.Controls;

/// <summary>Pixels for <see cref="VitalsStrip"/>. Paints are created once and reused; Render allocates only paths.</summary>
internal sealed class StripRenderer : IDisposable
{
    private const float Gutter = 40;
    private const float DrugRow = 30;
    private const float TimeRow = 22;
    private const float MinColumn = 52;
    private const float Glyph = 5f;

    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _glyph = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _text = new() { IsAntialias = true };
    private readonly SKFont _font = new() { Size = 12 };
    private readonly SKFont _small = new() { Size = 11 };
    // SkiaSharp 4 obsoletes the SKPath mutators: glyph outlines are built here and detached per draw.
    private readonly SKPathBuilder _path = new();

    public StripData Data { get; set; } = StripData.Empty;

    public StripPalette? Palette { get; set; }

    /// <summary>Alpha of the newest reading's glyphs during its 200 ms entrance.</summary>
    public float NewestAlpha { get; set; } = 1;

    public static SKTypeface? Typeface { get; set; }

    public void Render(SKCanvas canvas, float width, float height)
    {
        if (IsDisposed || Palette is not { } p || width < 120 || height < 120)
        {
            return;
        }
        _font.Typeface = Typeface;
        _small.Typeface = Typeface;

        var d = Data;
        var interval = (float)Math.Max(d.IntervalMinutes, 1);
        var plotLeft = Gutter;
        var plotRight = width - 8;
        var columns = Math.Max(3, (int)((plotRight - plotLeft) / MinColumn));
        var colW = (plotRight - plotLeft) / columns;
        var current = (int)Math.Floor((d.Live ? d.DueMinute - 0.001 : d.NowMinute) / interval);
        var first = Math.Max(0, current - columns + 1);

        // Vertical lanes.
        var top = DrugRow;
        var usable = height - DrugRow - TimeRow;
        var mainTop = top;
        var mainBottom = top + usable * 0.62f;
        var spoTop = mainBottom + 8;
        var spoBottom = spoTop + usable * 0.20f - 8;
        var tempTop = spoBottom + 8;
        var tempBottom = height - TimeRow;

        float X(double minute) => plotLeft + (float)((minute - first * interval) / interval) * colW;
        float Y(double v, double min, double max, float t, float b) => b - (float)((v - min) / (max - min)) * (b - t);

        // Grid: horizontal scale lines with labels.
        _line.Color = p.Grid;
        _text.Color = p.Muted;
        foreach (var v in new[] { 0, 50, 100, 150, 200 })
        {
            var y = Y(v, 0, 220, mainTop, mainBottom);
            canvas.DrawLine(plotLeft, y, plotRight, y, _line);
            canvas.DrawText(v.ToString(), plotLeft - 6, y + 4, SKTextAlign.Right, _small, _text);
        }
        canvas.DrawLine(plotLeft, spoTop, plotRight, spoTop, _line);
        canvas.DrawLine(plotLeft, spoBottom, plotRight, spoBottom, _line);
        canvas.DrawText("SpO₂", plotLeft - 6, (spoTop + spoBottom) / 2 + 4, SKTextAlign.Right, _small, _text);
        canvas.DrawLine(plotLeft, tempBottom, plotRight, tempBottom, _line);
        canvas.DrawText("T°", plotLeft - 6, (tempTop + tempBottom) / 2 + 4, SKTextAlign.Right, _small, _text);

        // Columns with clock labels.
        for (var i = 0; i <= columns; i++)
        {
            var x = plotLeft + i * colW;
            canvas.DrawLine(x, DrugRow - 4, x, tempBottom, _line);
            if (i < columns)
            {
                var label = d.Origin == DateTimeOffset.MinValue ? "" : d.Origin.AddMinutes((first + i) * interval).ToString("HH:mm");
                canvas.DrawText(label, x + 3, height - 6, SKTextAlign.Left, _small, _text);
            }
        }

        // The due slot: the current column, filling like a sight glass.
        if (d.Live && current >= first)
        {
            var x0 = plotLeft + (current - first) * colW;
            var slot = new SKRect(x0 + 2, mainTop, x0 + colW - 2, tempBottom);
            var color = d.Due == DueState.Overdue ? p.Error : p.Primary;
            _fill.Color = color.WithAlpha(0x38);
            var fillTop = slot.Bottom - slot.Height * (float)Math.Clamp(d.DueProgress, 0, 1);
            canvas.DrawRect(new SKRect(slot.Left, fillTop, slot.Right, slot.Bottom), _fill);
            _glyph.Color = color;
            canvas.DrawRoundRect(slot, 3, 3, _glyph);
        }

        // Drug row. Doses within one marker's width share a marker: "Dexm +2" instead of overprinted labels.
        var groups = new List<(float X, List<string> Labels)>();
        foreach (var dose in d.Doses.OrderBy(x => x.Minute))
        {
            if (dose.Minute < first * interval)
            {
                continue;
            }
            var x = X(dose.Minute);
            if (groups.Count > 0 && x - groups[^1].X < 44)
            {
                groups[^1].Labels.Add(dose.Label);
            }
            else
            {
                groups.Add((x, [dose.Label]));
            }
        }
        foreach (var (x, labels) in groups)
        {
            _path.MoveTo(x - 5, 6);
            _path.LineTo(x + 5, 6);
            _path.LineTo(x, 14);
            _path.Close();
            using var tri = _path.Detach();
            _fill.Color = p.Drug;
            canvas.DrawPath(tri, _fill);
            _text.Color = p.Drug;
            var label = labels.Count == 1 ? labels[0] : $"{labels[0]} +{labels.Count - 1}";
            canvas.DrawText(label, x + 7, 15, SKTextAlign.Left, _small, _text);
        }

        // Readings.
        for (var i = 0; i < d.Readings.Count; i++)
        {
            var r = d.Readings[i];
            if (r.Minute < first * interval)
            {
                continue;
            }
            var alpha = i == d.Readings.Count - 1 ? NewestAlpha : 1f;
            var x = X(r.Minute);
            if (r.Sys is int sys)
            {
                Chevron(canvas, x, Clamp(sys, 0, 220, mainTop, mainBottom, out var clipped), down: true, p.Bp, alpha);
                if (clipped) Marker(canvas, x, sys > 220 ? mainTop : mainBottom, sys > 220, p.Bp, alpha);
            }
            if (r.Dia is int dia)
            {
                Chevron(canvas, x, Clamp(dia, 0, 220, mainTop, mainBottom, out _), down: false, p.Bp, alpha);
            }
            if (r.EtCo2 is int co2)
            {
                Cross(canvas, x + 9, Clamp(co2, 0, 220, mainTop, mainBottom, out _), p.EtCo2, alpha);
            }
            if (r.Hr is int hr)
            {
                var y = Clamp(hr, 0, 220, mainTop, mainBottom, out var clipped);
                _fill.Color = p.Hr.WithAlpha((byte)(255 * alpha));
                canvas.DrawCircle(x - 9, y, 4.5f, _fill);
                if (clipped) Marker(canvas, x - 9, hr > 220 ? mainTop : mainBottom, hr > 220, p.Hr, alpha);
            }
            if (r.SpO2 is int spo)
            {
                var y = Clamp(spo, 80, 100, spoTop + 6, spoBottom - 6, out var clipped);
                _glyph.Color = p.SpO2.WithAlpha((byte)(255 * alpha));
                canvas.DrawCircle(x, y, 4.5f, _glyph);
                if (clipped) Marker(canvas, x + 9, spo < 80 ? spoBottom - 2 : spoTop + 2, spo > 100, p.SpO2, alpha);
            }
            if (r.TempC is decimal t)
            {
                var y = Clamp((double)t, 35, 40, tempTop + 6, tempBottom - 6, out _);
                _fill.Color = p.Temp.WithAlpha((byte)(255 * alpha));
                canvas.DrawRect(x - 4, y - 4, 8, 8, _fill);
            }
            if (r.HasAlarm)
            {
                _fill.Color = p.Error.WithAlpha((byte)(255 * alpha));
                canvas.DrawCircle(x, DrugRow + 2, 3, _fill);
            }
        }

        // Legend: colour is never the only cue, so each series is named beside its own glyph (drawn, not a font glyph).
        _text.Color = p.Muted;
        var lx = plotRight;
        const float ly = 20;
        foreach (var (label, kind) in new[] { ("T°", 4), ("SpO₂", 3), ("EtCO₂", 2), (Loc.T("Strip_LegendBp", "BP"), 1), (Loc.T("Strip_LegendHr", "HR"), 0) })
        {
            var w = _small.MeasureText(label);
            canvas.DrawText(label, lx, ly + 4, SKTextAlign.Right, _small, _text);
            var gx = lx - w - 10;
            switch (kind)
            {
                case 0: _fill.Color = p.Hr; canvas.DrawCircle(gx, ly, 4.5f, _fill); break;
                case 1: Chevron(canvas, gx - 12, ly + 3, down: true, p.Bp, 1); Chevron(canvas, gx - 1, ly - 3, down: false, p.Bp, 1); gx -= 12; break;
                case 2: Cross(canvas, gx, ly, p.EtCo2, 1); break;
                case 3: _glyph.Color = p.SpO2; canvas.DrawCircle(gx, ly, 4.5f, _glyph); break;
                default: _fill.Color = p.Temp; canvas.DrawRect(gx - 4, ly - 4, 8, 8, _fill); break;
            }
            lx = gx - 14;
        }
    }

    private static float Clamp(double v, double min, double max, float top, float bottom, out bool clipped)
    {
        clipped = v < min || v > max;
        var c = Math.Clamp(v, min, max);
        return bottom - (float)((c - min) / (max - min)) * (bottom - top);
    }

    private void Chevron(SKCanvas canvas, float x, float y, bool down, SKColor color, float alpha)
    {
        _glyph.Color = color.WithAlpha((byte)(255 * alpha));
        var dy = down ? -Glyph - 1 : Glyph + 1;
        _path.MoveTo(x - Glyph, y + dy);
        _path.LineTo(x, y);
        _path.LineTo(x + Glyph, y + dy);
        using var path = _path.Detach();
        canvas.DrawPath(path, _glyph);
    }

    private void Cross(SKCanvas canvas, float x, float y, SKColor color, float alpha)
    {
        _glyph.Color = color.WithAlpha((byte)(255 * alpha));
        _path.MoveTo(x - 4, y - 4);
        _path.LineTo(x + 4, y + 4);
        _path.MoveTo(x + 4, y - 4);
        _path.LineTo(x - 4, y + 4);
        using var path = _path.Detach();
        canvas.DrawPath(path, _glyph);
    }

    /// <summary>Clamped value: a small triangle at the lane edge pointing the way the value went.</summary>
    private void Marker(SKCanvas canvas, float x, float y, bool up, SKColor color, float alpha)
    {
        _fill.Color = color.WithAlpha((byte)(255 * alpha));
        _path.MoveTo(x - 4, up ? y + 6 : y - 6);
        _path.LineTo(x + 4, up ? y + 6 : y - 6);
        _path.LineTo(x, up ? y - 1 : y + 1);
        _path.Close();
        using var tri = _path.Detach();
        canvas.DrawPath(tri, _fill);
    }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
        _line.Dispose();
        _glyph.Dispose();
        _fill.Dispose();
        _text.Dispose();
        _font.Dispose();
        _small.Dispose();
        _path.Dispose();
    }
}
