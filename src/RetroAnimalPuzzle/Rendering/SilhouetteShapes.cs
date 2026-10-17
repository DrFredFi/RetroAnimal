using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>Simple food silhouettes centred on the origin, used for the embossed background pattern.</summary>
internal static class SilhouetteShapes
{
    public static SKPath Bone()
    {
        var path = new SKPathBuilder { FillType = SKPathFillType.Winding };
        path.AddRoundRect(new SKRect(-22f, -5f, 22f, 5f), 3f, 3f);
        path.AddCircle(-24f, -6f, 7f);
        path.AddCircle(-24f, 6f, 7f);
        path.AddCircle(24f, -6f, 7f);
        path.AddCircle(24f, 6f, 7f);
        return path.Detach();
    }

    public static SKPath Carrot()
    {
        var path = new SKPathBuilder { FillType = SKPathFillType.Winding };
        path.MoveTo(-10f, -14f);
        path.QuadTo(0f, -20f, 10f, -14f);
        path.QuadTo(6f, 8f, 0f, 28f);
        path.QuadTo(-6f, 8f, -10f, -14f);
        path.Close();
        path.AddOval(new SKRect(-12f, -32f, -2f, -14f));
        path.AddOval(new SKRect(-4f, -36f, 4f, -14f));
        path.AddOval(new SKRect(2f, -32f, 12f, -14f));
        return path.Detach();
    }

    public static SKPath Banana()
    {
        var path = new SKPathBuilder();
        path.MoveTo(-26f, -8f);
        path.QuadTo(0f, 30f, 26f, -8f);
        path.LineTo(28f, -12f);
        path.QuadTo(0f, 12f, -24f, -12f);
        path.Close();
        return path.Detach();
    }

    public static SKPath Fish()
    {
        var path = new SKPathBuilder { FillType = SKPathFillType.Winding };
        path.AddOval(new SKRect(-22f, -11f, 14f, 11f));
        path.MoveTo(10f, 0f);
        path.LineTo(28f, -12f);
        path.LineTo(28f, 12f);
        path.Close();
        return path.Detach();
    }

    public static SKPath Bamboo()
    {
        var path = new SKPathBuilder { FillType = SKPathFillType.Winding };
        path.AddRoundRect(new SKRect(-6f, -30f, 6f, -11f), 3f, 3f);
        path.AddRoundRect(new SKRect(-6f, -9f, 6f, 9f), 3f, 3f);
        path.AddRoundRect(new SKRect(-6f, 11f, 6f, 30f), 3f, 3f);
        path.AddOval(new SKRect(4f, -24f, 18f, -16f));
        return path.Detach();
    }
}
