using SkiaSharp.Views.Desktop;

namespace RetroAnimalPuzzle.Host;

/// <summary>SkiaSharp drawing surface that receives every gameplay key, including arrows and Tab.</summary>
internal sealed class GameSurface : SKControl
{
    public GameSurface()
    {
        TabStop = true;
        Dock = DockStyle.Fill;
    }

    protected override bool IsInputKey(Keys keyData) => true;
}
