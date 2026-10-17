using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Settings: volumes, effects, ghost, window mode, CPU level and key bindings. Saved on exit.</summary>
internal sealed class OptionsScreen : IScreen
{
    private const int Master = 0, Effects = 1, Music = 2, Animations = 3, Ghost = 4, Display = 5, Cpu = 6, Controls = 7, Back = 8;

    private readonly GameContext _context;
    private readonly MenuList _menu = new("Master Volume", "Effects Volume", "Music Volume", "Animations", "Ghost Piece",
        "Display", "CPU Level", "Controls...", "Back");
    private double _time;

    public OptionsScreen(GameContext context)
    {
        _context = context;
        RefreshValues();
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        if (input.WasPressed(GameAction.Back))
        {
            Leave();
            return;
        }

        int dx = MenuList.Horizontal(input);
        if (dx != 0 && Adjust(_menu.Selected, dx))
        {
            _context.ApplySettings();
            RefreshValues();
            _context.Audio.Play(_menu.Selected is Effects or Master ? Sound.Eat : Sound.MenuMove);
        }

        switch (_menu.Update(input, _context.Audio))
        {
            case Controls:
                _context.Screens.Push(new ControlsScreen(_context));
                break;
            case Back:
                Leave();
                break;
            case >= 0 and var index:
                if (Adjust(index, 1))
                {
                    _context.ApplySettings();
                    RefreshValues();
                }

                break;
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        _context.Arena.DrawBackground(canvas);
        ui.Panel(canvas, new SKRect(120, 40, 680, 560));
        ui.OutlinedText(canvas, "OPTIONS", 400, 98, 36, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 3);
        _menu.Draw(canvas, ui, 400, 152, 44, _time, 23);
        ui.Text(canvas, "Up / Down: choose    Left / Right: change    Esc: save & back", 400, 540, 14, Palette.TimerText, UiFont.Body);
    }

    private bool Adjust(int index, int delta)
    {
        var s = _context.Settings;
        switch (index)
        {
            case Master: s.MasterVolume = Math.Clamp(s.MasterVolume + delta, 0, SettingsData.MaxVolume); return true;
            case Effects: s.SfxVolume = Math.Clamp(s.SfxVolume + delta, 0, SettingsData.MaxVolume); return true;
            case Music: s.MusicVolume = Math.Clamp(s.MusicVolume + delta, 0, SettingsData.MaxVolume); return true;
            case Animations: s.Animations = s.Animations == AnimationIntensity.Full ? AnimationIntensity.Reduced : AnimationIntensity.Full; return true;
            case Ghost: s.ShowGhost = !s.ShowGhost; return true;
            case Display: s.Fullscreen = !s.Fullscreen; return true;
            case Cpu:
                int count = Enum.GetValues<CpuDifficulty>().Length;
                s.CpuDifficulty = (CpuDifficulty)(((int)s.CpuDifficulty + delta + count) % count);
                return true;
            default:
                return false;
        }
    }

    private void RefreshValues()
    {
        var s = _context.Settings;
        _menu.SetValue(Master, Bar(s.MasterVolume));
        _menu.SetValue(Effects, Bar(s.SfxVolume));
        _menu.SetValue(Music, Bar(s.MusicVolume));
        _menu.SetValue(Animations, s.Animations.ToString());
        _menu.SetValue(Ghost, s.ShowGhost ? "On" : "Off");
        _menu.SetValue(Display, s.Fullscreen ? "Fullscreen" : "Window");
        _menu.SetValue(Cpu, s.CpuDifficulty.ToString());
    }

    private static string Bar(int value) => new string('|', value) + new string('.', SettingsData.MaxVolume - value);

    private void Leave()
    {
        _context.SaveSettings();
        _context.Audio.Play(Sound.MenuBack);
        _context.Screens.Pop();
    }
}
