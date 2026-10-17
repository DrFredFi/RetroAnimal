using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Tests;

/// <summary>
/// Builds grids from ASCII art. The last string is the bottom row; rows are right-padded with '.'.
/// Animals are upper case: M monkey, R rabbit, P panda, D dog, K cat.
/// Food is lower case: b banana, c carrot, z bamboo, o bone, f fish. '$' is a coin, '.' is empty.
/// </summary>
internal static class TestGrid
{
    public static Grid Parse(GameRules rules, params string[] rows)
    {
        var grid = Grid.For(rules);
        int top = grid.Rows - rows.Length;
        for (int r = 0; r < rows.Length; r++)
        {
            for (int x = 0; x < rows[r].Length; x++)
            {
                var block = BlockFor(rows[r][x]);
                if (!block.IsEmpty)
                {
                    grid.Place(new Position(x, top + r), block);
                }
            }
        }

        return grid;
    }

    /// <summary>Renders the bottom <paramref name="rowCount"/> rows back to ASCII (handy in assertions).</summary>
    public static string[] Dump(Grid grid, int rowCount)
    {
        var rows = new string[rowCount];
        for (int r = 0; r < rowCount; r++)
        {
            int y = grid.Rows - rowCount + r;
            var chars = new char[grid.Columns];
            for (int x = 0; x < grid.Columns; x++)
            {
                chars[x] = CharFor(grid[x, y]);
            }

            rows[r] = new string(chars);
        }

        return rows;
    }

    /// <summary>Bottom-anchored position: row 0 is the bottom row.</summary>
    public static Position FromBottom(Grid grid, int x, int rowFromBottom) => new(x, grid.Rows - 1 - rowFromBottom);

    public static Block BlockFor(char c) => c switch
    {
        'M' => Block.Of(AnimalType.Monkey),
        'R' => Block.Of(AnimalType.Rabbit),
        'P' => Block.Of(AnimalType.Panda),
        'D' => Block.Of(AnimalType.Dog),
        'K' => Block.Of(AnimalType.Cat),
        'b' => Block.Of(FoodType.Banana),
        'c' => Block.Of(FoodType.Carrot),
        'z' => Block.Of(FoodType.Bamboo),
        'o' => Block.Of(FoodType.Bone),
        'f' => Block.Of(FoodType.Fish),
        '$' => Block.Coin,
        _ => Block.Empty,
    };

    private static char CharFor(Block block)
    {
        foreach (char c in "MRPDKbczof$")
        {
            if (BlockFor(c) == block)
            {
                return c;
            }
        }

        return '.';
    }
}
