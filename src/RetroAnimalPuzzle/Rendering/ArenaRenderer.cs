using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>Values shown by the arena chrome (everything except the blocks themselves).</summary>
/// <param name="HeartLeft">Number shown in the left heart: food eaten (counted when the animal reaches the heart).</param>
/// <param name="PulseLeft">0..1 pulse of the left heart after an animal arrives.</param>
internal readonly record struct ArenaHud(string StageText, int PlayerOneScore, int PlayerTwoScore,
    int HeartLeft, int HeartRight, int PlayerOneWins, int PlayerTwoWins, TimeSpan Elapsed, bool Versus,
    string PlayerTwoTag = "2", float PulseLeft = 0f, float PulseRight = 0f, int WinsToMatch = 2);

/// <summary>
/// Draws the static arena: embossed background, the two playfield wells, NEXT bars, hearts,
/// the central gold panels with stage, scores and timer. All paints and paths are cached.
/// </summary>
internal sealed class ArenaRenderer : IDisposable
{
    private readonly EmbossedPattern _background = new(
        Palette.Background, Palette.BackgroundRelief, Palette.BackgroundShadow, Palette.BackgroundHighlight);
    private readonly EmbossedPattern _field = new(
        Palette.Field, Palette.FieldRelief, Palette.FieldShadow, Palette.FieldHighlight);

    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _text = new() { IsAntialias = true };
    private readonly SKPaint _textOutline = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round };

    private readonly SKFont _labelFont;
    private readonly SKFont _stageFont;
    private readonly SKFont _scoreFont;
    private readonly SKFont _heartFont;
    private readonly SKFont _timerFont;
    private readonly SKFont _tagFont;
    private readonly SKFont _smallTagFont;

    private SKImage? _backgroundCache;
    private (float Scale, int Variant) _backgroundKey;
    private SKImage? _arenaCache;
    private (float Scale, int Variant) _arenaKey;

    private readonly SKPath _heart = CreateHeart();
    private readonly SKPath _playerOneTab = CreateScoreTab(ArenaLayout.PlayerOneScore, onLeft: true);
    private readonly SKPath _playerTwoTab = CreateScoreTab(ArenaLayout.PlayerTwoScore, onLeft: false);

    public ArenaRenderer(Fonts fonts)
    {
        _labelFont = new SKFont(fonts.Heading, 20f);
        _stageFont = new SKFont(fonts.Heading, 18f);
        _scoreFont = new SKFont(fonts.Digits, 30f);
        _heartFont = new SKFont(fonts.Digits, 28f);
        _timerFont = new SKFont(fonts.Digits, 22f);
        _tagFont = new SKFont(fonts.Heading, 13f);
        _smallTagFont = new SKFont(fonts.Heading, 9f);
    }

    /// <summary>Full-screen embossed background (menus). Served from a per-resolution cache.</summary>
    public void DrawBackground(SKCanvas canvas)
    {
        var image = Cached(canvas, ref _backgroundCache, ref _backgroundKey, 0,
            c => _background.Fill(c, new SKRect(0, 0, 800, 600)));
        canvas.DrawImage(image, new SKRect(0, 0, 800, 600), SKSamplingOptions.Default);
    }

    /// <summary>The mascot frame interior, where the caller draws the mascot.</summary>
    public static SKRect MascotArea => Inset(ArenaLayout.MascotFrame, 6f);

    /// <summary>
    /// Background plus every static arena element, then the changing values on top. The static layer is
    /// rasterised once at the window's pixel scale (re-rendered only on resize or mode change), because
    /// re-sampling the embossed patterns every frame costs more than the whole rest of the frame.
    /// </summary>
    public void DrawArena(SKCanvas canvas, in ArenaHud hud)
    {
        bool versus = hud.Versus;
        string tag = hud.PlayerTwoTag;
        var image = Cached(canvas, ref _arenaCache, ref _arenaKey, versus ? 2 : 1, c => DrawArenaStatic(c, versus, tag));
        canvas.DrawImage(image, new SKRect(0, 0, 800, 600), SKSamplingOptions.Default);

        DrawHeartDigits(canvas, ArenaLayout.LeftHeart, hud.HeartLeft, Palette.PlayerOneDigits, hud.PulseLeft);
        if (versus)
        {
            DrawHeartDigits(canvas, ArenaLayout.RightHeart, hud.HeartRight, Palette.PlayerTwoDigits, hud.PulseRight);
            DrawWinStars(canvas, ArenaLayout.LeftWins, hud.PlayerOneWins, hud.WinsToMatch);
            DrawWinStars(canvas, ArenaLayout.RightWins, hud.PlayerTwoWins, hud.WinsToMatch);
        }

        DrawOutlinedText(canvas, hud.StageText, ArenaLayout.StagePlate.MidX, ArenaLayout.StagePlate.MidY + 7f,
            _stageFont, Palette.GoldLight, Palette.GoldDark, 3f);
        DrawScoreDigits(canvas, ArenaLayout.PlayerOneScore, hud.PlayerOneScore, Palette.PlayerOneDigits);
        DrawScoreDigits(canvas, ArenaLayout.PlayerTwoScore, hud.PlayerTwoScore, Palette.PlayerTwoDigits);

        int minutes = (int)hud.Elapsed.TotalMinutes;
        _text.Color = Palette.TimerText;
        canvas.DrawText($"{minutes} : {hud.Elapsed.Seconds:00}", ArenaLayout.Timer.X, ArenaLayout.Timer.Y,
            SKTextAlign.Center, _timerFont, _text);
    }

    public void DrawOutlinedText(SKCanvas canvas, string text, float x, float y, SKFont font,
        SKColor fill, SKColor outline, float outlineWidth)
    {
        _textOutline.Color = outline;
        _textOutline.StrokeWidth = outlineWidth * 2f;
        canvas.DrawText(text, x, y, SKTextAlign.Center, font, _textOutline);
        _text.Color = fill;
        canvas.DrawText(text, x, y, SKTextAlign.Center, font, _text);
    }

    public void Dispose()
    {
        _backgroundCache?.Dispose();
        _arenaCache?.Dispose();
        _background.Dispose();
        _field.Dispose();
        _fill.Dispose();
        _stroke.Dispose();
        _text.Dispose();
        _textOutline.Dispose();
        _labelFont.Dispose();
        _stageFont.Dispose();
        _scoreFont.Dispose();
        _heartFont.Dispose();
        _timerFont.Dispose();
        _tagFont.Dispose();
        _smallTagFont.Dispose();
        _heart.Dispose();
        _playerOneTab.Dispose();
        _playerTwoTab.Dispose();
    }

    private void DrawField(SKCanvas canvas, SKRect field)
    {
        // Bevelled green well: light outer rim, dark inner rim, then the darker embossed floor.
        var outer = Inflate(field, 10f);
        _fill.Color = Palette.FrameGreenDark;
        canvas.DrawRoundRect(outer, 8f, 8f, _fill);
        _fill.Color = Palette.FrameGreenLight;
        canvas.DrawRoundRect(Inset(outer, 1f), 7f, 7f, _fill);
        _fill.Color = Palette.Background;
        canvas.DrawRoundRect(Inset(outer, 4f), 5f, 5f, _fill);
        _fill.Color = Palette.FrameGreenDark;
        canvas.DrawRect(Inflate(field, 2f), _fill);

        _field.Fill(canvas, field);
    }

    private void DrawNextBar(SKCanvas canvas, SKRect tiles, SKPoint label)
    {
        _fill.Color = Palette.FrameGreenDark;
        canvas.DrawRoundRect(Inflate(tiles, 3f), 4f, 4f, _fill);
        _fill.Color = Palette.Field;
        canvas.DrawRect(tiles, _fill);
        DrawOutlinedText(canvas, "NEXT", label.X, label.Y, _labelFont, Palette.NextLabel, Palette.Outline, 2.5f);
    }

    private void DrawArenaStatic(SKCanvas canvas, bool versus, string playerTwoTag)
    {
        _background.Fill(canvas, new SKRect(0, 0, 800, 600));
        DrawField(canvas, ArenaLayout.LeftField);
        DrawNextBar(canvas, ArenaLayout.LeftNext, ArenaLayout.LeftNextLabel);
        DrawHeart(canvas, ArenaLayout.LeftHeart);

        if (versus)
        {
            DrawField(canvas, ArenaLayout.RightField);
            DrawNextBar(canvas, ArenaLayout.RightNext, ArenaLayout.RightNextLabel);
            DrawHeart(canvas, ArenaLayout.RightHeart);
            DrawGroove(canvas, ArenaLayout.LeftIncomingBar);
            DrawGroove(canvas, ArenaLayout.RightIncomingBar);
        }

        DrawGoldFrame(canvas, ArenaLayout.MascotFrame, 6f);
        _fill.Color = new SKColor(0x08, 0x10, 0x18);
        canvas.DrawRect(MascotArea, _fill);
        DrawGoldPlate(canvas, ArenaLayout.StagePlate);
        DrawScoreBox(canvas, ArenaLayout.PlayerOneScore, "1", tagOnLeft: true);
        DrawScoreBox(canvas, ArenaLayout.PlayerTwoScore, playerTwoTag, tagOnLeft: false);
    }

    /// <summary>
    /// Returns a raster image of <paramref name="draw"/>'s 800×600 content at the canvas's current pixel scale,
    /// re-rendering only when the scale or <paramref name="variant"/> changes.
    /// </summary>
    private static SKImage Cached(SKCanvas canvas, ref SKImage? cache, ref (float Scale, int Variant) key, int variant,
        Action<SKCanvas> draw)
    {
        float scale = canvas.TotalMatrix.ScaleX;
        if (scale <= 0.01f)
        {
            scale = 1f;
        }

        var wanted = (MathF.Round(scale, 3), variant);
        if (cache is null || key != wanted)
        {
            cache?.Dispose();
            int width = Math.Max(1, (int)MathF.Ceiling(800 * scale));
            int height = Math.Max(1, (int)MathF.Ceiling(600 * scale));
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Scale(scale);
            draw(surface.Canvas);
            cache = surface.Snapshot();
            key = wanted;
        }

        return cache;
    }

    private void DrawHeart(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.Scale(bounds.Width / 100f, bounds.Height / 90f);

        _fill.Color = Palette.HeartDark;
        canvas.Save();
        canvas.Translate(3f, 4f);
        canvas.DrawPath(_heart, _fill);
        canvas.Restore();
        _fill.Color = Palette.HeartRed;
        canvas.DrawPath(_heart, _fill);
        _fill.Color = SKColors.White.WithAlpha(90);
        canvas.DrawOval(new SKRect(16f, 10f, 40f, 28f), _fill);
        canvas.Restore();

        var counter = HeartCounter(bounds);
        _fill.Color = SKColors.Black;
        canvas.DrawRect(counter, _fill);
        _stroke.Color = Palette.GoldDark;
        _stroke.StrokeWidth = 2f;
        canvas.DrawRect(counter, _stroke);
    }

    private static SKRect HeartCounter(SKRect bounds) =>
        new(bounds.MidX - 16f, bounds.Top + 14f, bounds.MidX + 16f, bounds.Top + 46f);

    private void DrawHeartDigits(SKCanvas canvas, SKRect bounds, int value, SKColor digits, float pulse)
    {
        var counter = HeartCounter(bounds);
        string text = Math.Min(value, 999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        float scale = (text.Length >= 3 ? 0.62f : text.Length == 2 ? 0.85f : 1f) * (1f + 0.35f * pulse);
        canvas.Save();
        canvas.Translate(counter.MidX, counter.Bottom - 7f);
        canvas.Scale(scale);
        _text.Color = pulse > 0 ? SKColors.White : digits;
        canvas.DrawText(text, 0, 0, SKTextAlign.Center, _heartFont, _text);
        canvas.Restore();
    }

    /// <summary>Round wins as a column of stars: filled for won rounds, hollow for the rest.</summary>
    private void DrawWinStars(SKCanvas canvas, SKPoint top, int wins, int needed)
    {
        for (int i = 0; i < needed; i++)
        {
            float cy = top.Y + i * 24f;
            using var star = new SKPathBuilder();
            for (int p = 0; p < 10; p++)
            {
                double a = -Math.PI / 2 + p * Math.PI / 5;
                float r = p % 2 == 0 ? 10f : 4.2f;
                float px = top.X + (float)Math.Cos(a) * r;
                float py = cy + (float)Math.Sin(a) * r;
                if (p == 0) star.MoveTo(px, py); else star.LineTo(px, py);
            }

            star.Close();
            using var path = star.Detach();
            _fill.Color = i < wins ? Palette.GoldLight : SKColors.Black.WithAlpha(120);
            canvas.DrawPath(path, _fill);
            _stroke.Color = Palette.GoldDark;
            _stroke.StrokeWidth = 1.5f;
            canvas.DrawPath(path, _stroke);
        }
    }

    private void DrawGroove(SKCanvas canvas, SKRect bar)
    {
        _fill.Color = Palette.FrameGreenDark;
        canvas.DrawRoundRect(bar, 6f, 6f, _fill);
        _fill.Color = SKColors.Black.WithAlpha(150);
        canvas.DrawRoundRect(Inset(bar, 2f), 4f, 4f, _fill);
    }

    private void DrawScoreDigits(SKCanvas canvas, SKRect box, int score, SKColor digits)
    {
        _text.Color = digits;
        canvas.DrawText(score.ToString(System.Globalization.CultureInfo.InvariantCulture),
            box.Right - 12f, box.Bottom - 8f, SKTextAlign.Right, _scoreFont, _text);
    }

    private void DrawScoreBox(SKCanvas canvas, SKRect box, string tag, bool tagOnLeft)
    {
        DrawGoldFrame(canvas, box, 4f);
        _fill.Color = SKColors.Black;
        canvas.DrawRect(Inset(box, 4f), _fill);

        // Small slanted tab carrying the player number, like the classic score plates.
        _fill.Color = Palette.Gold;
        canvas.DrawPath(tagOnLeft ? _playerOneTab : _playerTwoTab, _fill);
        _text.Color = Palette.Outline;
        float tagX = tagOnLeft ? box.Left - 3f : box.Right + 3f;
        float tagY = tagOnLeft ? box.Bottom - 3f : box.Top + 12f;
        canvas.DrawText(tag, tagX, tagY, SKTextAlign.Center, tag.Length > 1 ? _smallTagFont : _tagFont, _text);
    }

    private void DrawGoldFrame(SKCanvas canvas, SKRect rect, float thickness)
    {
        _fill.Color = Palette.GoldDark;
        canvas.DrawRect(Inflate(rect, 1.5f), _fill);
        _fill.Color = Palette.Gold;
        canvas.DrawRect(rect, _fill);
        _stroke.Color = Palette.GoldLight;
        _stroke.StrokeWidth = 1.5f;
        canvas.DrawRect(Inset(rect, thickness * 0.3f), _stroke);
        _fill.Color = Palette.GoldDark;
        canvas.DrawRect(Inset(rect, thickness - 1f), _fill);
    }

    private void DrawGoldPlate(SKCanvas canvas, SKRect rect)
    {
        _fill.Color = Palette.GoldDark;
        canvas.DrawRoundRect(Inflate(rect, 2f), 4f, 4f, _fill);
        _fill.Color = Palette.Gold;
        canvas.DrawRoundRect(rect, 3f, 3f, _fill);
        _stroke.Color = Palette.GoldLight;
        _stroke.StrokeWidth = 2f;
        canvas.DrawRoundRect(Inset(rect, 3f), 2f, 2f, _stroke);
    }

    private static SKPath CreateHeart()
    {
        // Heart in a 100×90 box.
        var path = new SKPathBuilder();
        path.MoveTo(50f, 88f);
        path.CubicTo(20f, 66f, 0f, 48f, 2f, 26f);
        path.CubicTo(4f, 8f, 18f, 0f, 30f, 0f);
        path.CubicTo(40f, 0f, 47f, 6f, 50f, 14f);
        path.CubicTo(53f, 6f, 60f, 0f, 70f, 0f);
        path.CubicTo(82f, 0f, 96f, 8f, 98f, 26f);
        path.CubicTo(100f, 48f, 80f, 66f, 50f, 88f);
        path.Close();
        return path.Detach();
    }

    private static SKPath CreateScoreTab(SKRect box, bool onLeft)
    {
        var path = new SKPathBuilder();
        if (onLeft)
        {
            path.MoveTo(box.Left - 14f, box.Bottom);
            path.LineTo(box.Left + 4f, box.Top + 12f);
            path.LineTo(box.Left + 4f, box.Bottom);
        }
        else
        {
            path.MoveTo(box.Right + 14f, box.Top);
            path.LineTo(box.Right - 4f, box.Bottom - 12f);
            path.LineTo(box.Right - 4f, box.Top);
        }

        path.Close();
        return path.Detach();
    }

    private static SKRect Inflate(SKRect rect, float amount) =>
        new(rect.Left - amount, rect.Top - amount, rect.Right + amount, rect.Bottom + amount);

    private static SKRect Inset(SKRect rect, float amount) => Inflate(rect, -amount);
}
