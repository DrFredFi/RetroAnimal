namespace RetroAnimalPuzzle.Core;

/// <summary>
/// Maps the fixed logical resolution onto an arbitrary window size, preserving the aspect ratio
/// and letterboxing the unused area.
/// </summary>
public readonly record struct Viewport(float Scale, float OffsetX, float OffsetY)
{
    public const float LogicalWidth = 800f;
    public const float LogicalHeight = 600f;

    public static Viewport Fit(int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0)
        {
            return new Viewport(1f, 0f, 0f);
        }

        float scale = Math.Min(windowWidth / LogicalWidth, windowHeight / LogicalHeight);
        float offsetX = (windowWidth - LogicalWidth * scale) / 2f;
        float offsetY = (windowHeight - LogicalHeight * scale) / 2f;
        return new Viewport(scale, offsetX, offsetY);
    }

    public (float X, float Y) ToLogical(float screenX, float screenY) =>
        ((screenX - OffsetX) / Scale, (screenY - OffsetY) / Scale);

    public (float X, float Y) ToScreen(float logicalX, float logicalY) =>
        (logicalX * Scale + OffsetX, logicalY * Scale + OffsetY);
}
