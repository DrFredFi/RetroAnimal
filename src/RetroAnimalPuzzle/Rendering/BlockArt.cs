using RetroAnimalPuzzle.Core;
using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

public enum BlockPose
{
    Normal,

    /// <summary>Animal with its mouth wide open, used while eating.</summary>
    Chomp,

    /// <summary>Only the animal's head (silhouette, ears, fixed markings): what is seen from behind while it turns.</summary>
    Head,

    /// <summary>Only the animal's face features, drawn over <see cref="Head"/> while the head turns.</summary>
    Face,
}

/// <summary>
/// Original vector illustrations for every block: animal faces (no backing tile, like the classic look)
/// and food on colour-coded bevelled tiles. Each drawing is recorded once into an <see cref="SKPicture"/>
/// in a 40×40 unit box and replayed scaled. A PNG in <c>Assets/Sprites</c> (e.g. <c>monkey.png</c>,
/// <c>monkey_eat.png</c>, <c>banana.png</c>, <c>coin.png</c>) replaces the built-in drawing for that block.
/// </summary>
internal sealed class BlockArt : IDisposable
{
    public const float Unit = 40f;

    private static readonly SKColor Ink = new(0x22, 0x14, 0x0C);

    private readonly Dictionary<(Block, BlockPose), SKPicture> _pictures = [];
    private readonly Dictionary<(Block, BlockPose), SKImage> _sprites = [];
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, Color = Ink,
        StrokeWidth = 1.8f, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round,
    };
    private readonly SKPaint _layer = new();
    private readonly SKPaint _edge = new() { IsAntialias = true, Color = new SKColor(0x22, 0x14, 0x0C, 220) };
    private readonly SKSamplingOptions _sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public BlockArt(string? spriteDirectory = null)
    {
        LoadSprites(spriteDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites"));
    }

    /// <summary>Draws a block with its top-left corner at (x, y).</summary>
    public void Draw(SKCanvas canvas, Block block, float x, float y, float size, BlockPose pose = BlockPose.Normal, byte alpha = 255) =>
        DrawCentered(canvas, block, x + size / 2f, y + size / 2f, size, pose, alpha);

    public void DrawCentered(SKCanvas canvas, Block block, float centerX, float centerY, float size,
        BlockPose pose = BlockPose.Normal, byte alpha = 255, float rotationDegrees = 0f, float scaleX = 1f, byte shade = 0)
    {
        if (block.IsEmpty || size <= 0.5f || alpha == 0)
        {
            return;
        }

        if (!block.IsAnimal)
        {
            pose = BlockPose.Normal;
        }

        int save = alpha < 255 || shade > 0 ? canvas.SaveLayer(LayerPaint(alpha, shade)) : canvas.Save();
        canvas.Translate(centerX, centerY);
        if (rotationDegrees != 0f)
        {
            canvas.RotateDegrees(rotationDegrees);
        }

        if (scaleX != 1f)
        {
            // Horizontal squash: used to fake a tile spinning on its vertical axis.
            canvas.Scale(scaleX, 1f);
        }

        if (_sprites.TryGetValue((block, pose), out var sprite) || _sprites.TryGetValue((block, BlockPose.Normal), out sprite))
        {
            canvas.DrawImage(sprite, new SKRect(-size / 2f, -size / 2f, size / 2f, size / 2f), _sampling);
        }
        else
        {
            canvas.Scale(size / Unit);
            canvas.Translate(-Unit / 2f, -Unit / 2f);
            canvas.DrawPicture(PictureFor(block, pose));
        }

        canvas.RestoreToCount(save);
    }

    /// <summary>
    /// Draws a block turned by <paramref name="angle"/> (radians) around its vertical axis, in fake 3-D.
    /// Food tiles and coins turn like a flat card: they narrow to their edge (showing a little thickness) and
    /// come back. Animals turn their head: the round head stays almost full width while the face slides towards
    /// the side, narrowing, and disappears behind the head, so you see the back of the head before it comes round.
    /// </summary>
    public void DrawTurning(SKCanvas canvas, Block block, float centerX, float centerY, float size, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        bool hasSprite = _sprites.ContainsKey((block, BlockPose.Normal));

        if (!block.IsAnimal || hasSprite)
        {
            float width = Math.Max(0.04f, MathF.Abs(cos));
            byte shade = (byte)(100 * (1f - MathF.Abs(cos)) + (cos < 0 ? 25 : 0));

            // Card thickness: a dark strip on the side that is turning towards the viewer.
            float edge = size * 0.09f * MathF.Abs(sin);
            if (edge > 0.5f)
            {
                float side = Math.Sign(sin * cos) * (width * size / 2f + edge / 2f);
                canvas.DrawRoundRect(new SKRect(centerX + side - edge / 2f, centerY - size * 0.46f,
                    centerX + side + edge / 2f, centerY + size * 0.46f), edge / 2f, edge / 2f, _edge);
            }

            DrawCentered(canvas, block, centerX, centerY, size, BlockPose.Normal, 255, 0f, width, shade);
            return;
        }

        // Head: a sphere-ish silhouette only narrows a little when seen from the side.
        float headWidth = 0.86f + 0.14f * MathF.Abs(cos);
        DrawCentered(canvas, block, centerX, centerY, size, BlockPose.Head, 255, 0f, headWidth, (byte)(cos < 0 ? 14 : 0));

        // Face: slides across the front of the head and is hidden while the head faces away.
        if (cos > 0.05f)
        {
            float faceWidth = MathF.Pow(cos, 0.7f);
            float shift = sin * size * 0.2f;
            DrawCentered(canvas, block, centerX + shift, centerY, size, BlockPose.Face, (byte)(255 * Math.Min(1f, cos * 4f)), 0f, faceWidth);
        }
    }

    public void Dispose()
    {
        foreach (var picture in _pictures.Values) picture.Dispose();
        foreach (var sprite in _sprites.Values) sprite.Dispose();
        _fill.Dispose();
        _line.Dispose();
        _layer.ColorFilter?.Dispose();
        _layer.Dispose();
        _edge.Dispose();
    }

    private SKPaint LayerPaint(byte alpha, byte shade)
    {
        _layer.Color = SKColors.White.WithAlpha(alpha);
        _layer.ColorFilter?.Dispose();
        _layer.ColorFilter = shade > 0 ? SKColorFilter.CreateBlendMode(SKColors.Black.WithAlpha(shade), SKBlendMode.SrcATop) : null;
        return _layer;
    }

    private SKPicture PictureFor(Block block, BlockPose pose)
    {
        if (_pictures.TryGetValue((block, pose), out var picture))
        {
            return picture;
        }

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(-20, -20, Unit + 20, Unit + 20));
        DrawBlock(canvas, block, pose);
        picture = recorder.EndRecording();
        _pictures[(block, pose)] = picture;
        return picture;
    }

    private void LoadSprites(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var animal in Enum.GetValues<AnimalType>())
        {
            TryLoadSprite(directory, animal.ToString(), Block.Of(animal), BlockPose.Normal);
            TryLoadSprite(directory, animal + "_eat", Block.Of(animal), BlockPose.Chomp);
        }

        foreach (var food in Enum.GetValues<FoodType>())
        {
            TryLoadSprite(directory, food.ToString(), Block.Of(food), BlockPose.Normal);
        }

        TryLoadSprite(directory, "coin", Block.Coin, BlockPose.Normal);
    }

    private void TryLoadSprite(string directory, string name, Block block, BlockPose pose)
    {
        string path = Path.Combine(directory, name.ToLowerInvariant() + ".png");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            if (SKImage.FromEncodedData(path) is { } image)
            {
                _sprites[(block, pose)] = image;
            }
        }
        catch (IOException)
        {
            // Unreadable sprite: keep the built-in drawing.
        }
    }

    // ------------------------------------------------------------------------------------------
    // Drawing primitives (all in the 40×40 unit box)
    // ------------------------------------------------------------------------------------------

    private void DrawBlock(SKCanvas c, Block block, BlockPose pose)
    {
        switch (block.Kind)
        {
            case BlockKind.Animal:
                DrawAnimal(c, block.Animal, pose);
                break;
            case BlockKind.Food:
                DrawFood(c, block.Food);
                break;
            case BlockKind.Coin:
                DrawCoin(c);
                break;
        }
    }

    private void Fill(SKCanvas c, SKColor color, Action<SKCanvas, SKPaint> shape)
    {
        _fill.Color = color;
        shape(c, _fill);
    }

    private void Outline(SKCanvas c, Action<SKCanvas, SKPaint> shape, float width = 1.8f)
    {
        _line.StrokeWidth = width;
        shape(c, _line);
        _line.StrokeWidth = 1.8f;
    }

    /// <summary>Fills a shape and strokes it in ink.</summary>
    private void Inked(SKCanvas c, SKColor color, Action<SKCanvas, SKPaint> shape, float width = 1.8f)
    {
        Fill(c, color, shape);
        Outline(c, shape, width);
    }

    private static void Oval(SKCanvas c, SKPaint p, float cx, float cy, float rx, float ry, float degrees = 0)
    {
        if (degrees == 0)
        {
            c.DrawOval(cx, cy, rx, ry, p);
            return;
        }

        c.Save();
        c.Translate(cx, cy);
        c.RotateDegrees(degrees);
        c.DrawOval(0, 0, rx, ry, p);
        c.Restore();
    }

    private static SKPath Build(Action<SKPathBuilder> build)
    {
        var builder = new SKPathBuilder();
        build(builder);
        return builder.Detach();
    }

    private void Shine(SKCanvas c, float cx, float cy, float r) =>
        Fill(c, SKColors.White, (k, p) => k.DrawCircle(cx, cy, r, p));

    private void ClosedEye(SKCanvas c, float cx, float cy, float w = 3.2f)
    {
        using var path = Build(b =>
        {
            b.MoveTo(cx - w, cy + 1f);
            b.QuadTo(cx, cy - 2.6f, cx + w, cy + 1f);
        });
        Outline(c, (k, p) => k.DrawPath(path, p), 2.2f);
    }

    private void OpenMouth(SKCanvas c, float cx, float cy, float rx, float ry)
    {
        Inked(c, new SKColor(0x8A, 0x10, 0x1C), (k, p) => k.DrawOval(cx, cy, rx, ry, p));
        Fill(c, new SKColor(0xFF, 0x78, 0x90), (k, p) => k.DrawOval(cx, cy + ry * 0.45f, rx * 0.6f, ry * 0.42f, p));
        Fill(c, SKColors.White, (k, p) => k.DrawRect(cx - rx * 0.55f, cy - ry + 0.8f, rx * 0.45f, ry * 0.32f, p));
        Fill(c, SKColors.White, (k, p) => k.DrawRect(cx + rx * 0.1f, cy - ry + 0.8f, rx * 0.45f, ry * 0.32f, p));
    }

    private void Smile(SKCanvas c, float cx, float cy, float w, float depth)
    {
        using var path = Build(b =>
        {
            b.MoveTo(cx - w, cy);
            b.QuadTo(cx, cy + depth, cx + w, cy);
        });
        Outline(c, (k, p) => k.DrawPath(path, p), 1.6f);
    }

    // ------------------------------------------------------------------------------------------
    // Food tiles
    // ------------------------------------------------------------------------------------------

    private void DrawFood(SKCanvas c, FoodType food)
    {
        var (light, dark) = food switch
        {
            FoodType.Banana => (new SKColor(0xFF, 0xC4, 0x3A), new SKColor(0xD2, 0x7A, 0x0E)),
            FoodType.Carrot => (new SKColor(0xF0, 0x4A, 0x3C), new SKColor(0x9C, 0x16, 0x14)),
            FoodType.Bamboo => (new SKColor(0x5C, 0xC8, 0x4C), new SKColor(0x1D, 0x74, 0x24)),
            FoodType.Bone => (new SKColor(0x4A, 0x8C, 0xF0), new SKColor(0x1A, 0x40, 0xA6)),
            _ => (new SKColor(0x44, 0xD0, 0xD4), new SKColor(0x12, 0x80, 0x92)),
        };

        FoodTile(c, light, dark);
        switch (food)
        {
            case FoodType.Banana: Banana(c); break;
            case FoodType.Carrot: Carrot(c); break;
            case FoodType.Bamboo: Bamboo(c); break;
            case FoodType.Bone: Bone(c); break;
            default: Fish(c); break;
        }
    }

    private void FoodTile(SKCanvas c, SKColor light, SKColor dark)
    {
        var rect = new SKRect(1.5f, 1.5f, 38.5f, 38.5f);
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 2), new SKPoint(0, 38), [light, dark], SKShaderTileMode.Clamp);
        _fill.Shader = shader;
        c.DrawRoundRect(rect, 6.5f, 6.5f, _fill);
        _fill.Shader = null;
        Outline(c, (k, p) => k.DrawRoundRect(rect, 6.5f, 6.5f, p), 2f);

        using var highlight = Build(b =>
        {
            b.MoveTo(5.5f, 28f);
            b.LineTo(5.5f, 9f);
            b.QuadTo(5.5f, 5.5f, 9f, 5.5f);
            b.LineTo(28f, 5.5f);
        });
        _line.Color = SKColors.White.WithAlpha(120);
        Outline(c, (k, p) => k.DrawPath(highlight, p), 1.6f);
        _line.Color = Ink;
    }

    private void Banana(SKCanvas c)
    {
        using var body = Build(b =>
        {
            b.MoveTo(9f, 10f);
            b.QuadTo(9f, 31f, 32f, 30f);
            b.LineTo(32.5f, 26.5f);
            b.QuadTo(16f, 25f, 14.5f, 9.5f);
            b.Close();
        });
        Inked(c, new SKColor(0xFF, 0xE8, 0x4A), (k, p) => k.DrawPath(body, p));
        using var ridge = Build(b =>
        {
            b.MoveTo(12f, 13f);
            b.QuadTo(13.5f, 25f, 27f, 27.5f);
        });
        _line.Color = new SKColor(0xD8, 0x9A, 0x10);
        Outline(c, (k, p) => k.DrawPath(ridge, p), 1.2f);
        _line.Color = Ink;
        Inked(c, new SKColor(0x6B, 0x3A, 0x10), (k, p) => k.DrawRoundRect(new SKRect(9.2f, 6.5f, 14.4f, 11f), 1.2f, 1.2f, p), 1.2f);
        Fill(c, new SKColor(0x4A, 0x2A, 0x0A), (k, p) => k.DrawCircle(32.4f, 28.3f, 1.6f, p));
    }

    private void Carrot(SKCanvas c)
    {
        var leaf = new SKColor(0x3F, 0xC2, 0x3A);
        Inked(c, leaf, (k, p) => Oval(k, p, 28.5f, 7.5f, 2.6f, 6f, 30f), 1.4f);
        Inked(c, leaf, (k, p) => Oval(k, p, 32.5f, 11f, 2.6f, 6f, 75f), 1.4f);
        Inked(c, leaf, (k, p) => Oval(k, p, 31.5f, 6.5f, 2.3f, 5.5f, 50f), 1.4f);

        using var body = Build(b =>
        {
            b.MoveTo(20.5f, 10f);
            b.QuadTo(26f, 8f, 30.5f, 14f);
            b.QuadTo(31f, 18f, 28f, 20f);
            b.QuadTo(17f, 30f, 7f, 34f);
            b.QuadTo(10f, 23f, 20.5f, 10f);
            b.Close();
        });
        Inked(c, new SKColor(0xFF, 0x8C, 0x1E), (k, p) => k.DrawPath(body, p));
        _line.Color = new SKColor(0xC2, 0x58, 0x0A);
        Outline(c, (k, p) =>
        {
            k.DrawLine(17f, 19f, 20f, 21.5f, p);
            k.DrawLine(22f, 15f, 25f, 17f, p);
            k.DrawLine(12.5f, 26f, 15f, 28f, p);
        }, 1.3f);
        _line.Color = Ink;
        Fill(c, SKColors.White.WithAlpha(150), (k, p) => Oval(k, p, 22f, 13.5f, 1.4f, 3.2f, 40f));
    }

    private void Bamboo(SKCanvas c)
    {
        var stalk = new SKColor(0xB4, 0xEC, 0x6A);
        var node = new SKColor(0x4C, 0x96, 0x28);
        c.Save();
        c.RotateDegrees(-12f, 20f, 20f);
        Inked(c, stalk, (k, p) => k.DrawRoundRect(new SKRect(15.5f, 3.5f, 24.5f, 36.5f), 3f, 3f, p));
        Fill(c, node, (k, p) =>
        {
            k.DrawRect(15.5f, 13f, 9f, 2.2f, p);
            k.DrawRect(15.5f, 25f, 9f, 2.2f, p);
        });
        Fill(c, SKColors.White.WithAlpha(140), (k, p) => k.DrawRoundRect(new SKRect(17.3f, 5.5f, 19f, 34f), 1f, 1f, p));
        c.Restore();

        using var leafPath = Build(b =>
        {
            b.MoveTo(23f, 15f);
            b.QuadTo(30f, 6f, 37f, 8f);
            b.QuadTo(31f, 15f, 23f, 15f);
            b.Close();
        });
        Inked(c, new SKColor(0x6E, 0xD0, 0x3E), (k, p) => k.DrawPath(leafPath, p), 1.4f);
    }

    private void Bone(SKCanvas c)
    {
        c.Save();
        c.Translate(20f, 20f);
        c.RotateDegrees(-38f);
        void Shape(SKCanvas k, SKPaint p)
        {
            k.DrawRoundRect(new SKRect(-11f, -3.6f, 11f, 3.6f), 2f, 2f, p);
            k.DrawCircle(-12f, -4.2f, 5f, p);
            k.DrawCircle(-12f, 4.2f, 5f, p);
            k.DrawCircle(12f, -4.2f, 5f, p);
            k.DrawCircle(12f, 4.2f, 5f, p);
        }

        Outline(c, Shape, 3.6f);
        Fill(c, new SKColor(0xFF, 0xF7, 0xE4), Shape);
        Fill(c, new SKColor(0xE2, 0xD3, 0xB0), (k, p) => k.DrawRect(-9f, 1.2f, 18f, 2f, p));
        c.Restore();
    }

    private void Fish(SKCanvas c)
    {
        void Shape(SKCanvas k, SKPaint p)
        {
            k.DrawOval(17f, 20f, 11.5f, 7.5f, p);
            using var tail = Build(b =>
            {
                b.MoveTo(26f, 20f);
                b.LineTo(35.5f, 12.5f);
                b.LineTo(34f, 20f);
                b.LineTo(35.5f, 27.5f);
                b.Close();
            });
            k.DrawPath(tail, p);
        }

        Outline(c, Shape, 3.6f);
        Fill(c, new SKColor(0xB6, 0xDE, 0xF4), Shape);
        Fill(c, new SKColor(0xE8, 0xF6, 0xFF), (k, p) => k.DrawOval(16f, 23.5f, 8f, 3f, p));
        _line.Color = new SKColor(0x4A, 0x7E, 0xA8);
        Outline(c, (k, p) => k.DrawArc(new SKRect(9f, 14f, 17f, 26f), -60f, 120f, false, p), 1.3f);
        _line.Color = Ink;
        Fill(c, SKColors.White, (k, p) => k.DrawCircle(10.5f, 18.5f, 2.6f, p));
        Fill(c, Ink, (k, p) => k.DrawCircle(10.2f, 18.6f, 1.4f, p));
    }

    private void DrawCoin(SKCanvas c)
    {
        using var shader = SKShader.CreateRadialGradient(new SKPoint(15f, 14f), 22f,
            [new SKColor(0xFF, 0xF2, 0x9A), new SKColor(0xF5, 0xB8, 0x1E), new SKColor(0xB0, 0x70, 0x08)],
            [0f, 0.55f, 1f], SKShaderTileMode.Clamp);
        _fill.Shader = shader;
        c.DrawCircle(20f, 20f, 16.5f, _fill);
        _fill.Shader = null;
        Outline(c, (k, p) => k.DrawCircle(20f, 20f, 16.5f, p), 2f);
        _line.Color = new SKColor(0xA0, 0x62, 0x06);
        Outline(c, (k, p) => k.DrawCircle(20f, 20f, 12.5f, p), 1.4f);
        _line.Color = Ink;

        using var star = Build(b =>
        {
            for (int i = 0; i < 10; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? 9f : 4f;
                float x = 20f + (float)(Math.Cos(angle) * r);
                float y = 20.5f + (float)(Math.Sin(angle) * r);
                if (i == 0) b.MoveTo(x, y); else b.LineTo(x, y);
            }

            b.Close();
        });
        Inked(c, new SKColor(0xFF, 0xF6, 0xC0), (k, p) => k.DrawPath(star, p), 1.2f);
        Fill(c, SKColors.White.WithAlpha(200), (k, p) => Oval(k, p, 12.5f, 11f, 3.2f, 1.8f, -40f));
    }

    // ------------------------------------------------------------------------------------------
    // Animals
    //
    // Each animal is drawn in two layers so it can turn its head in fake 3-D: the HEAD (silhouette,
    // ears, fur markings that stay put) and the FACE (eyes, nose, mouth, muzzle) which slides across
    // the head and disappears behind it. Normal/Chomp draw both layers.
    // ------------------------------------------------------------------------------------------

    private void DrawAnimal(SKCanvas c, AnimalType animal, BlockPose pose)
    {
        bool head = pose is BlockPose.Normal or BlockPose.Chomp or BlockPose.Head;
        bool face = pose is BlockPose.Normal or BlockPose.Chomp or BlockPose.Face;
        bool chomp = pose == BlockPose.Chomp;
        switch (animal)
        {
            case AnimalType.Monkey: Monkey(c, chomp, head, face); break;
            case AnimalType.Rabbit: Rabbit(c, chomp, head, face); break;
            case AnimalType.Panda: Panda(c, chomp, head, face); break;
            case AnimalType.Dog: Dog(c, chomp, head, face); break;
            default: Cat(c, chomp, head, face); break;
        }
    }

    private void Monkey(SKCanvas c, bool chomp, bool head, bool face)
    {
        var fur = new SKColor(0x8C, 0x4C, 0x20);
        var skin = new SKColor(0xF6, 0xC9, 0x94);
        if (head)
        {
            Inked(c, fur, (k, p) => { k.DrawCircle(5.5f, 20f, 5.8f, p); k.DrawCircle(34.5f, 20f, 5.8f, p); });
            Fill(c, skin, (k, p) => { k.DrawCircle(5.8f, 20f, 3.2f, p); k.DrawCircle(34.2f, 20f, 3.2f, p); });
            Inked(c, fur, (k, p) => k.DrawCircle(20f, 20f, 15.5f, p));
            Fill(c, new SKColor(0xA8, 0x62, 0x30), (k, p) => k.DrawOval(20f, 9f, 6f, 2.5f, p));
        }

        if (!face)
        {
            return;
        }

        void Mask(SKCanvas k, SKPaint p)
        {
            k.DrawCircle(15f, 18f, 6.2f, p);
            k.DrawCircle(25f, 18f, 6.2f, p);
            k.DrawOval(20f, 27f, 10f, 7.5f, p);
        }

        Outline(c, Mask, 3.2f);
        Fill(c, skin, Mask);

        if (chomp)
        {
            ClosedEye(c, 15f, 18f);
            ClosedEye(c, 25f, 18f);
            OpenMouth(c, 20f, 28.5f, 6.5f, 5.5f);
        }
        else
        {
            Fill(c, Ink, (k, p) => { k.DrawOval(15f, 18f, 2.1f, 2.8f, p); k.DrawOval(25f, 18f, 2.1f, 2.8f, p); });
            Shine(c, 15.7f, 17f, 0.8f);
            Shine(c, 25.7f, 17f, 0.8f);
            Fill(c, new SKColor(0x6A, 0x36, 0x14), (k, p) => { k.DrawCircle(18.6f, 25f, 1f, p); k.DrawCircle(21.4f, 25f, 1f, p); });
            Smile(c, 20f, 29f, 4.5f, 3.4f);
        }
    }

    private void Rabbit(SKCanvas c, bool chomp, bool head, bool face)
    {
        var fur = SKColors.White;
        var shade = new SKColor(0xDC, 0xDC, 0xE8);
        var pink = new SKColor(0xFF, 0x9C, 0xB8);
        if (head)
        {
            Inked(c, fur, (k, p) => { Oval(k, p, 13f, 9f, 4.2f, 10f, -10f); Oval(k, p, 27f, 9f, 4.2f, 10f, 10f); });
            Fill(c, pink, (k, p) => { Oval(k, p, 13.2f, 10f, 2f, 7f, -10f); Oval(k, p, 26.8f, 10f, 2f, 7f, 10f); });
            Inked(c, fur, (k, p) => k.DrawOval(20f, 26f, 14.5f, 12f, p));
            Fill(c, shade, (k, p) => k.DrawOval(20f, 33.5f, 10f, 3.5f, p));
        }

        if (!face)
        {
            return;
        }

        Fill(c, new SKColor(0xFF, 0xC4, 0xD4), (k, p) => { k.DrawOval(11f, 29f, 3f, 2f, p); k.DrawOval(29f, 29f, 3f, 2f, p); });
        if (chomp)
        {
            ClosedEye(c, 14f, 24f);
            ClosedEye(c, 26f, 24f);
            OpenMouth(c, 20f, 31f, 5.5f, 5f);
        }
        else
        {
            Inked(c, new SKColor(0xE0, 0x24, 0x34), (k, p) => { k.DrawCircle(14f, 24f, 2.7f, p); k.DrawCircle(26f, 24f, 2.7f, p); }, 1.2f);
            Shine(c, 14.8f, 23.2f, 0.9f);
            Shine(c, 26.8f, 23.2f, 0.9f);
            Fill(c, pink, (k, p) => k.DrawOval(20f, 28.3f, 1.9f, 1.3f, p));
            Smile(c, 18.2f, 30f, 1.8f, 1.6f);
            Smile(c, 21.8f, 30f, 1.8f, 1.6f);
            Inked(c, SKColors.White, (k, p) => k.DrawRect(18.4f, 31.2f, 3.2f, 3.4f, p), 1.1f);
            Outline(c, (k, p) => k.DrawLine(20f, 31.2f, 20f, 34.6f, p), 0.8f);
        }
    }

    private void Panda(SKCanvas c, bool chomp, bool head, bool face)
    {
        var black = new SKColor(0x1E, 0x1E, 0x26);
        if (head)
        {
            Inked(c, black, (k, p) => { k.DrawCircle(8f, 9.5f, 5.6f, p); k.DrawCircle(32f, 9.5f, 5.6f, p); });
            Inked(c, SKColors.White, (k, p) => k.DrawOval(20f, 21.5f, 16f, 15f, p));
            Fill(c, new SKColor(0xE2, 0xE2, 0xEA), (k, p) => k.DrawOval(20f, 32f, 10f, 3.5f, p));
        }

        if (!face)
        {
            return;
        }

        Fill(c, black, (k, p) => { Oval(k, p, 13.5f, 20f, 4.3f, 5.6f, 30f); Oval(k, p, 26.5f, 20f, 4.3f, 5.6f, -30f); });
        if (chomp)
        {
            _line.Color = SKColors.White;
            ClosedEye(c, 13.8f, 19.5f, 2.4f);
            ClosedEye(c, 26.2f, 19.5f, 2.4f);
            _line.Color = Ink;
            OpenMouth(c, 20f, 29f, 5.5f, 5f);
        }
        else
        {
            Fill(c, SKColors.White, (k, p) => { k.DrawCircle(14.4f, 19.6f, 1.7f, p); k.DrawCircle(25.6f, 19.6f, 1.7f, p); });
            Fill(c, black, (k, p) => k.DrawOval(20f, 26f, 2.6f, 1.8f, p));
            Smile(c, 18f, 28.5f, 2f, 1.8f);
            Smile(c, 22f, 28.5f, 2f, 1.8f);
        }
    }

    private void Dog(SKCanvas c, bool chomp, bool head, bool face)
    {
        var fur = new SKColor(0x70, 0x82, 0xAE);
        var dark = new SKColor(0x46, 0x54, 0x7C);
        if (head)
        {
            using var ears = Build(b =>
            {
                b.MoveTo(4.5f, 17f); b.LineTo(8.5f, 2.5f); b.LineTo(17f, 9f); b.Close();
                b.MoveTo(35.5f, 17f); b.LineTo(31.5f, 2.5f); b.LineTo(23f, 9f); b.Close();
            });
            Inked(c, fur, (k, p) => k.DrawPath(ears, p));
            using var inner = Build(b =>
            {
                b.MoveTo(7.5f, 13f); b.LineTo(9.5f, 6f); b.LineTo(13.5f, 9.5f); b.Close();
                b.MoveTo(32.5f, 13f); b.LineTo(30.5f, 6f); b.LineTo(26.5f, 9.5f); b.Close();
            });
            Fill(c, dark, (k, p) => k.DrawPath(inner, p));
            Inked(c, fur, (k, p) => k.DrawOval(20f, 22f, 15.5f, 14.5f, p));
        }

        if (!face)
        {
            return;
        }

        void Mask(SKCanvas k, SKPaint p)
        {
            k.DrawOval(20f, 28.5f, 10.5f, 8f, p);
            k.DrawOval(20f, 17f, 3.2f, 7f, p);
        }

        Fill(c, SKColors.White, Mask);
        Fill(c, dark, (k, p) => { k.DrawOval(13.5f, 15.5f, 3f, 1.2f, p); k.DrawOval(26.5f, 15.5f, 3f, 1.2f, p); });

        if (chomp)
        {
            ClosedEye(c, 13.5f, 20f);
            ClosedEye(c, 26.5f, 20f);
            Fill(c, Ink, (k, p) => k.DrawOval(20f, 24.5f, 2.8f, 2f, p));
            OpenMouth(c, 20f, 31f, 6f, 4.6f);
        }
        else
        {
            Fill(c, Ink, (k, p) => { k.DrawCircle(13.5f, 20f, 2.3f, p); k.DrawCircle(26.5f, 20f, 2.3f, p); });
            Shine(c, 14.2f, 19.2f, 0.8f);
            Shine(c, 27.2f, 19.2f, 0.8f);
            Fill(c, Ink, (k, p) => k.DrawOval(20f, 25.5f, 3f, 2.2f, p));
            Shine(c, 19.2f, 24.8f, 0.7f);
            Inked(c, new SKColor(0xFF, 0x6E, 0x86), (k, p) => k.DrawRoundRect(new SKRect(18.2f, 30f, 21.8f, 35.5f), 1.8f, 1.8f, p), 1.1f);
            Smile(c, 17.5f, 29.5f, 2.6f, 1.8f);
            Smile(c, 22.5f, 29.5f, 2.6f, 1.8f);
        }
    }

    private void Cat(SKCanvas c, bool chomp, bool head, bool face)
    {
        var fur = new SKColor(0xF4, 0x9C, 0x3A);
        var stripe = new SKColor(0xC2, 0x62, 0x14);
        if (head)
        {
            using var ears = Build(b =>
            {
                b.MoveTo(5f, 16f); b.LineTo(7f, 2.5f); b.LineTo(17f, 8.5f); b.Close();
                b.MoveTo(35f, 16f); b.LineTo(33f, 2.5f); b.LineTo(23f, 8.5f); b.Close();
            });
            Inked(c, fur, (k, p) => k.DrawPath(ears, p));
            using var inner = Build(b =>
            {
                b.MoveTo(8f, 12.5f); b.LineTo(9f, 6.5f); b.LineTo(13.5f, 9f); b.Close();
                b.MoveTo(32f, 12.5f); b.LineTo(31f, 6.5f); b.LineTo(26.5f, 9f); b.Close();
            });
            Fill(c, new SKColor(0xFF, 0xA8, 0xBC), (k, p) => k.DrawPath(inner, p));
            Inked(c, fur, (k, p) => k.DrawOval(20f, 22f, 15.5f, 14f, p));
            Fill(c, stripe, (k, p) =>
            {
                k.DrawRoundRect(new SKRect(18.8f, 8.5f, 21.2f, 14f), 1f, 1f, p);
                Oval(k, p, 14.5f, 10.5f, 1.1f, 2.6f, -25f);
                Oval(k, p, 25.5f, 10.5f, 1.1f, 2.6f, 25f);
            });
        }

        if (!face)
        {
            return;
        }

        Fill(c, new SKColor(0xFF, 0xEC, 0xD0), (k, p) => k.DrawOval(20f, 28.5f, 7.5f, 5.5f, p));
        if (chomp)
        {
            ClosedEye(c, 13.5f, 20f);
            ClosedEye(c, 26.5f, 20f);
            OpenMouth(c, 20f, 30f, 5.5f, 4.8f);
        }
        else
        {
            Inked(c, new SKColor(0x6C, 0xCC, 0x4C), (k, p) => { k.DrawOval(13.5f, 20f, 2.8f, 3.2f, p); k.DrawOval(26.5f, 20f, 2.8f, 3.2f, p); }, 1.2f);
            Fill(c, Ink, (k, p) => { k.DrawOval(13.5f, 20f, 0.9f, 2.7f, p); k.DrawOval(26.5f, 20f, 0.9f, 2.7f, p); });
            using var nose = Build(b => { b.MoveTo(18.3f, 25.5f); b.LineTo(21.7f, 25.5f); b.LineTo(20f, 27.4f); b.Close(); });
            Fill(c, new SKColor(0xFF, 0x7A, 0x96), (k, p) => k.DrawPath(nose, p));
            Smile(c, 18f, 28.5f, 2f, 1.8f);
            Smile(c, 22f, 28.5f, 2f, 1.8f);
            Outline(c, (k, p) =>
            {
                k.DrawLine(3f, 26f, 11f, 27f, p);
                k.DrawLine(3.5f, 30f, 11f, 29f, p);
                k.DrawLine(37f, 26f, 29f, 27f, p);
                k.DrawLine(36.5f, 30f, 29f, 29f, p);
            }, 0.9f);
        }
    }
}
