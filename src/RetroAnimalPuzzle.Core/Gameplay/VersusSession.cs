using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Gameplay;

public enum VersusPhase
{
    /// <summary>"READY?" countdown before a round.</summary>
    Ready,
    Playing,
    /// <summary>A round just ended; the result is shown briefly.</summary>
    RoundOver,
    /// <summary>Someone reached the required number of round wins. Waits for the UI.</summary>
    MatchOver,
}

public enum RoundResult
{
    None,
    PlayerWon,
    OpponentWon,
    Draw,
}

/// <summary>
/// Versus match against the CPU or a second human: two boards side by side, food eaten turns into garbage
/// dropped on the opponent (after cancelling one's own pending garbage), rounds are won by topping the opponent
/// out, and the first to <see cref="GameRules.RoundsToWinMatch"/> wins takes the stage. Winning a stage advances
/// to a faster, smarter opponent. In a two-player match there is no CPU: the opponent board is driven by the second player's input.
/// </summary>
public sealed class VersusSession
{
    public const double ReadyDuration = 1.6;
    public const double RoundOverDuration = 2.6;

    private readonly CpuDifficulty? _difficulty;
    private readonly Random _seeds;

    /// <param name="difficulty">The CPU opponent's level, or <c>null</c> for a two-player match.</param>
    public VersusSession(GameRules rules, int seed, CpuDifficulty? difficulty)
    {
        Rules = rules;
        _difficulty = difficulty;
        _seeds = new Random(seed);
        Stage = 1;
        StartRound();
    }

    public GameRules Rules { get; }

    public PlayerBoard Player { get; private set; } = null!;

    /// <summary>The right-hand board: the CPU, or the second player.</summary>
    public PlayerBoard Opponent { get; private set; } = null!;

    /// <summary>The CPU driving <see cref="Opponent"/>; <c>null</c> in a two-player match.</summary>
    public CpuController? CpuBrain { get; private set; }

    public bool IsTwoPlayer => _difficulty is null;

    public int Stage { get; private set; }

    public int Round { get; private set; }

    public int PlayerWins { get; private set; }

    public int OpponentWins { get; private set; }

    public VersusPhase Phase { get; private set; }

    public double PhaseElapsed { get; private set; }

    public RoundResult LastRound { get; private set; }

    public TimeSpan RoundElapsed { get; private set; }

    /// <summary>Points from stages and rounds already finished.</summary>
    public int PlayerScoreCarry { get; private set; }

    public int PlayerTotalScore => PlayerScoreCarry + Player.Score.Score;

    public int OpponentScore => Opponent.Score.Score;

    public bool PlayerWonMatch => Phase == VersusPhase.MatchOver && PlayerWins >= Rules.RoundsToWinMatch;

    public int SpeedLevel => Stage + (int)(RoundElapsed.TotalSeconds / Rules.VersusSecondsPerSpeedLevel);

    /// <param name="opponentInput">The second player's input; ignored when the CPU plays.</param>
    public void Update(double deltaSeconds, in InputState playerInput, in InputState opponentInput = default)
    {
        PhaseElapsed += deltaSeconds;
        switch (Phase)
        {
            case VersusPhase.Ready when PhaseElapsed >= ReadyDuration:
                EnterPhase(VersusPhase.Playing);
                break;
            case VersusPhase.Playing:
                UpdatePlaying(deltaSeconds, playerInput, opponentInput);
                break;
            case VersusPhase.RoundOver when PhaseElapsed >= RoundOverDuration:
                if (PlayerWins >= Rules.RoundsToWinMatch || OpponentWins >= Rules.RoundsToWinMatch)
                {
                    EnterPhase(VersusPhase.MatchOver);
                }
                else
                {
                    StartRound();
                }

                break;
        }
    }

    /// <summary>After the player wins a match: carry the score over and face the next, tougher opponent.</summary>
    public void AdvanceStage()
    {
        if (!PlayerWonMatch)
        {
            throw new InvalidOperationException("The player has not won the current match.");
        }

        Stage++;
        PlayerWins = 0;
        OpponentWins = 0;
        Round = 0;
        StartRound();
    }

    private void UpdatePlaying(double deltaSeconds, in InputState playerInput, in InputState opponentInput)
    {
        RoundElapsed += TimeSpan.FromSeconds(deltaSeconds);
        int speed = SpeedLevel;
        Player.SpeedLevel = speed;
        Opponent.SpeedLevel = speed;

        var secondInput = CpuBrain?.Think(Opponent, deltaSeconds) ?? opponentInput;
        Player.Update(deltaSeconds, playerInput);
        Opponent.Update(deltaSeconds, secondInput);

        Opponent.AddIncomingGarbage(Player.CancelIncoming(Player.TakeOutgoingGarbage()));
        Player.AddIncomingGarbage(Opponent.CancelIncoming(Opponent.TakeOutgoingGarbage()));

        bool playerOut = Player.IsToppedOut;
        bool opponentOut = Opponent.IsToppedOut;
        if (!playerOut && !opponentOut)
        {
            return;
        }

        LastRound = (playerOut, opponentOut) switch
        {
            (true, true) => RoundResult.Draw,
            (true, false) => RoundResult.OpponentWon,
            _ => RoundResult.PlayerWon,
        };

        if (LastRound == RoundResult.PlayerWon) PlayerWins++;
        if (LastRound == RoundResult.OpponentWon) OpponentWins++;
        EnterPhase(VersusPhase.RoundOver);
    }

    private void StartRound()
    {
        if (Player is not null)
        {
            PlayerScoreCarry += Player.Score.Score;
        }

        Round++;
        RoundElapsed = TimeSpan.Zero;
        LastRound = RoundResult.None;
        Player = new PlayerBoard(Rules, _seeds.Next());
        Opponent = new PlayerBoard(Rules, _seeds.Next());
        int cpuSeed = _seeds.Next();
        CpuBrain = _difficulty is { } level ? new CpuController(Rules, CpuSkill.For(level, Stage), cpuSeed) : null;
        EnterPhase(VersusPhase.Ready);
    }

    private void EnterPhase(VersusPhase phase)
    {
        Phase = phase;
        PhaseElapsed = 0;
    }
}
