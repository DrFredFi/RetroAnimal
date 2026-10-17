using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Rebinds gameplay keys. Assigning a key already used by another action swaps the two.</summary>
internal sealed class ControlsScreen : IScreen
{
    private static readonly string[] Labels =
        ["Move Left", "Move Right", "Drop Faster", "Rotate Right", "Rotate Left", "Pause"];

    private readonly GameContext _context;
    private readonly MenuList _menu;
    private readonly int _resetIndex;
    private readonly int _backIndex;
    private double _time;
    private bool _waiting;

    public ControlsScreen(GameContext context)
    {
        _context = context;
        _menu = new MenuList([.. Labels, "Reset to Defaults", "Back"]);
        _resetIndex = Labels.Length;
        _backIndex = Labels.Length + 1;
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
                Assign(KeyboardBindings.Rebindable[_menu.Selected], key);
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
        ui.OutlinedText(canvas, "CONTROLS", 400, 98, 36, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 3);
        _menu.Draw(canvas, ui, 400, 160, 42, _time, 23);
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
        for (int i = 0; i < KeyboardBindings.Rebindable.Length; i++)
        {
            _menu.SetValue(i, KeyboardBindings.KeyName(_context.Input.Bindings.PrimaryKey(KeyboardBindings.Rebindable[i])));
        }
    }

    private void Leave()
    {
        _context.SaveSettings();
        _context.Audio.Play(Sound.MenuBack);
        _context.Screens.Pop();
    }
}
