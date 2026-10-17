using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Tests.Core;

public class GridTests
{
    private readonly GameRules _rules = GameRules.Default;

    [Fact]
    public void NewGrid_IsEmptyEverywhere()
    {
        var grid = Grid.For(_rules);

        Assert.Equal(_rules.TotalRows, grid.Rows);
        Assert.Equal(_rules.Columns, grid.Columns);
        Assert.Equal(0, grid.CountBlocks());
        for (int y = 0; y < grid.Rows; y++)
        {
            for (int x = 0; x < grid.Columns; x++)
            {
                Assert.True(grid.IsFree(x, y));
                Assert.True(grid[x, y].IsEmpty);
            }
        }
    }

    [Fact]
    public void PlacedBlock_IsDetectedAsOccupied()
    {
        var grid = Grid.For(_rules);
        var p = new Position(3, 7);

        grid.Place(p, Block.Of(AnimalType.Panda));

        Assert.True(grid.IsOccupied(3, 7));
        Assert.False(grid.IsFree(p));
        Assert.Equal(Block.Of(AnimalType.Panda), grid[p]);
        Assert.Equal(1, grid.CountBlocks());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(6, 0)]
    [InlineData(0, 13)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void OutOfRange_IsSafeAndBlocked(int x, int y)
    {
        var grid = Grid.For(_rules);

        Assert.False(grid.InBounds(x, y));
        Assert.True(grid[x, y].IsEmpty);
        Assert.False(grid.IsFree(x, y));
        Assert.False(grid.IsOccupied(x, y));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Place(new Position(x, y), Block.Coin));
        grid.Clear(new Position(x, y)); // no throw
    }

    [Fact]
    public void LockedBlocks_CannotOverlap()
    {
        var grid = Grid.For(_rules);
        var p = new Position(0, 12);
        grid.Place(p, Block.Of(FoodType.Bone));

        Assert.Throws<InvalidOperationException>(() => grid.Place(p, Block.Of(FoodType.Banana)));
        Assert.Equal(Block.Of(FoodType.Bone), grid[p]);
    }

    [Fact]
    public void PlacingEmpty_IsRejected()
    {
        var grid = Grid.For(_rules);

        Assert.Throws<ArgumentException>(() => grid.Place(new Position(0, 0), Block.Empty));
    }

    [Fact]
    public void HiddenRows_AreDetected()
    {
        var grid = Grid.For(_rules);
        Assert.False(grid.HasBlocksInHiddenRows());

        grid.Place(new Position(2, _rules.HiddenRows), Block.Coin); // first visible row
        Assert.False(grid.HasBlocksInHiddenRows());

        grid.Place(new Position(2, _rules.HiddenRows - 1), Block.Coin);
        Assert.True(grid.HasBlocksInHiddenRows());
    }

    [Fact]
    public void ColumnHeight_AndLowestFreeRow()
    {
        var grid = TestGrid.Parse(_rules,
            "M.....",
            "b.....");

        Assert.Equal(2, grid.ColumnHeight(0));
        Assert.Equal(0, grid.ColumnHeight(1));
        Assert.Equal(grid.Rows - 3, grid.LowestFreeRow(0));
        Assert.Equal(grid.Rows - 1, grid.LowestFreeRow(1));
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var grid = TestGrid.Parse(_rules, "Mb....");
        var clone = grid.Clone();

        clone.Clear(new Position(0, grid.Rows - 1));

        Assert.True(grid.IsOccupied(0, grid.Rows - 1));
        Assert.False(clone.IsOccupied(0, grid.Rows - 1));
    }
}
