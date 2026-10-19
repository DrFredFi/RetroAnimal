using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>
/// Controller settings: turn controllers on or off, choose which field the first controller plays in a two-player
/// match, and rebind the gameplay buttons (one mapping shared by every controller). Assigning a button already used
/// by another action swaps the two.
/// </summary>
internal sealed class GamepadControlsScreen : IScreen
{
    private const int Enabled = 0, FirstPadSide = 1, Header = 2, FirstAction = 3;

    private static readonly string[] Labels = ["Move Left", "Move Right", "Drop Faster", "Rotate Right", "Rotate Left", "Pause"];

    private readonly GameContext _context;
    private readonly MenuList _menu;
    private readonly int _resetIndex;
    private readonly int _backIndex;
    private double _time;
    private bool _waiting;

    public GamepadControlsScreen(GameContext context)
    {
        _context = context;
        _menu = new MenuList(["Controller", "2P: 1st Controller", "- Buttons -", .. Labels, "Reset to Defaults", "Back"]);
        _menu.SetEnabled(Header, false);
        _resetIndex = FirstAction + Labels.Length;
        _backIndex = _resetIndex + 1;
        RefreshValues();
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        if (_waiting)
        {
            var button = _context.Input.LastPressedButton;
            if (_context.Input.LastPressedKey == Keys.Escape || button == GamepadButton.Back)
            {
                _waiting = false;
                _context.Audio.Play(Sound.MenuBack);
            }
            else if (button is { } b && GamepadBindings.IsBindable(b))
            {
                Assign(GamepadBindings.Rebindable[_menu.Selected - FirstAction], b);
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

        int dx = MenuList.Horizontal(input);
        if (dx != 0 && _menu.Selected is Enabled or FirstPadSide)
        {
            Toggle(_menu.Selected);
            _context.Audio.Play(Sound.MenuMove);
        }

        int choice = _menu.Update(input, _context.Audio);
        if (choice == _backIndex)
        {
            Leave();
        }
        else if (choice == _resetIndex)
        {
            _context.Settings.GamepadBindings.Clear();
            _context.ApplySettings();
            RefreshValues();
        }
        else if (choice is Enabled or FirstPadSide)
        {
            Toggle(choice);
        }
        else if (choice >= FirstAction)
        {
            _waiting = true;
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        _context.Arena.DrawBackground(canvas);
        ui.Panel(canvas, new SKRect(140, 40, 660, 560));
        ui.OutlinedText(canvas, "CONTROLLER", 400, 92, 34, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 3);

        var input = _context.Input;
        string status = !input.GamepadsEnabled ? "Controllers are off"
            : input.ConnectedPads switch
            {
                0 => "No controller connected (Xbox-compatible, XInput)",
                1 => "1 controller connected",
                var n => $"{n} controllers connected",
            };
        ui.Text(canvas, status, 400, 124, 15, Palette.TimerText, UiFont.Body);

        _menu.Draw(canvas, ui, 400, 164, 31, _time, 20);
        string hint = _waiting
            ? "Press a controller button...  (Esc / View: cancel)"
            : "Enter / A: change    Esc / B: save & back";
        ui.OutlinedText(canvas, hint, 400, 540, 17, _waiting ? new SKColor(0xFF, 0xE4, 0x5C) : Palette.TimerText,
            Palette.Outline, UiFont.Playful, 2);
    }

    private void Toggle(int index)
    {
        var s = _context.Settings;
        if (index == Enabled)
        {
            s.GamepadEnabled = !s.GamepadEnabled;
        }
        else
        {
            s.FirstGamepadPlaysLeft = !s.FirstGamepadPlaysLeft;
        }

        _context.ApplySettings();
        RefreshValues();
    }

    private void Assign(GameAction action, GamepadButton button)
    {
        var saved = _context.Settings.GamepadBindings;
        var bindings = _context.Input.PadBindings;
        var previous = bindings.PrimaryButton(action);

        // Persist every action explicitly so swaps are unambiguous.
        foreach (var a in GamepadBindings.Rebindable)
        {
            if (bindings.PrimaryButton(a) is { } current)
            {
                saved[a.ToString()] = current.ToString();
            }
        }

        foreach (var other in GamepadBindings.Rebindable)
        {
            if (other != action && bindings.PrimaryButton(other) == button)
            {
                if (previous is { } p)
                {
                    saved[other.ToString()] = p.ToString();
                }
                else
                {
                    saved.Remove(other.ToString());
                }
            }
        }

        saved[action.ToString()] = button.ToString();
        _context.ApplySettings();
        RefreshValues();
    }

    private void RefreshValues()
    {
        var s = _context.Settings;
        _menu.SetValue(Enabled, s.GamepadEnabled ? "On" : "Off");
        _menu.SetValue(FirstPadSide, s.FirstGamepadPlaysLeft ? "Left Player" : "Right Player");
        for (int i = 0; i < GamepadBindings.Rebindable.Length; i++)
        {
            _menu.SetValue(FirstAction + i, GamepadBindings.ButtonName(_context.Input.PadBindings.PrimaryButton(GamepadBindings.Rebindable[i])));
        }
    }

    private void Leave()
    {
        _context.SaveSettings();
        _context.Audio.Play(Sound.MenuBack);
        _context.Screens.Pop();
    }
}
