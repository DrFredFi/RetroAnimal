using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>
/// Shared behaviour of the two play screens: pause menu with confirmation, auto-pause on focus loss,
/// turning gameplay events into sounds/particles/announcements, and the mascot's mood.
/// </summary>
internal abstract class PlayScreenBase : IScreen, IDisposable
{
    private const int Resume = 0, Restart = 1, Options = 2, HowToPlay = 3, MainMenu = 4;

    private readonly MenuList _pauseMenu = new("Resume", "Restart", "Options", "How to Play", "Main Menu");
    private readonly MenuList _confirmMenu = new("Yes", "No");
    private Action? _confirmAction;
    private string _confirmText = "";
    private MascotMood _tempMood = MascotMood.Idle;
    private double _tempMoodTimer;

    protected PlayScreenBase(GameContext context)
    {
        Context = context;
        LeftEffects.Configure(context.ReducedEffects);
        RightEffects.Configure(context.ReducedEffects);
    }

    protected GameContext Context { get; }

    protected FieldEffects LeftEffects { get; } = new(HeartTarget(ArenaLayout.LeftHeart));

    protected FieldEffects RightEffects { get; } = new(HeartTarget(ArenaLayout.RightHeart));

    /// <summary>The board shown in the left field (always a human player).</summary>
    protected abstract PlayerBoard LeftBoard { get; }

    /// <summary>The board shown in the right field, if any.</summary>
    protected abstract PlayerBoard? RightBoard { get; }

    protected double Time { get; private set; }

    protected bool Paused { get; private set; }

    /// <summary>Whether a person plays the right field (full-volume sounds, ghost piece) rather than the CPU.</summary>
    protected virtual bool RightIsHuman => false;

    /// <summary>Whether the pause menu may open right now (not during end-of-game panels).</summary>
    protected abstract bool CanPause { get; }

    public virtual void OnEnter()
    {
        Context.Audio.PlayMusic(MusicTrack.Game);
        LeftEffects.Configure(Context.ReducedEffects);
        RightEffects.Configure(Context.ReducedEffects);
    }

    public void OnFocusLost()
    {
        if (CanPause && !Paused)
        {
            OpenPause();
        }
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        Time += deltaSeconds;
        if (Paused)
        {
            UpdatePause(input);
            return;
        }

        if (CanPause && input.WasPressed(GameAction.Pause))
        {
            OpenPause();
            return;
        }

        if (CanPause && input.WasPressed(GameAction.Help))
        {
            OpenPause();
            Context.Screens.Push(new HelpScreen(Context));
            return;
        }

        UpdateGame(deltaSeconds, input);
        UpdateEffects(LeftEffects, LeftBoard, ArenaLayout.LeftField, deltaSeconds, human: true);
        UpdateEffects(RightEffects, RightBoard, ArenaLayout.RightField, deltaSeconds, RightIsHuman);
        _tempMoodTimer -= deltaSeconds;
    }

    public void Render(SKCanvas canvas)
    {
        RenderGame(canvas);
        if (Paused)
        {
            RenderPause(canvas);
        }
    }

    public virtual void Dispose()
    {
        LeftEffects.Dispose();
        RightEffects.Dispose();
    }

    protected abstract void UpdateGame(double deltaSeconds, in InputState input);

    protected abstract void RenderGame(SKCanvas canvas);

    /// <summary>Starts a brand-new game of the same kind.</summary>
    protected abstract void RestartGame();

    protected void ShowMood(MascotMood mood, double seconds)
    {
        _tempMood = mood;
        _tempMoodTimer = seconds;
    }

    /// <summary>A temporary reaction if one is active, otherwise the given steady mood.</summary>
    protected MascotMood MoodOr(MascotMood steady) => _tempMoodTimer > 0 ? _tempMood : steady;

    protected static MascotMood SteadyMoodFor(PlayerBoard board)
    {
        var rules = board.Rules;
        int highest = 0;
        for (int x = 0; x < rules.Columns; x++)
        {
            highest = Math.Max(highest, board.Grid.ColumnHeight(x));
        }

        return highest >= rules.TotalRows - 3 ? MascotMood.Worried : MascotMood.Idle;
    }

    /// <summary>Draws a board with its effects; the field contents shake, the frame does not.</summary>
    protected void DrawField(SKCanvas canvas, PlayerBoard board, SKRect field, FieldEffects effects, bool human = true)
    {
        var shake = effects.Animations.ShakeOffset;
        canvas.Save();
        canvas.Translate(shake.X, shake.Y);
        Context.Playfield.Draw(canvas, board, field, effects.Visuals, Time, human && Context.Settings.ShowGhost);
        canvas.Restore();
        effects.Animations.Draw(canvas, field, Context.Ui);
    }

    /// <summary>Particles and flying animals: drawn last, above the HUD, since they travel to the hearts.</summary>
    protected void DrawFlyingEffects(SKCanvas canvas)
    {
        float tile = PlayfieldRenderer.TileSize(Context.Rules, ArenaLayout.LeftField);
        foreach (var effects in new[] { LeftEffects, RightEffects })
        {
            effects.Particles.Draw(canvas);
            effects.DrawFlights(canvas, Context.Art, tile);
        }
    }

    private void UpdateEffects(FieldEffects effects, PlayerBoard? board, SKRect field, double deltaSeconds, bool human)
    {
        var (bites, arrivals) = effects.Update(deltaSeconds, board);
        float volume = human ? 1f : 0.45f;
        if (bites > 0 && board?.PendingClear is { } clear)
        {
            Context.Audio.Play(Sound.Chew, 0.95f + (float)Random.Shared.NextDouble() * 0.15f, volume);
            foreach (var route in clear.Groups.SelectMany(g => g.Routes))
            {
                var (x, y, _, _) = EatTimeline.Position(board.Rules, route, board.PhaseElapsed);
                var p = PlayfieldRenderer.CellCenter(board.Rules, field, x, y);
                var food = route.Path.Count > 0 ? board.Grid[route.Path[0]] : board.Grid[route.Animal];
                effects.Particles.Burst(p.X, p.Y + 8, ColorOf(food), 4, 90f);
            }
        }

        if (arrivals > 0)
        {
            Context.Audio.Play(Sound.HeartArrive, 1f, volume);
        }
    }

    private static SKPoint HeartTarget(SKRect heart) => new(heart.MidX, heart.Top + 30f);

    /// <summary>Turns board events into sound, particles, popups and announcements.</summary>
    protected void React(PlayerBoard board, SKRect field, FieldEffects effects, bool human)
    {
        var audio = Context.Audio;
        float volume = human ? 1f : 0.45f;
        foreach (var e in board.Events)
        {
            switch (e.Type)
            {
                case GameEventType.PieceMoved when human:
                    audio.Play(Sound.Move);
                    break;
                case GameEventType.PieceRotated when human:
                    audio.Play(Sound.Rotate);
                    break;
                case GameEventType.PieceLanded:
                    audio.Play(Sound.Land, 1f, volume * 0.8f);
                    break;
                case GameEventType.HardDrop when human:
                    audio.Play(Sound.HardDrop);
                    effects.Animations.Shake(2f);
                    break;
                case GameEventType.Eat when e.Clear is { } clear:
                    OnEat(board, field, effects, clear, e.Chain, e.Value, volume, human);
                    break;
                case GameEventType.GarbageSent:
                    audio.Play(e.Value >= 10 ? Sound.SendLarge : e.Value >= 4 ? Sound.SendMedium : Sound.SendSmall, 1f, human ? 1f : 0.7f);
                    break;
                case GameEventType.GarbageDropped:
                    audio.Play(Sound.GarbageDrop, 1f, volume);
                    effects.Animations.Shake(4f);
                    break;
            }
        }
    }

    private void OnEat(PlayerBoard board, SKRect field, FieldEffects effects, ClearResult clear, int chain, int points,
        float volume, bool human)
    {
        var audio = Context.Audio;
        audio.Play(Sound.Eat, 1f + 0.08f * (chain - 1), volume);
        if (chain >= 2)
        {
            audio.Play(Sound.Chain, 1f + 0.1f * (chain - 2), volume);
        }

        if (clear.Coins.Count > 0)
        {
            audio.Play(Sound.Coin, 1f, volume);
            effects.Animations.Announce("COIN!", new SKColor(0xFF, 0xD8, 0x40), "BAKU!", 1.0f);
        }

        // Eaters walk over their food and then fly to the heart, where the points are counted.
        effects.QueueFlights(board, field, clear, points);
        if (clear.Groups.Count == 0)
        {
            effects.Animations.AddPopup($"+{points}", field.MidX, field.MidY, SKColors.White);
        }

        foreach (var coin in clear.Coins)
        {
            foreach (var cell in coin.Cleared)
            {
                var center = PlayfieldRenderer.CellCenter(board.Rules, field, cell);
                effects.Particles.Burst(center.X, center.Y, ColorOf(board.Grid[cell]), 6, 150f);
            }
        }

        foreach (var group in clear.Groups)
        {
            foreach (var p in group.Animals)
            {
                var center = PlayfieldRenderer.CellCenter(board.Rules, field, p);
                effects.Particles.Burst(center.X, center.Y, new SKColor(0xFF, 0xF0, 0x80), chain >= 2 ? 8 + chain * 3 : 5, 160f, stars: true);
            }
        }

        var (title, subtitle) = chain switch
        {
            1 when clear.Groups.Count >= 2 => ($"COMBO x{clear.Groups.Count}!", null),
            1 => (null, null),
            2 => ("NICE!", "CHAIN x2"),
            3 => ("GREAT!", "CHAIN x3"),
            _ => ($"CHAIN x{chain}!", "AMAZING!"),
        };

        if (title is not null)
        {
            effects.Animations.Announce(title, chain >= 3 ? new SKColor(0xFF, 0x7C, 0xD8) : new SKColor(0xFF, 0xE4, 0x5C), subtitle);
        }

        if (chain >= 3)
        {
            effects.Animations.Shake(3f + chain);
        }

        if (human && (chain >= 2 || clear.Groups.Count >= 2 || clear.FoodCleared >= 5))
        {
            ShowMood(MascotMood.Cheer, 1.6);
        }
    }

    protected static SKColor ColorOf(Block block) => block.Kind switch
    {
        BlockKind.Food => block.Food switch
        {
            FoodType.Banana => new SKColor(0xFF, 0xE0, 0x40),
            FoodType.Carrot => new SKColor(0xFF, 0x8C, 0x1E),
            FoodType.Bamboo => new SKColor(0x8C, 0xE0, 0x50),
            FoodType.Bone => new SKColor(0xFF, 0xF8, 0xE8),
            _ => new SKColor(0x9C, 0xD8, 0xF4),
        },
        BlockKind.Animal => block.Animal switch
        {
            AnimalType.Monkey => new SKColor(0xB0, 0x6C, 0x34),
            AnimalType.Rabbit => SKColors.White,
            AnimalType.Panda => new SKColor(0x40, 0x40, 0x48),
            AnimalType.Dog => new SKColor(0x8C, 0x9C, 0xC4),
            _ => new SKColor(0xF4, 0x9C, 0x3A),
        },
        _ => new SKColor(0xFF, 0xD0, 0x30),
    };

    private void OpenPause()
    {
        Paused = true;
        _pauseMenu.Selected = 0;
        _confirmAction = null;
        Context.Input.ReleaseAll();
        Context.Audio.Play(Sound.Pause);
    }

    private void UpdatePause(in InputState input)
    {
        if (_confirmAction is not null)
        {
            if (input.WasPressed(GameAction.Back))
            {
                _confirmAction = null;
                Context.Audio.Play(Sound.MenuBack);
                return;
            }

            int choice = _confirmMenu.Update(input, Context.Audio);
            if (choice == 0)
            {
                var action = _confirmAction;
                _confirmAction = null;
                action();
            }
            else if (choice == 1)
            {
                _confirmAction = null;
            }

            return;
        }

        if (input.WasPressed(GameAction.Pause) || input.WasPressed(GameAction.Back))
        {
            Paused = false;
            Context.Audio.Play(Sound.Pause);
            return;
        }

        switch (_pauseMenu.Update(input, Context.Audio))
        {
            case Resume:
                Paused = false;
                break;
            case Restart:
                Confirm("Restart and lose this game?", () =>
                {
                    Paused = false;
                    LeftEffects.Clear();
                    RightEffects.Clear();
                    RestartGame();
                });
                break;
            case Options:
                Context.Screens.Push(new OptionsScreen(Context));
                break;
            case HowToPlay:
                Context.Screens.Push(new HelpScreen(Context));
                break;
            case MainMenu:
                Confirm("Quit to the main menu?", () => Context.Screens.Replace(new MainMenuScreen(Context)));
                break;
        }
    }

    private void Confirm(string text, Action action)
    {
        _confirmText = text;
        _confirmAction = action;
        _confirmMenu.Selected = 1;
    }

    private void RenderPause(SKCanvas canvas)
    {
        var ui = Context.Ui;
        ui.Dim(canvas, 150);
        if (_confirmAction is not null)
        {
            ui.Panel(canvas, new SKRect(190, 210, 610, 400));
            ui.OutlinedText(canvas, _confirmText, 400, 265, 22, SKColors.White, Palette.Outline, UiFont.Playful);
            _confirmMenu.Draw(canvas, ui, 400, 320, 44, Time, 26);
            return;
        }

        ui.Panel(canvas, new SKRect(250, 140, 550, 470));
        ui.OutlinedText(canvas, "PAUSE", 400, 195, 40, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 4);
        _pauseMenu.Draw(canvas, ui, 400, 252, 46, Time, 25);
    }
}
