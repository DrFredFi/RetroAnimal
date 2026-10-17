using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class SoftDropTests
{
    private const double Tick = 1.0 / 60;
    private static readonly GameRules Rules = GameRules.Default;
    private static readonly InputState HoldDown = new(ActionSet.Empty.With(GameAction.SoftDrop), ActionSet.Empty, ActionSet.Empty);

    private static PlayerBoard SpawnedBoard()
    {
        var board = new PlayerBoard(Rules, 8);
        board.Update(Tick, InputState.None);
        Assert.Equal(BoardPhase.Falling, board.Phase);
        return board;
    }

    [Fact]
    public void SoftDropInterval_ShrinksTheLongerDownIsHeld()
    {
        Assert.Equal(Rules.SoftDropStartInterval, Rules.SoftDropInterval(0), 6);
        Assert.Equal(Rules.SoftDropMinInterval, Rules.SoftDropInterval(Rules.SoftDropRampSeconds), 6);
        Assert.Equal(Rules.SoftDropMinInterval, Rules.SoftDropInterval(10), 6);
        for (double t = 0.05; t <= Rules.SoftDropRampSeconds; t += 0.05)
        {
            Assert.True(Rules.SoftDropInterval(t) <= Rules.SoftDropInterval(t - 0.05));
        }
    }

    [Fact]
    public void HoldingDown_AcceleratesProgressively()
    {
        var board = SpawnedBoard();
        int start = board.Current!.Value.Pivot.Y;

        for (int i = 0; i < 18; i++) board.Update(Tick, HoldDown); // first 0.3 s
        int afterEarly = board.Current!.Value.Pivot.Y;
        for (int i = 0; i < 18; i++) board.Update(Tick, HoldDown); // next 0.3 s, faster
        int afterLate = board.Current?.Pivot.Y ?? Rules.TotalRows;

        int early = afterEarly - start;
        int late = afterLate - afterEarly;
        Assert.True(early >= 1, $"Soft drop should move the piece at once (moved {early}).");
        Assert.True(late > early, $"Later rows ({late}) should fall faster than the first ones ({early}).");
    }

    [Fact]
    public void SoftDrop_IsFasterThanNaturalFall_ButNotInstant()
    {
        var board = SpawnedBoard();
        int start = board.Current!.Value.Pivot.Y;

        board.Update(Tick, HoldDown);

        Assert.NotNull(board.Current);
        Assert.True(board.Current!.Value.Pivot.Y - start <= 1);
    }

    [Fact]
    public void PressingDown_AfterSlowFall_NeverJumpsSeveralRows()
    {
        var board = SpawnedBoard();
        // Let the natural fall build up almost a full (slow) row interval.
        int ticks = (int)(Rules.FallInterval(1) / Tick) - 2;
        for (int i = 0; i < ticks; i++) board.Update(Tick, InputState.None);
        int before = board.Current!.Value.Pivot.Y;

        board.Update(Tick, HoldDown);

        Assert.True(board.Current!.Value.Pivot.Y - before <= 1,
            $"Pressing Down jumped {board.Current!.Value.Pivot.Y - before} rows at once.");
    }

    [Fact]
    public void SoftDrop_NeverMovesMoreThanOneRowPerTick()
    {
        var board = SpawnedBoard();
        int previous = board.Current!.Value.Pivot.Y;
        int serial = board.PieceSerial;
        for (int i = 0; i < 120 && board.PieceSerial == serial && board.Current is { } piece; i++)
        {
            board.Update(Tick, HoldDown);
            if (board.Current is { } now && board.PieceSerial == serial)
            {
                Assert.True(now.Pivot.Y - previous <= 1);
                previous = now.Pivot.Y;
            }
        }
    }

    [Fact]
    public void HardDrop_IsIgnoredByDefault()
    {
        var board = SpawnedBoard();
        int start = board.Current!.Value.Pivot.Y;

        board.Update(Tick, new InputState(ActionSet.Empty, ActionSet.Empty.With(GameAction.HardDrop), ActionSet.Empty));

        Assert.Equal(BoardPhase.Falling, board.Phase);
        Assert.Equal(start, board.Current!.Value.Pivot.Y);
    }

    [Fact]
    public void Acceleration_RestartsForEachNewPiece()
    {
        var board = SpawnedBoard();
        int serial = board.PieceSerial;
        for (int i = 0; i < 60 * 10 && board.PieceSerial == serial; i++)
        {
            board.Update(Tick, HoldDown);
        }

        Assert.NotEqual(serial, board.PieceSerial);
        int start = board.Current!.Value.Pivot.Y;
        for (int i = 0; i < 6; i++) board.Update(Tick, HoldDown); // 0.1 s at the slow start interval

        Assert.True(board.Current!.Value.Pivot.Y - start <= 2);
    }
}
