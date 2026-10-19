using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Host;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using RetroAnimalPuzzle.UI;
using SkiaSharp.Views.Desktop;

namespace RetroAnimalPuzzle.Application;

/// <summary>
/// Composition root: loads settings and saves, wires the window, input, audio, game loop, renderer and
/// screens, and owns their lifetimes.
/// </summary>
internal sealed class GameApplication : IDisposable
{
    private readonly GameForm _form = new();
    private readonly InputManager _input;
    private readonly IAudioService _audio;
    private readonly GameRenderer _renderer = new();
    private readonly GameContext _context;
    private readonly GameLoop _loop;

    public GameApplication()
    {
        string dataDirectory = JsonStore.DefaultDirectory;
        var settings = new SettingsService(dataDirectory);
        settings.Load();
        var saves = new SaveService(dataDirectory);
        saves.Load();

        _input = new InputManager(KeyboardBindings.FromSettings(settings.Settings.KeyBindings), new XInputGamepads());
        _audio = AudioService.CreateOrSilent();
        _context = new GameContext(_input, _audio, settings, saves, SetFullscreen, () => _form.Close());
        _context.ApplySettings();
        _context.Screens.Start(new TitleScreen(_context));

        _loop = new GameLoop(Update, Render);

        _form.Surface.PaintSurface += OnPaintSurface;
        _form.Surface.KeyDown += OnKeyDown;
        _form.Surface.KeyUp += (_, e) => _input.OnKeyUp(e.KeyCode);
        _form.Activated += (_, _) => _input.WindowActive = true;
        _form.Deactivate += (_, _) =>
        {
            _input.WindowActive = false;
            _input.ReleaseAll();
            _context.Screens.NotifyFocusLost();
        };
        _form.Shown += (_, _) => _loop.Start();
        _form.FormClosing += (_, _) => _loop.Stop();
    }

    public void Run() => System.Windows.Forms.Application.Run(_form);

    public void Dispose()
    {
        _loop.Dispose();
        _context.Dispose();
        _audio.Dispose();
        _renderer.Dispose();
        _form.Dispose();
    }

    private void SetFullscreen(bool fullscreen)
    {
        if (_form.IsFullscreen != fullscreen)
        {
            _form.ToggleFullscreen();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Window-level shortcuts are handled here rather than as game actions.
        if (e.KeyCode == Keys.F11 || (e.KeyCode == Keys.Enter && e.Alt))
        {
            _context.Settings.Fullscreen = !_form.IsFullscreen;
            _context.SaveSettings();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Menu || e.KeyCode == Keys.F10)
        {
            // Keep Alt / F10 from activating the system menu, which would stall the loop.
            e.SuppressKeyPress = true;
            return;
        }

        _input.OnKeyDown(e.KeyCode);
        e.SuppressKeyPress = true;
    }

    private void Update(double deltaSeconds)
    {
        var state = _input.Snapshot();
        if (state.WasPressed(GameAction.ToggleDebugOverlay))
        {
            _renderer.ShowDebugOverlay = !_renderer.ShowDebugOverlay;
        }

        _context.Time += deltaSeconds;
        _context.Screens.Update(deltaSeconds, state);
    }

    private void Render()
    {
        _form.Surface.Invalidate();
        _form.Surface.Update();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e) =>
        _renderer.Render(e.Surface.Canvas, e.Info.Size, _context.Screens, _loop.FramesPerSecond);
}
