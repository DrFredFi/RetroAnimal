using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

public enum MascotMood
{
    Idle,
    Cheer,
    Worried,
    Sad,
    Celebrate,
}

/// <summary>
/// The arena's original host character, "Pip": a round zoo-keeper chick in a green cap who reacts
/// to the game (bobbing, cheering on chains, sweating when the stack is high, crying on defeat).
/// </summary>
internal sealed class MascotRenderer : IDisposable
{
    private static readonly SKColor Ink = new(0x22, 0x14, 0x0C);
    private static readonly SKColor Body = new(0xFF, 0xD8, 0x3A);
    private static readonly SKColor BodyShade = new(0xE8, 0xA8, 0x1A);
    private static readonly SKColor Beak = new(0xFF, 0x8A, 0x1E);
    private static readonly SKColor Cap = new(0x2E, 0x9E, 0x48);
    private static readonly SKColor CapDark = new(0x1C, 0x6A, 0x2E);

    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, Color = Ink, StrokeWidth = 2.5f,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    private readonly SKPath _capPath;
    private readonly SKPath _beakPath;

    public MascotRenderer()
    {
        var cap = new SKPathBuilder();
        cap.MoveTo(-34, -30);
        cap.QuadTo(-30, -66, 0, -66);
        cap.QuadTo(30, -66, 34, -30);
        cap.Close();
        _capPath = cap.Detach();

        var beak = new SKPathBuilder();
        beak.MoveTo(-9, 2);
        beak.QuadTo(0, -4, 9, 2);
        beak.QuadTo(0, 14, -9, 2);
        beak.Close();
        _beakPath = beak.Detach();
    }

    /// <summary>Draws the mascot centred in <paramref name="frame"/>. <paramref name="time"/> drives idle motion.</summary>
    public void Draw(SKCanvas canvas, SKRect frame, MascotMood mood, double time)
    {
        float t = (float)time;
        float bob = mood switch
        {
            MascotMood.Cheer or MascotMood.Celebrate => -Math.Abs(MathF.Sin(t * 9f)) * 14f,
            MascotMood.Worried => MathF.Sin(t * 22f) * 1.5f,
            MascotMood.Sad => 4f,
            _ => MathF.Sin(t * 2.4f) * 3f,
        };
        float squash = mood is MascotMood.Cheer or MascotMood.Celebrate ? 1f + MathF.Sin(t * 18f) * 0.04f : 1f;

        canvas.Save();
        canvas.ClipRect(frame);
        canvas.Translate(frame.MidX, frame.MidY + 18 + bob);
        float scale = Math.Min(frame.Width, frame.Height) / 170f;
        canvas.Scale(scale * squash, scale / squash);

        // Shadow.
        _fill.Color = SKColors.Black.WithAlpha(90);
        canvas.DrawOval(0, 62 - bob / scale, 40, 8, _fill);

        // Feet.
        Inked(canvas, Beak, (c, p) => { c.DrawOval(-16, 56, 11, 5, p); c.DrawOval(16, 56, 11, 5, p); });

        // Wings (raised when cheering).
        float wingAngle = mood is MascotMood.Cheer or MascotMood.Celebrate ? -60f + MathF.Sin(t * 18f) * 15f : 10f + MathF.Sin(t * 2.4f) * 4f;
        Wing(canvas, -44, 8, -wingAngle);
        Wing(canvas, 44, 8, wingAngle);

        // Body.
        using (var shader = SKShader.CreateRadialGradient(new SKPoint(-14, -18), 70, [Body, BodyShade], SKShaderTileMode.Clamp))
        {
            _fill.Shader = shader;
            canvas.DrawOval(0, 4, 48, 54, _fill);
            _fill.Shader = null;
        }

        canvas.DrawOval(0, 4, 48, 54, _line);
        _fill.Color = new SKColor(0xFF, 0xF2, 0xB0);
        canvas.DrawOval(0, 26, 28, 24, _fill);

        Face(canvas, mood, t);

        // Cap with brim and badge.
        Inked(canvas, Cap, (c, p) => c.DrawPath(_capPath, p));
        Inked(canvas, CapDark, (c, p) => c.DrawRoundRect(new SKRect(-40, -34, 46, -26), 4, 4, p));
        Inked(canvas, SKColors.White, (c, p) => c.DrawCircle(0, -48, 9, p), 2f);
        _fill.Color = new SKColor(0xFF, 0x8A, 0x1E);
        canvas.DrawCircle(0, -48, 4.5f, _fill);

        if (mood == MascotMood.Worried)
        {
            Inked(canvas, new SKColor(0x8C, 0xD8, 0xFF), (c, p) => c.DrawOval(38, -20, 5, 8, p), 2f);
        }

        if (mood == MascotMood.Celebrate)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = t * 2f + i * MathF.Tau / 5f;
                Star(canvas, MathF.Cos(a) * 70, -10 + MathF.Sin(a) * 55, 7);
            }
        }

        canvas.Restore();
    }

    public void Dispose()
    {
        _fill.Dispose();
        _line.Dispose();
        _capPath.Dispose();
        _beakPath.Dispose();
    }

    private void Face(SKCanvas canvas, MascotMood mood, float t)
    {
        bool blink = mood == MascotMood.Idle && t % 3.7f < 0.12f;
        if (mood is MascotMood.Cheer or MascotMood.Celebrate)
        {
            Arc(canvas, -16, -6, true);
            Arc(canvas, 16, -6, true);
        }
        else if (mood == MascotMood.Sad)
        {
            Arc(canvas, -16, -4, false);
            Arc(canvas, 16, -4, false);
            _fill.Color = new SKColor(0x6C, 0xC8, 0xFF);
            float drop = (t * 40f) % 30f;
            canvas.DrawOval(-18, 4 + drop, 3.5f, 5, _fill);
            canvas.DrawOval(18, 4 + (drop + 15) % 30f, 3.5f, 5, _fill);
        }
        else if (blink)
        {
            canvas.DrawLine(-22, -6, -10, -6, _line);
            canvas.DrawLine(10, -6, 22, -6, _line);
        }
        else
        {
            Inked(canvas, SKColors.White, (c, p) => { c.DrawOval(-16, -6, 9, 11, p); c.DrawOval(16, -6, 9, 11, p); }, 2f);
            float look = mood == MascotMood.Worried ? MathF.Sin(t * 6f) * 2.5f : 0f;
            _fill.Color = Ink;
            canvas.DrawCircle(-14 + look, -4, 4.5f, _fill);
            canvas.DrawCircle(18 + look, -4, 4.5f, _fill);
            _fill.Color = SKColors.White;
            canvas.DrawCircle(-12.5f + look, -6, 1.6f, _fill);
            canvas.DrawCircle(19.5f + look, -6, 1.6f, _fill);
        }

        _fill.Color = new SKColor(0xFF, 0x7E, 0x8E, 150);
        canvas.DrawOval(-30, 10, 7, 4, _fill);
        canvas.DrawOval(30, 10, 7, 4, _fill);

        canvas.Save();
        canvas.Translate(0, 8);
        if (mood is MascotMood.Cheer or MascotMood.Celebrate)
        {
            Inked(canvas, new SKColor(0xB0, 0x20, 0x20), (c, p) => c.DrawOval(0, 4, 9, 8, p), 2f);
            Inked(canvas, Beak, (c, p) => c.DrawOval(0, -3, 11, 4, p), 2f);
        }
        else
        {
            Inked(canvas, Beak, (c, p) => c.DrawPath(_beakPath, p), 2f);
        }

        canvas.Restore();
    }

    private void Wing(SKCanvas canvas, float x, float y, float degrees)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(degrees);
        Inked(canvas, BodyShade, (c, p) => c.DrawOval(0, 10, 10, 20, p));
        canvas.Restore();
    }

    private void Arc(SKCanvas canvas, float x, float y, bool happy)
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(x - 8, y + (happy ? 3 : -2));
        builder.QuadTo(x, y + (happy ? -7 : 6), x + 8, y + (happy ? 3 : -2));
        using var path = builder.Detach();
        canvas.DrawPath(path, _line);
    }

    private void Star(SKCanvas canvas, float x, float y, float r)
    {
        _fill.Color = new SKColor(0xFF, 0xF0, 0x70);
        using var builder = new SKPathBuilder();
        for (int i = 0; i < 10; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 5;
            float rr = i % 2 == 0 ? r : r * 0.45f;
            float px = x + (float)Math.Cos(a) * rr;
            float py = y + (float)Math.Sin(a) * rr;
            if (i == 0) builder.MoveTo(px, py); else builder.LineTo(px, py);
        }

        builder.Close();
        using var path = builder.Detach();
        canvas.DrawPath(path, _fill);
    }

    private void Inked(SKCanvas canvas, SKColor color, Action<SKCanvas, SKPaint> shape, float width = 2.5f)
    {
        _fill.Color = color;
        shape(canvas, _fill);
        _line.StrokeWidth = width;
        shape(canvas, _line);
        _line.StrokeWidth = 2.5f;
    }
}
