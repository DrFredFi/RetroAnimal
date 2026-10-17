using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>A block that fell from one row to another during a gravity pass (same column).</summary>
public readonly record struct BlockFall(Position From, Position To, Block Block)
{
    public int Distance => To.Y - From.Y;
}

/// <summary>
/// Makes every unsupported block fall straight down until it rests on the floor or another block.
/// Blocks keep their relative order within a column.
/// </summary>
public static class GravitySystem
{
    public static List<BlockFall> Apply(Grid grid)
    {
        var falls = new List<BlockFall>();
        for (int x = 0; x < grid.Columns; x++)
        {
            int writeY = grid.Rows - 1;
            for (int y = grid.Rows - 1; y >= 0; y--)
            {
                var block = grid[x, y];
                if (block.IsEmpty)
                {
                    continue;
                }

                if (y != writeY)
                {
                    var from = new Position(x, y);
                    var to = new Position(x, writeY);
                    grid.Move(from, to);
                    falls.Add(new BlockFall(from, to, block));
                }

                writeY--;
            }
        }

        return falls;
    }

    /// <summary>True when at least one block has an empty cell directly below it.</summary>
    public static bool HasUnsupportedBlocks(Grid grid)
    {
        for (int x = 0; x < grid.Columns; x++)
        {
            for (int y = 0; y < grid.Rows - 1; y++)
            {
                if (grid.IsOccupied(x, y) && grid.IsFree(x, y + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
