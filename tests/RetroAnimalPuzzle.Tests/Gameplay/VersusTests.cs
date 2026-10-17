using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class VersusTests
{
    private const double Tick = 1.0 / 60;
    private static readonly GameRules Rules = GameRules.Default;

    [Fact]
    public void Garbage_CancelsIncomingFirst()
    {
        var board = new PlayerBoard(Rules, 1);
        board.AddIncomingGarbage(5);

        int remaining = board.CancelIncoming(8);

        Assert.Equal(3, remaining);
        Assert.Equal(0, board.IncomingGarbage);
    }

    [Fact]
    public void IdlePlayer_LosesMatchToCpu()
    {
        var session = new VersusSession(Rules, seed: 11, CpuDifficulty.Normal);

        for (int i = 0; i < 60 * 60 * 20 && session.Phase != VersusPhase.MatchOver; i++)
        {
            session.Update(Tick, InputState.None);
        }

        Assert.Equal(VersusPhase.MatchOver, session.Phase);
        Assert.Equal(Rules.RoundsToWinMatch, session.CpuWins);
        Assert.False(session.PlayerWonMatch);
    }

    [Fact]
    public void Cpu_PlansWithoutTopping_OutQuickly()
    {
        // A hard CPU playing alone should survive well beyond what random play manages.
        var board = new PlayerBoard(Rules, 3);
        var cpu = new CpuController(Rules, CpuSkill.For(CpuDifficulty.Hard, 1), 4);
        int pieces = 0;
        int lastSerial = 0;

        for (int i = 0; i < 60 * 60 * 3 && !board.IsToppedOut; i++)
        {
            board.Update(Tick, cpu.Think(board, Tick));
            if (board.PieceSerial != lastSerial)
            {
                lastSerial = board.PieceSerial;
                pieces++;
            }
        }

        Assert.True(pieces > 60, $"CPU only placed {pieces} pieces.");
        Assert.True(board.Score.FoodCleared > 20, $"CPU only ate {board.Score.FoodCleared} food.");
    }

    [Fact]
    public void CpuSkill_GetsStrongerWithStage()
    {
        var early = CpuSkill.For(CpuDifficulty.Normal, 1);
        var late = CpuSkill.For(CpuDifficulty.Normal, 5);

        Assert.True(late.ReactionDelay < early.ReactionDelay);
        Assert.True(late.ActionInterval < early.ActionInterval);
        Assert.True(late.BlunderChance < early.BlunderChance);
    }
}
