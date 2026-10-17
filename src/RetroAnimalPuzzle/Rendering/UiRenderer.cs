using RetroAnimalPuzzle.Core;
using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

public enum UiFont
{
    Heading,
    Playful,
    Digits,
    Body,
}

/// <summary>A menu row: label, optional value shown on the right (for options), and whether it can be chosen.</summary>
internal readonly record struct MenuEntry(string Label, string? Value = null, bool Enabled = true);

/// <summary>
/// Shared drawing helpers for interface elements: outlined text, framed panels, menus with an animated
/// animal cursor and dimming overlays. Fonts are created once per (face, size) and cached.
/// </summary>
internal sealed class UiRenderer : IDisposable
{
    private readonly Fonts _fonts;
    private readonly BlockArt _art;
    private readonly Dictionary<(UiFont, float), SKFont> _fontCache = [];
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round };

    public UiRenderer(Fonts fonts, BlockArt art)
    {
        _fonts = fonts;
        _art = art;
    }

    public BlockArt Art => _art;

    public SKFont Font(UiFont face, float size)
    {
        size = MathF.Round(size * 2f) / 2f;
        if (!_fontCache.TryGetValue((face, size), out var font))
        {
            var typeface = face switch
            {
                UiFont.Heading => _fonts.Heading,
                UiFont.Playful => _fonts.Playful,
                UiFont.Digits => _fonts.Digits,
                _ => _fonts.Body,
            };
            font = new SKFont(typeface, size) { Edging = SKFontEdging.Antialias };
            _fontCache[(face, size)] = font;
        }

        return font;
    }

    public float Measure(string text, float size, UiFont face = UiFont.Playful) => Font(face, size).MeasureText(text);

    public void Text(SKCanvas canvas, string text, float x, float y, float size, SKColor color,
        UiFont face = UiFont.Playful, SKTextAlign align = SKTextAlign.Center)
    {
        _fill.Color = color;
        canvas.DrawText(text, x, y, align, Font(face, size), _fill);
    }

    public void OutlinedText(SKCanvas canvas, string text, float x, float y, float size, SKColor fill, SKColor outline,
        UiFont face = UiFont.Heading, float outlineWidth = 3f, SKTextAlign align = SKTextAlign.Center)
    {
        var font = Font(face, size);
        _stroke.Color = outline;
        _stroke.StrokeWidth = outlineWidth * 2f;
        canvas.DrawText(text, x, y, align, font, _stroke);
        _fill.Color = fill;
        canvas.DrawText(text, x, y, align, font, _fill);
    }

    /// <summary>Title-style text with a vertical two-colour gradient fill.</summary>
    public void GradientText(SKCanvas canvas, string text, float x, float y, float size, SKColor top, SKColor bottom,
        SKColor outline, float outlineWidth = 5f)
    {
        var font = Font(UiFont.Heading, size);
        _stroke.Color = outline;
        _stroke.StrokeWidth = outlineWidth * 2f;
        canvas.DrawText(text, x, y, SKTextAlign.Center, font, _stroke);
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, y - size * 0.8f), new SKPoint(0, y), [top, bottom], SKShaderTileMode.Clamp);
        _fill.Shader = shader;
        _fill.Color = SKColors.White;
        canvas.DrawText(text, x, y, SKTextAlign.Center, font, _fill);
        _fill.Shader = null;
    }

    /// <summary>Gold-framed panel with a translucent dark-green interior.</summary>
    public void Panel(SKCanvas canvas, SKRect rect, byte interiorAlpha = 235)
    {
        _fill.Color = SKColors.Black.WithAlpha(110);
        canvas.DrawRoundRect(new SKRect(rect.Left + 5, rect.Top + 6, rect.Right + 5, rect.Bottom + 6), 12, 12, _fill);
        _fill.Color = Palette.GoldDark;
        canvas.DrawRoundRect(Inflate(rect, 2), 12, 12, _fill);
        _fill.Color = Palette.Gold;
        canvas.DrawRoundRect(rect, 10, 10, _fill);
        _stroke.Color = Palette.GoldLight;
        _stroke.StrokeWidth = 2;
        canvas.DrawRoundRect(Inflate(rect, -3), 8, 8, _stroke);
        _fill.Color = Palette.Field.WithAlpha(interiorAlpha);
        canvas.DrawRoundRect(Inflate(rect, -7), 6, 6, _fill);
    }

    public void Dim(SKCanvas canvas, byte alpha)
    {
        _fill.Color = SKColors.Black.WithAlpha(alpha);
        canvas.DrawRect(0, 0, Viewport.LogicalWidth, Viewport.LogicalHeight, _fill);
    }

    public void Fill(SKCanvas canvas, SKRect rect, SKColor color, float radius = 0)
    {
        _fill.Color = color;
        if (radius > 0)
        {
            canvas.DrawRoundRect(rect, radius, radius, _fill);
        }
        else
        {
            canvas.DrawRect(rect, _fill);
        }
    }

    /// <summary>
    /// Vertical menu centred on <paramref name="centerX"/>. The selected row is enlarged, tinted and marked
    /// with a bobbing monkey on each side. Rows with a value show "Label < value >".
    /// </summary>
    public void Menu(SKCanvas canvas, IReadOnlyList<MenuEntry> items, int selected, float centerX, float top,
        float spacing, double time, float size = 26f)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            bool isSelected = i == selected;
            float y = top + i * spacing;
            float s = isSelected ? size * (1.08f + 0.03f * MathF.Sin((float)time * 6f)) : size;
            var color = !item.Enabled ? new SKColor(0x8A, 0x9A, 0x8A)
                : isSelected ? new SKColor(0xFF, 0xE4, 0x5C) : SKColors.White;
            string label = item.Value is null ? item.Label : $"{item.Label}:  < {item.Value} >";
            OutlinedText(canvas, label, centerX, y, s, color, Palette.Outline, UiFont.Playful, 3f);

            if (isSelected)
            {
                float half = Measure(label, s) / 2f;
                float bob = MathF.Sin((float)time * 8f) * 3f;
                var monkey = Block.Of(AnimalType.Monkey);
                _art.DrawCentered(canvas, monkey, centerX - half - 26, y - s * 0.35f + bob, 30);
                _art.DrawCentered(canvas, monkey, centerX + half + 26, y - s * 0.35f - bob, 30);
            }
        }
    }

    /// <summary>Draws text word-wrapped to <paramref name="maxWidth"/>; returns the y after the last line.</summary>
    public float Paragraph(SKCanvas canvas, string text, float x, float y, float maxWidth, float size, SKColor color,
        UiFont face = UiFont.Body, float lineHeight = 1.3f)
    {
        var font = Font(face, size);
        var line = new System.Text.StringBuilder();
        foreach (var word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureText(candidate) > maxWidth && line.Length > 0)
            {
                Text(canvas, line.ToString(), x, y, size, color, face, SKTextAlign.Left);
                y += size * lineHeight;
                line.Clear().Append(word);
            }
            else
            {
                line.Clear().Append(candidate);
            }
        }

        if (line.Length > 0)
        {
            Text(canvas, line.ToString(), x, y, size, color, face, SKTextAlign.Left);
            y += size * lineHeight;
        }

        return y;
    }

    public void Dispose()
    {
        foreach (var font in _fontCache.Values)
        {
            font.Dispose();
        }

        _fill.Dispose();
        _stroke.Dispose();
    }

    private static SKRect Inflate(SKRect r, float a) => new(r.Left - a, r.Top - a, r.Right + a, r.Bottom + a);
}
