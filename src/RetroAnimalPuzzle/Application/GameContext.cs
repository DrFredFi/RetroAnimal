using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using RetroAnimalPuzzle.UI;

namespace RetroAnimalPuzzle.Application;

/// <summary>
/// Services and shared rendering resources handed to every screen. Owned and disposed by
/// <see cref="GameApplication"/>; screens never dispose anything they get from here.
/// </summary>
internal sealed class GameContext : IDisposable
{
    private readonly Action<bool> _setFullscreen;

    public GameContext(InputManager input, IAudioService audio, SettingsService settings, SaveService saves,
        Action<bool> setFullscreen, Action exit)
    {
        Input = input;
        Audio = audio;
        SettingsService = settings;
        Saves = saves;
        _setFullscreen = setFullscreen;
        Exit = exit;

        Fonts = new Fonts();
        Art = new BlockArt();
        Ui = new UiRenderer(Fonts, Art);
        Arena = new ArenaRenderer(Fonts);
        Mascot = new MascotRenderer();
        Playfield = new PlayfieldRenderer(Art);
        Screens = new ScreenManager();
    }

    public GameRules Rules { get; } = GameRules.Default;

    public InputManager Input { get; }

    public IAudioService Audio { get; }

    public SettingsService SettingsService { get; }

    public SettingsData Settings => SettingsService.Settings;

    public SaveService Saves { get; }

    public Fonts Fonts { get; }

    public BlockArt Art { get; }

    public UiRenderer Ui { get; }

    public ArenaRenderer Arena { get; }

    public MascotRenderer Mascot { get; }

    public PlayfieldRenderer Playfield { get; }

    public ScreenManager Screens { get; }

    public Action Exit { get; }

    /// <summary>Seconds since start-up; drives idle animations on every screen.</summary>
    public double Time { get; set; }

    public bool ReducedEffects => Settings.Animations == AnimationIntensity.Reduced;

    public static int NewSeed() => Random.Shared.Next();

    /// <summary>Pushes the current settings into audio, input and the window.</summary>
    public void ApplySettings()
    {
        var s = Settings;
        Audio.SetVolumes(s.MasterVolume / (float)SettingsData.MaxVolume, s.SfxVolume / (float)SettingsData.MaxVolume,
            s.MusicVolume / (float)SettingsData.MaxVolume);
        Input.Bindings = KeyboardBindings.FromSettings(s.KeyBindings);
        Input.PadBindings = GamepadBindings.FromSettings(s.GamepadBindings);
        Input.GamepadsEnabled = s.GamepadEnabled;
        Input.FirstPadPlaysLeft = s.FirstGamepadPlaysLeft;
        _setFullscreen(s.Fullscreen);
    }

    public void SaveSettings()
    {
        SettingsService.Save();
        ApplySettings();
    }

    public void Dispose()
    {
        Screens.Dispose();
        Playfield.Dispose();
        Mascot.Dispose();
        Arena.Dispose();
        Ui.Dispose();
        Art.Dispose();
        Fonts.Dispose();
    }
}
