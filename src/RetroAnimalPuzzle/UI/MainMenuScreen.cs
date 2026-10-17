using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

internal sealed class MainMenuScreen : IScreen
{
    private const int StartGame = 0, Versus = 1, HowToPlay = 2, Options = 3, HighScores = 4, Exit = 5;

    private readonly GameContext _context;
    private readonly MenuList _menu = new("Start Game", "Versus CPU", "How to Play", "Options", "High Scores", "Exit");
    private double _time;

    public MainMenuScreen(GameContext context)
    {
        _context = context;
    }

    public void OnEnter() => _context.Audio.PlayMusic(MusicTrack.Menu);

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        if (input.WasPressed(GameAction.Back))
        {
            _context.Audio.Play(Sound.MenuBack);
            _context.Screens.Replace(new TitleScreen(_context));
            return;
        }

        if (input.WasPressed(GameAction.Help))
        {
            _context.Screens.Push(new HelpScreen(_context));
            return;
        }

        switch (_menu.Update(input, _context.Audio))
        {
            case StartGame:
                _context.Screens.Replace(new EndlessScreen(_context));
                break;
            case Versus:
                _context.Screens.Replace(new VersusScreen(_context));
                break;
            case HowToPlay:
                _context.Screens.Push(new HelpScreen(_context));
                break;
            case Options:
                _context.Screens.Push(new OptionsScreen(_context));
                break;
            case HighScores:
                _context.Screens.Push(new HighScoresScreen(_context));
                break;
            case Exit:
                _context.Exit();
                break;
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        _context.Arena.DrawBackground(canvas);
        TitleArt.DrawLogo(canvas, ui, 400, 150, (float)_time, 0.75f);

        ui.Panel(canvas, new SKRect(220, 175, 580, 520));
        _menu.Draw(canvas, ui, 400, 232, 50, _time);

        var saves = _context.Saves.Data;
        ui.Text(canvas, $"Best (Endless): {saves.Best(GameMode.Endless)}    Best (Versus): {saves.Best(GameMode.Versus)}",
            400, 555, 16, Palette.TimerText, UiFont.Body);
        ui.Text(canvas, "Arrows: choose   Enter: confirm   Esc: back", 400, 582, 14, Palette.TimerText, UiFont.Body);
    }
}
