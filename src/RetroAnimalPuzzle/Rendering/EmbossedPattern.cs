using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Repeating tile of embossed food silhouettes (bones, carrots, bananas, fish, bamboo), recorded once
/// into a picture shader so filling any area with it costs a single draw call.
/// </summary>
internal sealed class EmbossedPattern : IDisposable
{
    private const float TileSize = 200f;

    /// <summary>The tile is rasterised at this multiple of its logical size so it stays crisp when the window is scaled up.</summary>
    private const int Supersample = 3;

    private readonly SKPicture _picture;
    private readonly SKImage _tile;
    private readonly SKShader _shader;
    private readonly SKPaint _paint;

    public EmbossedPattern(SKColor background, SKColor relief, SKColor shadow, SKColor highlight)
    {
        _picture = Record(background, relief, shadow, highlight);

        // Rasterise once: an image shader is far cheaper per frame than replaying the vector picture.
        int size = (int)TileSize * Supersample;
        using (var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            surface.Canvas.Scale(Supersample);
            surface.Canvas.DrawPicture(_picture);
            _tile = surface.Snapshot();
        }
        _shader = SKShader.CreateImage(_tile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), SKMatrix.CreateScale(1f / Supersample, 1f / Supersample));
        _paint = new SKPaint { Shader = _shader, IsAntialias = true };
    }

    public void Fill(SKCanvas canvas, SKRect area) => canvas.DrawRect(area, _paint);

    public void Fill(SKCanvas canvas, SKRoundRect area) => canvas.DrawRoundRect(area, _paint);

    public void Dispose()
    {
        _paint.Dispose();
        _shader.Dispose();
        _tile.Dispose();
        _picture.Dispose();
    }

    private static SKPicture Record(SKColor background, SKColor relief, SKColor shadow, SKColor highlight)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, TileSize, TileSize));
        canvas.Clear(background);

        using var shadowPaint = new SKPaint { Color = shadow, IsAntialias = true };
        using var highlightPaint = new SKPaint { Color = highlight, IsAntialias = true };
        using var reliefPaint = new SKPaint { Color = relief, IsAntialias = true };

        (Func<SKPath> Shape, float X, float Y, float Degrees)[] placements =
        [
            (SilhouetteShapes.Bone, 52f, 38f, 18f),
            (SilhouetteShapes.Carrot, 152f, 52f, -38f),
            (SilhouetteShapes.Banana, 48f, 132f, 12f),
            (SilhouetteShapes.Fish, 150f, 152f, -16f),
            (SilhouetteShapes.Bamboo, 104f, 92f, 62f),
        ];

        foreach (var (shape, x, y, degrees) in placements)
        {
            using var path = shape();
            // Draw each silhouette at the original position and at the wrap-around copies so
            // shapes crossing the tile edge tile seamlessly.
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    float px = x + dx * TileSize;
                    float py = y + dy * TileSize;
                    DrawEmbossed(canvas, path, px, py, degrees, shadowPaint, highlightPaint, reliefPaint);
                }
            }
        }

        return recorder.EndRecording();
    }

    private static void DrawEmbossed(SKCanvas canvas, SKPath path, float x, float y, float degrees,
        SKPaint shadow, SKPaint highlight, SKPaint relief)
    {
        DrawAt(canvas, path, x + 2.5f, y + 2.5f, degrees, shadow);
        DrawAt(canvas, path, x - 1.5f, y - 1.5f, degrees, highlight);
        DrawAt(canvas, path, x, y, degrees, relief);
    }

    private static void DrawAt(SKCanvas canvas, SKPath path, float x, float y, float degrees, SKPaint paint)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(degrees);
        canvas.DrawPath(path, paint);
        canvas.Restore();
    }
}
