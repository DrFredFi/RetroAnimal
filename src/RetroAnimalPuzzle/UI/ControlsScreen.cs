using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>
/// Rebinds gameplay keys, in two groups: the main keys (solo, versus CPU, right player in a two-player match) and
/// the left player's keys for a two-player match. Assigning a key already used by another action swaps the two.
/// </summary>
internal sealed class ControlsScreen : IScreen
{
    private static readonly string[] Labels =
    [
        "Move Left", "Move Right", "Drop Faster", "Rotate Right", "Rotate Left", "Pause",
        "Move Left", "Move Right", "Drop Faster", "Rotate Right", "Rotate Left",
    ];

    /// <summary>Where the left player's group starts in <see cref="KeyboardBindings.Rebindable"/>.</summary>
    private static readonly int LeftPlayerStart = Array.IndexOf(KeyboardBindings.Rebindable, GameAction.LeftPlayerMoveLeft);

    private readonly GameContext _context;
    private readonly MenuList _menu;
    private readonly GameAction?[] _rows;
    private readonly int _resetIndex;
    private readonly int _backIndex;
    private double _time;
    private bool _waiting;

    public ControlsScreen(GameContext context)
    {
        _context = context;
        // Group headers are disabled menu entries, so the selection skips them.
        var actions = KeyboardBindings.Rebindable;
        _rows = [null, .. actions[..LeftPlayerStart], null, .. actions[LeftPlayerStart..], null, null];
        _menu = new MenuList(
        [
            "- One player / Right player -", .. Labels[..LeftPlayerStart],
            "- Left player (2 Players) -", .. Labels[LeftPlayerStart..],
            "Reset to Defaults", "Back",
        ]);
        _menu.SetEnabled(0, false);
        _menu.SetEnabled(LeftPlayerStart + 1, false);
        _menu.Selected = 1;
        _resetIndex = _rows.Length - 2;
        _backIndex = _rows.Length - 1;
        RefreshValues();
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        if (_waiting)
        {
            var key = _context.Input.LastPressedKey;
            if (key == Keys.Escape)
            {
                _waiting = false;
                _context.Audio.Play(Sound.MenuBack);
            }
            else if (key != Keys.None && KeyboardBindings.IsBindable(key))
            {
                Assign(_rows[_menu.Selected]!.Value, key);
                _waiting = false;
                _context.Audio.Play(Sound.MenuSelect);
            }

            return;
        }

        if (input.WasPressed(GameAction.Back))
        {
            Leave();
            return;
        }

        int choice = _menu.Update(input, _context.Audio);
        if (choice == _backIndex)
        {
            Leave();
        }
        else if (choice == _resetIndex)
        {
            _context.Settings.KeyBindings.Clear();
            _context.ApplySettings();
            RefreshValues();
        }
        else if (choice >= 0)
        {
            _waiting = true;
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        _context.Arena.DrawBackground(canvas);
        ui.Panel(canvas, new SKRect(140, 40, 660, 560));
        ui.OutlinedText(canvas, "KEYBOARD", 400, 88, 34, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 3);
        _menu.Draw(canvas, ui, 400, 126, 27, _time, 18);
        string hint = _waiting ? "Press the new key...  (Esc: cancel)" : "Enter: change key    Esc: save & back";
        ui.OutlinedText(canvas, hint, 400, 540, 17, _waiting ? new SKColor(0xFF, 0xE4, 0x5C) : Palette.TimerText,
            Palette.Outline, UiFont.Playful, 2);
    }

    private void Assign(GameAction action, Keys key)
    {
        var saved = _context.Settings.KeyBindings;
        var bindings = _context.Input.Bindings;
        var previous = bindings.PrimaryKey(action);

        // Persist every rebindable action explicitly so swaps are unambiguous.
        foreach (var a in KeyboardBindings.Rebindable)
        {
            saved[a.ToString()] = bindings.PrimaryKey(a).ToString();
        }

        foreach (var other in KeyboardBindings.Rebindable)
        {
            if (other != action && bindings.PrimaryKey(other) == key)
            {
                saved[other.ToString()] = previous.ToString();
            }
        }

        saved[action.ToString()] = key.ToString();
        _context.ApplySettings();
        RefreshValues();
    }

    private void RefreshValues()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] is { } action)
            {
                _menu.SetValue(i, KeyboardBindings.KeyName(_context.Input.Bindings.PrimaryKey(action)));
            }
        }
    }

    private void Leave()
    {
        _context.SaveSettings();
        _context.Audio.Play(Sound.MenuBack);
        _context.Screens.Pop();
    }
}
