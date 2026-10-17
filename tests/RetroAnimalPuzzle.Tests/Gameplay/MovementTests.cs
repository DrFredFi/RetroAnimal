using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class MovementTests
{
    private static readonly GameRules Rules = GameRules.Default;
    private static readonly PiecePair Pair = new(Block.Of(AnimalType.Monkey), Block.Of(FoodType.Carrot));

    private static Piece At(int x, int y, Orientation o = Orientation.Right) => new(Pair, new Position(x, y), o);

    [Fact]
    public void SpawnedPiece_IsHorizontalAtSpawnColumn()
    {
        var piece = Piece.Spawn(Pair, Rules);

        Assert.Equal(new Position(Rules.SpawnColumn, Rules.HiddenRows), piece.Pivot);
        Assert.Equal(new Position(Rules.SpawnColumn + 1, Rules.HiddenRows), piece.Satellite);
    }

    [Fact]
    public void Piece_MovesLeftAndRight_WhenSpaceAvailable()
    {
        var grid = Grid.For(Rules);
        var piece = At(2, 5);

        Assert.True(CollisionSystem.CanMove(grid, piece, -1, 0));
        Assert.True(CollisionSystem.CanMove(grid, piece, 1, 0));
    }

    [Fact]
    public void Piece_CannotLeaveTheGrid()
    {
        var grid = Grid.For(Rules);

        Assert.False(CollisionSystem.CanMove(grid, At(0, 5), -1, 0));
        Assert.False(CollisionSystem.CanMove(grid, At(Rules.Columns - 2, 5), 1, 0));
        Assert.False(CollisionSystem.CanMove(grid, At(2, grid.Rows - 1), 0, 1));
        Assert.False(CollisionSystem.Fits(grid, At(2, 0, Orientation.Up)));
    }

    [Fact]
    public void Piece_CannotMoveThroughLockedBlocks()
    {
        var grid = TestGrid.Parse(Rules, "....D.");
        var piece = At(2, grid.Rows - 1);

        Assert.False(CollisionSystem.CanMove(grid, piece, 1, 0));
        Assert.True(CollisionSystem.CanMove(grid, piece, -1, 0));
    }

    [Fact]
    public void Rotation_Works_WhenSpaceAvailable()
    {
        var grid = Grid.For(Rules);
        var piece = At(2, 5, Orientation.Right);

        Assert.True(CollisionSystem.TryRotate(grid, piece, clockwise: true, out var rotated));
        Assert.Equal(Orientation.Down, rotated.Orientation);
        Assert.Equal(piece.Pivot, rotated.Pivot);
        Assert.Equal(new Position(2, 6), rotated.Satellite);

        Assert.True(CollisionSystem.TryRotate(grid, piece, clockwise: false, out var back));
        Assert.Equal(Orientation.Up, back.Orientation);
    }

    [Fact]
    public void Rotation_FailsSafely_WhenFullyBlocked()
    {
        // Pivot in a one-cell pocket: walls of blocks on every side but the satellite's.
        var grid = TestGrid.Parse(Rules,
            "DDDDDD",
            "D..DDD",
            "DDDDDD");
        int y = grid.Rows - 2;
        var piece = At(1, y, Orientation.Right);
        Assert.True(CollisionSystem.Fits(grid, piece));

        bool rotated = CollisionSystem.TryRotate(grid, piece, clockwise: true, out var result);

        Assert.False(rotated);
        Assert.Equal(piece, result);
    }

    [Fact]
    public void Rotation_WallKicks_AwayFromTheWall()
    {
        var grid = Grid.For(Rules);
        var piece = At(Rules.Columns - 1, 5, Orientation.Up);

        Assert.True(CollisionSystem.TryRotate(grid, piece, clockwise: true, out var rotated));
        Assert.Equal(Orientation.Right, rotated.Orientation);
        Assert.Equal(Rules.Columns - 2, rotated.Pivot.X);
        Assert.True(CollisionSystem.Fits(grid, rotated));
    }

    [Fact]
    public void Rotation_InNarrowWell_FlipsVertically()
    {
        var grid = TestGrid.Parse(Rules,
            "DD.DDD",
            "DD.DDD",
            "DD.DDD");
        var piece = At(2, grid.Rows - 2, Orientation.Up);

        Assert.True(CollisionSystem.TryRotate(grid, piece, clockwise: true, out var rotated));
        Assert.Equal(Orientation.Down, rotated.Orientation);
        Assert.True(CollisionSystem.Fits(grid, rotated));
    }

    [Fact]
    public void DropPosition_IsLowestLegalPosition()
    {
        var grid = TestGrid.Parse(Rules,
            "..M...",
            "..b...");
        var piece = At(2, 3, Orientation.Right);

        var dropped = CollisionSystem.DropPosition(grid, piece);

        Assert.Equal(grid.Rows - 3, dropped.Pivot.Y);
        Assert.True(CollisionSystem.IsGrounded(grid, dropped));
    }

    [Fact]
    public void HeldDirection_AutoRepeatsAfterDelay()
    {
        var grid = Grid.For(Rules);
        var controller = new PieceController(Rules);
        var piece = At(0, 5);
        const double tick = 1.0 / 60;

        var press = new InputState(ActionSet.Empty.With(GameAction.MoveRight), ActionSet.Empty.With(GameAction.MoveRight), ActionSet.Empty);
        var hold = new InputState(ActionSet.Empty.With(GameAction.MoveRight), ActionSet.Empty, ActionSet.Empty);

        controller.Apply(grid, ref piece, tick, press);
        Assert.Equal(1, piece.Pivot.X);

        // Still inside the initial repeat delay: no further movement.
        for (int i = 0; i < 5; i++)
        {
            controller.Apply(grid, ref piece, tick, hold);
        }

        Assert.Equal(1, piece.Pivot.X);

        // Long hold: the piece reaches the wall and stops there.
        for (int i = 0; i < 120; i++)
        {
            controller.Apply(grid, ref piece, tick, hold);
        }

        Assert.Equal(Rules.Columns - 2, piece.Pivot.X);
    }
}
