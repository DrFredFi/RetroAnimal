using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>
/// Versus in the classic two-field arena with hearts for round wins: the player (left) against the CPU (right),
/// or, in a two-player match, two people on one keyboard (the left player uses the second key set).
/// </summary>
internal sealed class VersusScreen : PlayScreenBase
{
    private static readonly SKColor Yellow = new(0xFF, 0xE4, 0x5C);
    private static readonly SKColor Red = new(0xFF, 0x70, 0x70);

    private readonly bool _twoPlayers;
    private readonly MenuList _winMenu = new("Next Stage", "Main Menu");
    private readonly MenuList _loseMenu;
    private VersusSession _session = null!;
    private VersusPhase _lastPhase;
    private bool _recorded;

    public VersusScreen(GameContext context, bool twoPlayers = false) : base(context)
    {
        _twoPlayers = twoPlayers;
        _loseMenu = new MenuList(twoPlayers ? "Rematch" : "Retry", "Main Menu");
        RestartGame();
    }

    protected override bool CanPause => _session.Phase is VersusPhase.Ready or VersusPhase.Playing;

    protected override PlayerBoard LeftBoard => _session.Player;

    protected override PlayerBoard? RightBoard => _session.Opponent;

    protected override bool RightIsHuman => _twoPlayers;

    public override void OnEnter()
    {
        base.OnEnter();
        Context.Input.TwoPlayerPads = _twoPlayers;
    }

    public override void Dispose()
    {
        Context.Input.TwoPlayerPads = false;
        base.Dispose();
    }

    protected override void RestartGame()
    {
        _session = new VersusSession(Context.Rules, GameContext.NewSeed(), _twoPlayers ? null : Context.Settings.CpuDifficulty);
        _recorded = false;
        LeftEffects.Clear();
        RightEffects.Clear();
        OnPhaseEntered(_session.Phase);
        _lastPhase = _session.Phase;
    }

    protected override void UpdateGame(double deltaSeconds, in InputState input)
    {
        var before = _session.Phase;
        if (before == VersusPhase.Playing)
        {
            // Two players share the keyboard: the left board only sees the second key set.
            _session.Update(deltaSeconds, _twoPlayers ? input.ForLeftPlayer() : input, input);
        }
        else
        {
            _session.Update(deltaSeconds, InputState.None);
        }

        // Board events are only fresh when the boards were actually updated this tick.
        if (before == VersusPhase.Playing)
        {
            React(_session.Player, ArenaLayout.LeftField, LeftEffects, human: true);
            React(_session.Opponent, ArenaLayout.RightField, RightEffects, human: _twoPlayers);
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
        arena.DrawArena(canvas, new ArenaHud(_twoPlayers ? "2 PLAYERS" : $"STAGE {_session.Stage}",
            _session.PlayerTotalScore - LeftEffects.PendingPoints, _session.OpponentScore - RightEffects.PendingPoints,
            _session.Player.Score.FoodCleared - LeftEffects.PendingFood, _session.Opponent.Score.FoodCleared - RightEffects.PendingFood,
            _session.PlayerWins, _session.OpponentWins, _session.RoundElapsed, Versus: true,
            PulseLeft: LeftEffects.HeartPulse, PulseRight: RightEffects.HeartPulse, WinsToMatch: Context.Rules.RoundsToWinMatch));
        Context.Playfield.DrawNext(canvas, _session.Player, ArenaLayout.LeftNext, labelOnRight: true, LeftEffects.Visuals);
        Context.Playfield.DrawNext(canvas, _session.Opponent, ArenaLayout.RightNext, labelOnRight: false, RightEffects.Visuals);
        Context.Playfield.DrawIncomingBar(canvas, ArenaLayout.LeftIncomingBar, LeftEffects.Visuals, outwardIsLeft: true, Time);
        Context.Playfield.DrawIncomingBar(canvas, ArenaLayout.RightIncomingBar, RightEffects.Visuals, outwardIsLeft: false, Time);
        Context.Mascot.Draw(canvas, ArenaRenderer.MascotArea, CurrentMood(), Time);

        DrawField(canvas, _session.Player, ArenaLayout.LeftField, LeftEffects);
        DrawField(canvas, _session.Opponent, ArenaLayout.RightField, RightEffects, human: _twoPlayers);

        var (leftName, rightName) = _twoPlayers
            ? _session.Phase == VersusPhase.Ready
                ? (KeysHint(leftPlayer: true), KeysHint(leftPlayer: false))
                : ("PLAYER 1", "PLAYER 2")
            : ("YOU", $"CPU ({Context.Settings.CpuDifficulty})");
        float nameSize = _twoPlayers && _session.Phase == VersusPhase.Ready ? 12 : 14;
        Context.Ui.Text(canvas, leftName, ArenaLayout.LeftField.MidX, ArenaLayout.LeftField.Bottom + 22, nameSize, Palette.TimerText, UiFont.Heading);
        Context.Ui.Text(canvas, rightName, ArenaLayout.RightField.MidX, ArenaLayout.RightField.Bottom + 22, nameSize, Palette.TimerText, UiFont.Heading);
        DrawFlyingEffects(canvas);

        if (_session.Phase == VersusPhase.MatchOver)
        {
            DrawMatchOver(canvas);
        }
    }

    /// <summary>Shown under each field while "READY?" is up, so both players know their keys.</summary>
    private string KeysHint(bool leftPlayer)
    {
        var bindings = Context.Input.Bindings;
        string Key(GameAction action) => KeyboardBindings.KeyName(bindings.PrimaryKey(action));
        return leftPlayer
            ? $"{Key(GameAction.LeftPlayerMoveLeft)} {Key(GameAction.LeftPlayerMoveRight)} move  " +
              $"{Key(GameAction.LeftPlayerRotateClockwise)} turn  {Key(GameAction.LeftPlayerSoftDrop)} drop"
            : $"{Key(GameAction.MoveLeft)} {Key(GameAction.MoveRight)} move  " +
              $"{Key(GameAction.RotateClockwise)} turn  {Key(GameAction.SoftDrop)} drop";
    }

    private MascotMood CurrentMood() => _session.Phase switch
    {
        VersusPhase.MatchOver => _session.PlayerWonMatch || _twoPlayers ? MascotMood.Celebrate : MascotMood.Sad,
        VersusPhase.RoundOver => _session.LastRound switch
        {
            RoundResult.PlayerWon => MascotMood.Celebrate,
            RoundResult.OpponentWon => _twoPlayers ? MascotMood.Celebrate : MascotMood.Sad,
            _ => MascotMood.Idle,
        },
        _ when _twoPlayers => MoodOr(SteadyMoodFor(_session.Player) == MascotMood.Worried
            ? MascotMood.Worried : SteadyMoodFor(_session.Opponent)),
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
                float life = (float)VersusSession.RoundOverDuration;
                switch (_session.LastRound)
                {
                    case RoundResult.PlayerWon:
                        LeftEffects.Animations.Announce(_twoPlayers ? "WIN!" : "YOU WIN!", Yellow, null, life);
                        RightEffects.Animations.Announce("LOSE", Red, null, life);
                        audio.Play(Sound.RoundWin);
                        break;
                    case RoundResult.OpponentWon:
                        LeftEffects.Animations.Announce(_twoPlayers ? "LOSE" : "YOU LOSE", Red, null, life);
                        RightEffects.Animations.Announce("WIN!", Yellow, null, life);
                        audio.Play(_twoPlayers ? Sound.RoundWin : Sound.RoundLose);
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
                if (_session.PlayerWonMatch || _twoPlayers)
                {
                    audio.Play(Sound.LevelUp);
                }
                else
                {
                    audio.Play(Sound.GameOver);
                }

                if (!_session.PlayerWonMatch)
                {
                    Record();
                }

                break;
        }
    }

    private void UpdateMatchOver(in InputState input)
    {
        // Against the CPU a won match goes on to the next stage; a two-player match just offers a rematch.
        if (_session.PlayerWonMatch && !_twoPlayers)
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

    /// <summary>Saves the high score of a match against the CPU; two-player matches are not ranked.</summary>
    private void Record()
    {
        if (_recorded || _twoPlayers)
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
        if (_twoPlayers)
        {
            string winner = _session.PlayerWonMatch ? "PLAYER 1 WINS!" : "PLAYER 2 WINS!";
            ui.OutlinedText(canvas, winner, 400, 232, 36, Yellow, Palette.Outline, UiFont.Heading, 4);
            ui.OutlinedText(canvas, $"{_session.PlayerTotalScore}  -  {_session.OpponentScore}", 400, 284, 24, SKColors.White,
                Palette.Outline, UiFont.Digits);
            ui.Text(canvas, $"Rounds  {_session.PlayerWins} - {_session.OpponentWins}", 400, 322, 17, Palette.TimerText, UiFont.Playful);
            _loseMenu.Draw(canvas, ui, 400, 372, 42, Time, 24);
        }
        else if (_session.PlayerWonMatch)
        {
            ui.OutlinedText(canvas, "STAGE CLEAR!", 400, 232, 40, Yellow, Palette.Outline, UiFont.Heading, 4);
            ui.OutlinedText(canvas, $"SCORE  {_session.PlayerTotalScore}", 400, 284, 24, SKColors.White, Palette.Outline, UiFont.Digits);
            ui.Text(canvas, $"Next: stage {_session.Stage + 1} - a faster opponent!", 400, 322, 17, Palette.TimerText, UiFont.Playful);
            _winMenu.Draw(canvas, ui, 400, 372, 42, Time, 24);
        }
        else
        {
            ui.OutlinedText(canvas, "GAME OVER", 400, 232, 40, Red, Palette.Outline, UiFont.Heading, 4);
            ui.OutlinedText(canvas, $"SCORE  {_session.PlayerTotalScore}", 400, 284, 24, SKColors.White, Palette.Outline, UiFont.Digits);
            ui.Text(canvas, $"Reached stage {_session.Stage}", 400, 322, 17, Palette.TimerText, UiFont.Playful);
            _loseMenu.Draw(canvas, ui, 400, 372, 42, Time, 24);
        }
    }
}
