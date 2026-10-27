using ContentWatch.Core;
using SkiaSharp;

namespace ContentWatch.Rendering;

public sealed record OverlayFrame(long Sequence, byte[] Png, byte[] Bgra, TimerSnapshot Timer);

/// <summary>Only time digits are rendered. UI rings and controls never enter OBS.</summary>
public sealed class OverlayRenderer : IDisposable
{
    public const int Width = 1280, Height = 360;
    private readonly SKBitmap bitmap = new(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
    private long sequence;

    public OverlayFrame Render(TimerSnapshot timer, Appearance style)
    {
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var face = SKTypeface.FromFamilyName(style.Font, SKFontStyle.Normal);
        using var paint = new SKPaint { Typeface = face, TextSize = style.FontSize, IsAntialias = true, Color = SKColor.Parse(style.Foreground) };
        var text = TimerDisplay.Text(timer, style);
        var cell = Enumerable.Range(0, 10).Max(n => paint.MeasureText(n.ToString()));
        var width = text.Sum(ch => char.IsDigit(ch) ? cell : paint.MeasureText(ch.ToString()));
        if (width > Width - 100)
        {
            var scale = (Width - 100) / width;
            paint.TextSize *= scale;
            cell *= scale;
            width = Width - 100;
        }
        float x = (Width - width) / 2;
        var baseline = Height / 2f - (paint.FontMetrics.Ascent + paint.FontMetrics.Descent) / 2;
        using var blur = SKImageFilter.CreateBlur(4, 4);
        foreach (var ch in text)
        {
            var measured = paint.MeasureText(ch.ToString());
            var advance = char.IsDigit(ch) ? cell : measured;
            var dx = x + (advance - measured) / 2;
            if (style.Shadow)
            {
                using var shadow = paint.Clone();
                shadow.Color = SKColors.Black.WithAlpha(150);
                shadow.ImageFilter = blur;
                canvas.DrawText(ch.ToString(), dx + 3, baseline + 5, shadow);
            }
            if (style.OutlineWidth > 0)
            {
                using var outline = paint.Clone();
                outline.Color = SKColor.Parse(style.Outline);
                outline.Style = SKPaintStyle.Stroke;
                outline.StrokeWidth = style.OutlineWidth * 2;
                outline.StrokeJoin = SKStrokeJoin.Round;
                canvas.DrawText(ch.ToString(), dx, baseline, outline);
            }
            canvas.DrawText(ch.ToString(), dx, baseline, paint);
            x += advance;
        }
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new(++sequence, data.ToArray(), bitmap.Bytes, timer);
    }
    public void Dispose() => bitmap.Dispose();
}
