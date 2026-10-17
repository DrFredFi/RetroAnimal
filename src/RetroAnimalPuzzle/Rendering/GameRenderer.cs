using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.UI;
using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Top-level renderer: fits the 800×600 logical canvas into the window (letterboxed),
/// lets the active screen draw, then overlays debug information in window pixels.
/// </summary>
internal sealed class GameRenderer : IDisposable
{
    private readonly SKPaint _debugText = new() { IsAntialias = true, Color = SKColors.Yellow };
    private readonly SKPaint _debugBackground = new() { Color = SKColors.Black.WithAlpha(160) };
    private readonly SKFont _debugFont = new(SKTypeface.FromFamilyName("Consolas"), 14f);

    public bool ShowDebugOverlay { get; set; }

    public void Render(SKCanvas canvas, SKSizeI windowSize, ScreenManager screens, double framesPerSecond)
    {
        var viewport = Viewport.Fit(windowSize.Width, windowSize.Height);

        canvas.Clear(Palette.Letterbox);
        canvas.Save();
        canvas.Translate(viewport.OffsetX, viewport.OffsetY);
        canvas.Scale(viewport.Scale);
        canvas.ClipRect(new SKRect(0, 0, Viewport.LogicalWidth, Viewport.LogicalHeight));
        screens.Render(canvas);
        canvas.Restore();

        if (ShowDebugOverlay)
        {
            string line = $"{framesPerSecond:0.0} fps  {windowSize.Width}x{windowSize.Height}  scale {viewport.Scale:0.00}";
            canvas.DrawRect(new SKRect(4, 4, 340, 26), _debugBackground);
            canvas.DrawText(line, 10, 20, SKTextAlign.Left, _debugFont, _debugText);
        }
    }

    public void Dispose()
    {
        _debugText.Dispose();
        _debugBackground.Dispose();
        _debugFont.Typeface?.Dispose();
        _debugFont.Dispose();
    }
}
