using RetroAnimalPuzzle.Input;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>One full-window screen (title, menu, game...). Drawing happens in 800×600 logical coordinates.</summary>
internal interface IScreen
{
    void Update(double deltaSeconds, in InputState input);

    void Render(SKCanvas canvas);

    /// <summary>Called when the screen becomes the active (top) screen.</summary>
    void OnEnter()
    {
    }

    /// <summary>Called when the window loses focus while this screen is active.</summary>
    void OnFocusLost()
    {
    }
}
