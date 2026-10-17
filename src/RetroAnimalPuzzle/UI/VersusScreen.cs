using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Player (left) versus CPU (right), in the classic two-field arena with hearts for round wins.</summary>
internal sealed class VersusScreen : PlayScreenBase
{
    private static readonly SKColor Yellow = new(0xFF, 0xE4, 0x5C);

    private readonly MenuList _winMenu = new("Next Stage", "Main Menu");
    private readonly MenuList _loseMenu = new("Retry", "Main Menu");
    private VersusSession _session = null!;
    private VersusPhase _lastPhase;
    private bool _recorded;

    public VersusScreen(GameContext context) : base(context)
    {
        RestartGame();
    }

    protected override bool CanPause => _session.Phase is VersusPhase.Ready or VersusPhase.Playing;

    protected override PlayerBoard LeftBoard => _session.Player;

    protected override PlayerBoard? RightBoard => _session.Cpu;

    protected override void RestartGame()
    {
        _session = new VersusSession(Context.Rules, GameContext.NewSeed(), Context.Settings.CpuDifficulty);
        _recorded = false;
        LeftEffects.Clear();
        RightEffects.Clear();
        OnPhaseEntered(_session.Phase);
        _lastPhase = _session.Phase;
    }

    protected override void UpdateGame(double deltaSeconds, in InputState input)
    {
        var before = _session.Phase;
        _session.Update(deltaSeconds, before == VersusPhase.Playing ? input : InputState.None);

        // Board events are only fresh when the boards were actually updated this tick.
        if (before == VersusPhase.Playing)
        {
            React(_session.Player, ArenaLayout.LeftField, LeftEffects, human: true);
            React(_session.Cpu, ArenaLayout.RightField, RightEffects, human: false);
        }

        if (_session.Phase != _lastPhase)
        {
            _lastPhase = _session.Phase;
            OnPhaseEntered(_session.Phase);
        }

        if (_session.Phase == VersusPhase.MatchOver)
        {
            UpdateMatchOver(input);
        }
    }

    protected override void RenderGame(SKCanvas canvas)
    {
        var arena = Context.Arena;
        arena.DrawArena(canvas, new ArenaHud($"STAGE {_session.Stage}",
            _session.PlayerTotalScore - LeftEffects.PendingPoints, _session.CpuScore - RightEffects.PendingPoints,
            _session.Player.Score.FoodCleared - LeftEffects.PendingFood, _session.Cpu.Score.FoodCleared - RightEffects.PendingFood,
            _session.PlayerWins, _session.CpuWins, _session.RoundElapsed, Versus: true,
            PulseLeft: LeftEffects.HeartPulse, PulseRight: RightEffects.HeartPulse, WinsToMatch: Context.Rules.RoundsToWinMatch));
        Context.Playfield.DrawNext(canvas, _session.Player, ArenaLayout.LeftNext, labelOnRight: true, LeftEffects.Visuals);
        Context.Playfield.DrawNext(canvas, _session.Cpu, ArenaLayout.RightNext, labelOnRight: false, RightEffects.Visuals);
        Context.Playfield.DrawIncomingBar(canvas, ArenaLayout.LeftIncomingBar, LeftEffects.Visuals, outwardIsLeft: true, Time);
        Context.Playfield.DrawIncomingBar(canvas, ArenaLayout.RightIncomingBar, RightEffects.Visuals, outwardIsLeft: false, Time);
        Context.Mascot.Draw(canvas, ArenaRenderer.MascotArea, CurrentMood(), Time);

        DrawField(canvas, _session.Player, ArenaLayout.LeftField, LeftEffects);
        DrawField(canvas, _session.Cpu, ArenaLayout.RightField, RightEffects, human: false);

        Context.Ui.Text(canvas, "YOU", ArenaLayout.LeftField.MidX, ArenaLayout.LeftField.Bottom + 22, 14, Palette.TimerText, UiFont.Heading);
        Context.Ui.Text(canvas, $"CPU ({Context.Settings.CpuDifficulty})", ArenaLayout.RightField.MidX,
            ArenaLayout.RightField.Bottom + 22, 14, Palette.TimerText, UiFont.Heading);
        DrawFlyingEffects(canvas);

        if (_session.Phase == VersusPhase.MatchOver)
        {
            DrawMatchOver(canvas);
        }
    }

    private MascotMood CurrentMood() => _session.Phase switch
    {
        VersusPhase.MatchOver => _session.PlayerWonMatch ? MascotMood.Celebrate : MascotMood.Sad,
        VersusPhase.RoundOver => _session.LastRound == RoundResult.PlayerWon ? MascotMood.Celebrate
            : _session.LastRound == RoundResult.CpuWon ? MascotMood.Sad : MascotMood.Idle,
        _ => MoodOr(SteadyMoodFor(_session.Player)),
    };

    private void OnPhaseEntered(VersusPhase phase)
    {
        var audio = Context.Audio;
        switch (phase)
        {
            case VersusPhase.Ready:
                LeftEffects.Clear();
                RightEffects.Clear();
                string round = $"ROUND {_session.Round}";
                LeftEffects.Animations.Announce(round, Yellow, "READY?", (float)VersusSession.ReadyDuration);
                RightEffects.Animations.Announce(round, Yellow, "READY?", (float)VersusSession.ReadyDuration);
                audio.Play(Sound.Ready);
                break;
            case VersusPhase.Playing:
                LeftEffects.Animations.Announce("GO!", Yellow, null, 0.8f);
                RightEffects.Animations.Announce("GO!", Yellow, null, 0.8f);
                audio.Play(Sound.Go);
                break;
            case VersusPhase.RoundOver:
                var win = new SKColor(0xFF, 0xE4, 0x5C);
                var lose = new SKColor(0xFF, 0x70, 0x70);
                float life = (float)VersusSession.RoundOverDuration;
                switch (_session.LastRound)
                {
                    case RoundResult.PlayerWon:
                        LeftEffects.Animations.Announce("YOU WIN!", win, null, life);
                        RightEffects.Animations.Announce("LOSE", lose, null, life);
                        audio.Play(Sound.RoundWin);
                        break;
                    case RoundResult.CpuWon:
                        LeftEffects.Animations.Announce("YOU LOSE", lose, null, life);
                        RightEffects.Animations.Announce("WIN!", win, null, life);
                        audio.Play(Sound.RoundLose);
                        break;
                    default:
                        LeftEffects.Animations.Announce("DRAW", SKColors.White, null, life);
                        RightEffects.Animations.Announce("DRAW", SKColors.White, null, life);
                        audio.Play(Sound.RoundLose);
                        break;
                }

                break;
            case VersusPhase.MatchOver:
                _winMenu.Selected = 0;
                _loseMenu.Selected = 0;
                if (_session.PlayerWonMatch)
                {
                    audio.Play(Sound.LevelUp);
                }
                else
                {
                    audio.Play(Sound.GameOver);
                    Record();
                }

                break;
        }
    }

    private void UpdateMatchOver(in InputState input)
    {
        if (_session.PlayerWonMatch)
        {
            switch (_winMenu.Update(input, Context.Audio))
            {
                case 0:
                    _session.AdvanceStage();
                    _lastPhase = _session.Phase;
                    OnPhaseEntered(_session.Phase);
                    break;
                case 1:
                    Record();
                    Context.Screens.Replace(new MainMenuScreen(Context));
                    break;
            }

            return;
        }

        if (input.WasPressed(GameAction.Restart))
        {
            RestartGame();
            return;
        }

        switch (_loseMenu.Update(input, Context.Audio))
        {
            case 0:
                RestartGame();
                break;
            case 1:
                Context.Screens.Replace(new MainMenuScreen(Context));
                break;
        }
    }

    private void Record()
    {
        if (_recorded)
        {
            return;
        }

        _recorded = true;
        Context.Saves.Record(new HighScoreEntry(_session.PlayerTotalScore, _session.Stage,
            _session.Player.Score.FoodCleared, _session.Player.Score.BestChain, GameMode.Versus, DateTimeOffset.Now));
    }

    private void DrawMatchOver(SKCanvas canvas)
    {
        var ui = Context.Ui;
        ui.Dim(canvas, 120);
        ui.Panel(canvas, new SKRect(200, 170, 600, 450));
        if (_session.PlayerWonMatch)
        {
            ui.OutlinedText(canvas, "STAGE CLEAR!", 400, 232, 40, Yellow, Palette.Outline, UiFont.Heading, 4);
            ui.OutlinedText(canvas, $"SCORE  {_session.PlayerTotalScore}", 400, 284, 24, SKColors.White, Palette.Outline, UiFont.Digits);
            ui.Text(canvas, $"Next: stage {_session.Stage + 1} - a faster opponent!", 400, 322, 17, Palette.TimerText, UiFont.Playful);
            _winMenu.Draw(canvas, ui, 400, 372, 42, Time, 24);
        }
        else
        {
            ui.OutlinedText(canvas, "GAME OVER", 400, 232, 40, new SKColor(0xFF, 0x70, 0x70), Palette.Outline, UiFont.Heading, 4);
            ui.OutlinedText(canvas, $"SCORE  {_session.PlayerTotalScore}", 400, 284, 24, SKColors.White, Palette.Outline, UiFont.Digits);
            ui.Text(canvas, $"Reached stage {_session.Stage}", 400, 322, 17, Palette.TimerText, UiFont.Playful);
            _loseMenu.Draw(canvas, ui, 400, 372, 42, Time, 24);
        }
    }
}
