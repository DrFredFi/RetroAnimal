using System.Globalization;
using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Single-player endless mode: one field on the left, an info panel on the right.</summary>
internal sealed class EndlessScreen : PlayScreenBase
{
    private const double GameOverPanelDelay = 1.4;

    private readonly MenuList _overMenu = new("Retry", "Main Menu");
    private SinglePlayerSession _session = null!;
    private double _overTimer;
    private bool _recorded;
    private int _rank;
    private int _bestAtStart;

    public EndlessScreen(GameContext context) : base(context)
    {
        RestartGame();
    }

    protected override bool CanPause => !_session.IsOver;

    protected override PlayerBoard LeftBoard => _session.Board;

    protected override PlayerBoard? RightBoard => null;

    protected override void RestartGame()
    {
        _session = new SinglePlayerSession(Context.Rules, GameContext.NewSeed());
        _overTimer = 0;
        _recorded = false;
        _rank = 0;
        _bestAtStart = Context.Saves.Data.Best(GameMode.Endless);
        _overMenu.Selected = 0;
        LeftEffects.Clear();
        LeftEffects.Animations.Announce("READY?", new SKColor(0xFF, 0xE4, 0x5C), "LEVEL 1", 1.4f);
        Context.Audio.Play(Sound.Ready);
    }

    protected override void UpdateGame(double deltaSeconds, in InputState input)
    {
        if (!_session.IsOver)
        {
            _session.Update(deltaSeconds, input);
            React(_session.Board, ArenaLayout.LeftField, LeftEffects, human: true);
            foreach (var e in _session.Events)
            {
                if (e.Type == GameEventType.LevelUp)
                {
                    Context.Audio.Play(Sound.LevelUp);
                    LeftEffects.Animations.Announce("LEVEL UP!", new SKColor(0x7C, 0xF0, 0xFF), $"LEVEL {e.Value}", 1.6f);
                    ShowMood(MascotMood.Celebrate, 2.0);
                }
            }

            if (_session.IsOver)
            {
                Context.Audio.Play(Sound.GameOver);
                LeftEffects.Animations.Announce("GAME OVER", new SKColor(0xFF, 0x60, 0x60), null, 3f);
                Record();
            }

            return;
        }

        _overTimer += deltaSeconds;
        if (_overTimer < GameOverPanelDelay)
        {
            return;
        }

        if (input.WasPressed(GameAction.Restart))
        {
            RestartGame();
            return;
        }

        switch (_overMenu.Update(input, Context.Audio))
        {
            case 0:
                RestartGame();
                break;
            case 1:
                Context.Screens.Replace(new MainMenuScreen(Context));
                break;
        }
    }

    protected override void RenderGame(SKCanvas canvas)
    {
        var board = _session.Board;
        var arena = Context.Arena;
        var ui = Context.Ui;
        int shownScore = board.Score.Score - LeftEffects.PendingPoints;
        int best = Math.Max(_bestAtStart, shownScore);

        arena.DrawArena(canvas, new ArenaHud($"LEVEL {_session.Levels.Level}", shownScore, best,
            board.Score.FoodCleared - LeftEffects.PendingFood, 0, 0, 0, _session.Elapsed, Versus: false,
            PlayerTwoTag: "HI", PulseLeft: LeftEffects.HeartPulse));
        Context.Playfield.DrawNext(canvas, board, ArenaLayout.LeftNext, labelOnRight: true, LeftEffects.Visuals);

        var mood = _session.IsOver ? MascotMood.Sad : MoodOr(SteadyMoodFor(board));
        Context.Mascot.Draw(canvas, ArenaRenderer.MascotArea, mood, Time);

        DrawField(canvas, board, ArenaLayout.LeftField, LeftEffects);
        DrawInfoPanel(canvas, ui, board);
        DrawFlyingEffects(canvas);

        if (_session.IsOver && _overTimer >= GameOverPanelDelay)
        {
            DrawGameOver(canvas, ui, board, best);
        }
    }

    private void Record()
    {
        if (_recorded)
        {
            return;
        }

        _recorded = true;
        var board = _session.Board;
        _rank = Context.Saves.Record(new HighScoreEntry(board.Score.Score, _session.Levels.Level,
            board.Score.FoodCleared, board.Score.BestChain, GameMode.Endless, DateTimeOffset.Now));
    }

    private void DrawInfoPanel(SKCanvas canvas, UiRenderer ui, PlayerBoard board)
    {
        var area = new SKRect(526, 122, 786, 582);
        ui.Panel(canvas, area);
        float x = area.MidX;
        var gold = new SKColor(0xFF, 0xE4, 0x5C);
        ui.OutlinedText(canvas, "ENDLESS", x, area.Top + 44, 26, gold, Palette.Outline, UiFont.Heading, 3);

        float y = area.Top + 84;
        Stat(canvas, ui, "LEVEL", _session.Levels.Level.ToString(CultureInfo.InvariantCulture), area, ref y);

        // Progress towards the next level.
        var rules = Context.Rules;
        int target = _session.Levels.NextLevelTarget;
        int previous = target - rules.FoodPerLevel;
        float progress = _session.Levels.Level >= rules.MaxLevel ? 1f
            : Math.Clamp((board.Score.FoodCleared - previous) / (float)rules.FoodPerLevel, 0f, 1f);
        var bar = new SKRect(area.Left + 24, y - 14, area.Right - 24, y);
        ui.Fill(canvas, bar, SKColors.Black, 5);
        ui.Fill(canvas, new SKRect(bar.Left + 2, bar.Top + 2, bar.Left + 2 + (bar.Width - 4) * progress, bar.Bottom - 2),
            new SKColor(0x6C, 0xE0, 0x5A), 4);
        y += 22;

        Stat(canvas, ui, "FOOD EATEN", board.Score.FoodCleared.ToString(CultureInfo.InvariantCulture), area, ref y);
        Stat(canvas, ui, "BEST CHAIN", board.Score.BestChain.ToString(CultureInfo.InvariantCulture), area, ref y);

        y += 4;
        ui.Text(canvas, "WHO EATS WHAT", x, y, 15, gold, UiFont.Heading);
        y += 10;
        int count = rules.Animals.Count;
        for (int i = 0; i < count; i++)
        {
            var animal = rules.Animals[i];
            float col = area.Left + 30 + (i % 2) * 118;
            float row = y + (i / 2) * 46;
            Context.Art.Draw(canvas, Block.Of(animal), col, row, 38);
            ui.Text(canvas, "+", col + 50, row + 26, 18, SKColors.White, UiFont.Heading);
            Context.Art.Draw(canvas, Block.Of(rules.FoodFor(animal)), col + 62, row, 38);
        }

        y += (count + 1) / 2 * 46 + 18;
        string[] hints =
        [
            $"{Key(GameAction.MoveLeft)} / {Key(GameAction.MoveRight)}: move",
            $"{Key(GameAction.RotateClockwise)} / {Key(GameAction.RotateCounterClockwise)}: rotate",
            $"{Key(GameAction.SoftDrop)} (hold): drop faster",
            $"{Key(GameAction.Pause)} / Esc: pause   F1: help",
        ];
        foreach (var hint in hints)
        {
            ui.Text(canvas, hint, x, y, 14, Palette.TimerText, UiFont.Body);
            y += 19;
        }
    }

    private string Key(GameAction action) => Input.KeyboardBindings.KeyName(Context.Input.Bindings.PrimaryKey(action));

    private static void Stat(SKCanvas canvas, UiRenderer ui, string label, string value, SKRect area, ref float y)
    {
        ui.Text(canvas, label, area.Left + 24, y, 16, Palette.TimerText, UiFont.Heading, SKTextAlign.Left);
        ui.OutlinedText(canvas, value, area.Right - 24, y + 2, 24, SKColors.White, Palette.Outline, UiFont.Digits, 2.5f, SKTextAlign.Right);
        y += 38;
    }

    private void DrawGameOver(SKCanvas canvas, UiRenderer ui, PlayerBoard board, int best)
    {
        ui.Dim(canvas, 120);
        var panel = new SKRect(210, 150, 590, 470);
        ui.Panel(canvas, panel);
        ui.OutlinedText(canvas, "GAME OVER", 400, 210, 40, new SKColor(0xFF, 0x70, 0x70), Palette.Outline, UiFont.Heading, 4);
        ui.OutlinedText(canvas, $"SCORE  {board.Score.Score}", 400, 262, 26, SKColors.White, Palette.Outline, UiFont.Digits);
        ui.OutlinedText(canvas, $"BEST   {best}", 400, 298, 22, Palette.TimerText, Palette.Outline, UiFont.Digits);
        if (_rank == 1)
        {
            float pulse = 1f + 0.06f * MathF.Sin((float)Time * 8f);
            ui.OutlinedText(canvas, "NEW RECORD!", 400, 340, 26 * pulse, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 3);
        }
        else if (_rank > 1)
        {
            ui.Text(canvas, $"High score table: #{_rank}", 400, 336, 18, new SKColor(0xFF, 0xE4, 0x5C), UiFont.Playful);
        }

        _overMenu.Draw(canvas, ui, 400, 392, 42, Time, 24);
    }
}
