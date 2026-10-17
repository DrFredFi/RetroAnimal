using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>One connected group of food eaten by the compatible animals touching it.</summary>
public sealed record EatGroup(AnimalType Animal, FoodType Food, IReadOnlyList<Position> Animals, IReadOnlyList<Position> FoodCells)
{
    private IReadOnlyList<EaterRoute>? _routes;

    /// <summary>How each eating animal walks over the food (one route per animal, same order as <see cref="Animals"/>).</summary>
    public IReadOnlyList<EaterRoute> Routes => _routes ??= EatRoutes.Plan(Animals, FoodCells);
}

/// <summary>A coin that landed on a block and removes every block of that exact type.</summary>
public sealed record CoinClear(Position Coin, Block Target, IReadOnlyList<Position> Cleared);

/// <summary>Everything that disappears in one resolution step.</summary>
public sealed record ClearResult(
    IReadOnlyList<Position> Cells,
    IReadOnlyList<EatGroup> Groups,
    IReadOnlyList<CoinClear> Coins,
    int FoodCleared,
    int AnimalsCleared,
    int CoinsCleared)
{
    public static ClearResult None { get; } = new([], [], [], 0, 0, 0);

    public bool IsEmpty => Cells.Count == 0;

    public int TotalCleared => Cells.Count;
}

/// <summary>
/// Finds what disappears on the current grid. Rules:
/// <list type="bullet">
/// <item>An animal orthogonally adjacent to its food eats the whole connected group of that food
/// (food blocks of the same type joined orthogonally), and then the animal disappears as well.</item>
/// <item>Contact works both ways: it does not matter whether the animal or the food arrived last.</item>
/// <item>Diagonal contact never counts; incompatible animals never eat.</item>
/// <item>A coin resting on a block removes every block on the board identical to that block, then itself.
/// A coin on the floor or on another coin simply vanishes.</item>
/// </list>
/// The scan order is fixed (row-major) so results are deterministic.
/// </summary>
public static class MatchDetector
{
    private static readonly (int Dx, int Dy)[] Neighbours = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    public static ClearResult Find(Grid grid, GameRules rules)
    {
        int columns = grid.Columns;
        var marked = new bool[columns * grid.Rows];
        var coins = FindCoinClears(grid, marked);
        var groups = FindEatGroups(grid, rules, marked);

        if (coins.Count == 0 && groups.Count == 0)
        {
            return ClearResult.None;
        }

        var cells = new List<Position>();
        int food = 0, animals = 0, coinCount = 0;
        for (int y = 0; y < grid.Rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (!marked[y * columns + x])
                {
                    continue;
                }

                cells.Add(new Position(x, y));
                switch (grid[x, y].Kind)
                {
                    case BlockKind.Food: food++; break;
                    case BlockKind.Animal: animals++; break;
                    case BlockKind.Coin: coinCount++; break;
                }
            }
        }

        return new ClearResult(cells, groups, coins, food, animals, coinCount);
    }

    /// <summary>Removes the cleared cells from the grid.</summary>
    public static void Apply(Grid grid, ClearResult result)
    {
        foreach (var cell in result.Cells)
        {
            grid.Clear(cell);
        }
    }

    private static List<CoinClear> FindCoinClears(Grid grid, bool[] marked)
    {
        var result = new List<CoinClear>();
        for (int y = 0; y < grid.Rows; y++)
        {
            for (int x = 0; x < grid.Columns; x++)
            {
                if (!grid[x, y].IsCoin)
                {
                    continue;
                }

                var coin = new Position(x, y);
                var target = grid[x, y + 1];
                var cleared = new List<Position>();
                if (!target.IsEmpty && !target.IsCoin)
                {
                    for (int ty = 0; ty < grid.Rows; ty++)
                    {
                        for (int tx = 0; tx < grid.Columns; tx++)
                        {
                            if (grid[tx, ty] == target)
                            {
                                cleared.Add(new Position(tx, ty));
                                marked[ty * grid.Columns + tx] = true;
                            }
                        }
                    }
                }

                marked[y * grid.Columns + x] = true;
                result.Add(new CoinClear(coin, target, cleared));
            }
        }

        return result;
    }

    private static List<EatGroup> FindEatGroups(Grid grid, GameRules rules, bool[] marked)
    {
        int columns = grid.Columns;
        var visited = new bool[columns * grid.Rows];
        var groups = new List<EatGroup>();
        var queue = new Queue<Position>();

        for (int y = 0; y < grid.Rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var block = grid[x, y];
                if (!block.IsFood || visited[y * columns + x])
                {
                    continue;
                }

                // Breadth-first search over the connected component of this food type.
                var food = new List<Position>();
                queue.Enqueue(new Position(x, y));
                visited[y * columns + x] = true;
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    food.Add(p);
                    foreach (var (dx, dy) in Neighbours)
                    {
                        var n = p.Offset(dx, dy);
                        if (grid.InBounds(n) && !visited[n.Y * columns + n.X] && grid[n] == block)
                        {
                            visited[n.Y * columns + n.X] = true;
                            queue.Enqueue(n);
                        }
                    }
                }

                var eaters = FindEaters(grid, rules, block.Food, food);
                if (eaters.Animals.Count == 0)
                {
                    continue;
                }

                foreach (var p in food)
                {
                    marked[p.Y * columns + p.X] = true;
                }

                foreach (var p in eaters.Animals)
                {
                    marked[p.Y * columns + p.X] = true;
                }

                food.Sort(RowMajor);
                groups.Add(new EatGroup(eaters.Animal, block.Food, eaters.Animals, food));
            }
        }

        return groups;
    }

    private static (AnimalType Animal, List<Position> Animals) FindEaters(Grid grid, GameRules rules, FoodType food, List<Position> foodCells)
    {
        var animals = new List<Position>();
        AnimalType eater = default;
        foreach (var p in foodCells)
        {
            foreach (var (dx, dy) in Neighbours)
            {
                var n = p.Offset(dx, dy);
                var neighbour = grid[n];
                if (neighbour.IsAnimal && rules.Diet.TryGetValue(neighbour.Animal, out var diet) && diet == food && !animals.Contains(n))
                {
                    eater = neighbour.Animal;
                    animals.Add(n);
                }
            }
        }

        animals.Sort(RowMajor);
        return (eater, animals);
    }

    private static int RowMajor(Position a, Position b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
}
