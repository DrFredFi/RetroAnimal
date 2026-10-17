using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Keyboard-driven vertical menu state: selection with wrap-around, confirm, and left/right adjustments.</summary>
internal sealed class MenuList
{
    private readonly List<MenuEntry> _items;

    public MenuList(params string[] labels)
    {
        _items = labels.Select(l => new MenuEntry(l)).ToList();
    }

    public int Selected { get; set; }

    public IReadOnlyList<MenuEntry> Items => _items;

    public void SetValue(int index, string? value) => _items[index] = _items[index] with { Value = value };

    public void SetEnabled(int index, bool enabled) => _items[index] = _items[index] with { Enabled = enabled };

    /// <summary>Moves the selection and returns the confirmed index, or -1.</summary>
    public int Update(in InputState input, IAudioService audio)
    {
        if (input.WasPressed(GameAction.MenuUp)) Move(-1, audio);
        if (input.WasPressed(GameAction.MenuDown)) Move(1, audio);

        if (input.WasPressed(GameAction.Confirm) && _items[Selected].Enabled)
        {
            audio.Play(Sound.MenuSelect);
            return Selected;
        }

        return -1;
    }

    /// <summary>-1 / +1 when left / right is pressed (for adjusting option values), otherwise 0.</summary>
    public static int Horizontal(in InputState input) =>
        input.WasPressed(GameAction.MenuLeft) ? -1 : input.WasPressed(GameAction.MenuRight) ? 1 : 0;

    public void Draw(SKCanvas canvas, UiRenderer ui, float centerX, float top, float spacing, double time, float size = 26f) =>
        ui.Menu(canvas, _items, Selected, centerX, top, spacing, time, size);

    private void Move(int delta, IAudioService audio)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            Selected = (Selected + delta + _items.Count) % _items.Count;
            if (_items[Selected].Enabled)
            {
                break;
            }
        }

        audio.Play(Sound.MenuMove);
    }
}
