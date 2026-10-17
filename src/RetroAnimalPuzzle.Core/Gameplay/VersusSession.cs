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
    CpuWon,
    Draw,
}

/// <summary>
/// Player versus CPU match: two boards side by side, food eaten turns into garbage dropped on the opponent
/// (after cancelling one's own pending garbage), rounds are won by topping the opponent out, and the first
/// to <see cref="GameRules.RoundsToWinMatch"/> wins takes the stage. Winning a stage advances to a faster,
/// smarter opponent.
/// </summary>
public sealed class VersusSession
{
    public const double ReadyDuration = 1.6;
    public const double RoundOverDuration = 2.6;

    private readonly CpuDifficulty _difficulty;
    private readonly Random _seeds;

    public VersusSession(GameRules rules, int seed, CpuDifficulty difficulty)
    {
        Rules = rules;
        _difficulty = difficulty;
        _seeds = new Random(seed);
        Stage = 1;
        StartRound();
    }

    public GameRules Rules { get; }

    public PlayerBoard Player { get; private set; } = null!;

    public PlayerBoard Cpu { get; private set; } = null!;

    public CpuController CpuBrain { get; private set; } = null!;

    public int Stage { get; private set; }

    public int Round { get; private set; }

    public int PlayerWins { get; private set; }

    public int CpuWins { get; private set; }

    public VersusPhase Phase { get; private set; }

    public double PhaseElapsed { get; private set; }

    public RoundResult LastRound { get; private set; }

    public TimeSpan RoundElapsed { get; private set; }

    /// <summary>Points from stages and rounds already finished.</summary>
    public int PlayerScoreCarry { get; private set; }

    public int PlayerTotalScore => PlayerScoreCarry + Player.Score.Score;

    public int CpuScore => Cpu.Score.Score;

    public bool PlayerWonMatch => Phase == VersusPhase.MatchOver && PlayerWins >= Rules.RoundsToWinMatch;

    public int SpeedLevel => Stage + (int)(RoundElapsed.TotalSeconds / Rules.VersusSecondsPerSpeedLevel);

    public void Update(double deltaSeconds, in InputState playerInput)
    {
        PhaseElapsed += deltaSeconds;
        switch (Phase)
        {
            case VersusPhase.Ready when PhaseElapsed >= ReadyDuration:
                EnterPhase(VersusPhase.Playing);
                break;
            case VersusPhase.Playing:
                UpdatePlaying(deltaSeconds, playerInput);
                break;
            case VersusPhase.RoundOver when PhaseElapsed >= RoundOverDuration:
                if (PlayerWins >= Rules.RoundsToWinMatch || CpuWins >= Rules.RoundsToWinMatch)
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
        CpuWins = 0;
        Round = 0;
        StartRound();
    }

    private void UpdatePlaying(double deltaSeconds, in InputState playerInput)
    {
        RoundElapsed += TimeSpan.FromSeconds(deltaSeconds);
        int speed = SpeedLevel;
        Player.SpeedLevel = speed;
        Cpu.SpeedLevel = speed;

        var cpuInput = CpuBrain.Think(Cpu, deltaSeconds);
        Player.Update(deltaSeconds, playerInput);
        Cpu.Update(deltaSeconds, cpuInput);

        Cpu.AddIncomingGarbage(Player.CancelIncoming(Player.TakeOutgoingGarbage()));
        Player.AddIncomingGarbage(Cpu.CancelIncoming(Cpu.TakeOutgoingGarbage()));

        bool playerOut = Player.IsToppedOut;
        bool cpuOut = Cpu.IsToppedOut;
        if (!playerOut && !cpuOut)
        {
            return;
        }

        LastRound = (playerOut, cpuOut) switch
        {
            (true, true) => RoundResult.Draw,
            (true, false) => RoundResult.CpuWon,
            _ => RoundResult.PlayerWon,
        };

        if (LastRound == RoundResult.PlayerWon) PlayerWins++;
        if (LastRound == RoundResult.CpuWon) CpuWins++;
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
        Cpu = new PlayerBoard(Rules, _seeds.Next());
        CpuBrain = new CpuController(Rules, CpuSkill.For(_difficulty, Stage), _seeds.Next());
        EnterPhase(VersusPhase.Ready);
    }

    private void EnterPhase(VersusPhase phase)
    {
        Phase = phase;
        PhaseElapsed = 0;
    }
}
