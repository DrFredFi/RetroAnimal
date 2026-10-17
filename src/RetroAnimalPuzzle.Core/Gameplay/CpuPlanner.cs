using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>Where the CPU wants to put the current pair.</summary>
public readonly record struct CpuTarget(int PivotX, Orientation Orientation);

/// <summary>
/// Brute-force placement search: tries every column × orientation, simulates the drop and the full
/// chain resolution on a scratch grid, and scores the outcome (points, garbage, stack height, shape,
/// food grouping). Skill noise and occasional blunders make weaker opponents beatable.
/// </summary>
public sealed class CpuPlanner
{
    private readonly GameRules _rules;
    private readonly Grid _scratch;

    public CpuPlanner(GameRules rules)
    {
        _rules = rules;
        _scratch = Grid.For(rules);
    }

    public CpuTarget Choose(Grid grid, Piece piece, CpuSkill skill, Random random)
    {
        var candidates = new List<(CpuTarget Target, double Score)>();
        foreach (var orientation in Enum.GetValues<Orientation>())
        {
            for (int x = 0; x < grid.Columns; x++)
            {
                var candidate = new Piece(piece.Blocks, new Position(x, piece.Pivot.Y), orientation);
                if (!CollisionSystem.Fits(grid, candidate) || !IsReachable(grid, piece.WithOrientation(orientation), x))
                {
                    continue;
                }

                double score = Evaluate(grid, candidate) + random.NextDouble() * skill.Noise;
                candidates.Add((new CpuTarget(x, orientation), score));
            }
        }

        if (candidates.Count == 0)
        {
            return new CpuTarget(piece.Pivot.X, piece.Orientation);
        }

        if (random.NextDouble() < skill.BlunderChance)
        {
            return candidates[random.Next(candidates.Count)].Target;
        }

        return candidates.MaxBy(c => c.Score).Target;
    }

    /// <summary>Scores dropping the piece at its given column/orientation (higher is better).</summary>
    public double Evaluate(Grid grid, Piece candidate)
    {
        var landed = CollisionSystem.DropPosition(grid, candidate);
        _scratch.CopyFrom(grid);
        _scratch.Place(landed.Pivot, landed.Blocks.Pivot);
        _scratch.Place(landed.Satellite, landed.Blocks.Satellite);
        var steps = MatchResolver.ResolveAll(_scratch, _rules);

        double score = 0;
        foreach (var step in steps)
        {
            var r = step.Result;
            score += (r.FoodCleared * 10 + r.AnimalsCleared * 4 + r.CoinsCleared * 2) * _rules.ChainMultiplier(step.Chain);
            score += MatchResolver.GarbageFor(r, step.Chain, _rules) * 6;
        }

        if (_scratch.HasBlocksInHiddenRows())
        {
            return score - 100_000;
        }

        int maxHeight = 0;
        int bumpiness = 0;
        int previous = -1;
        for (int x = 0; x < _scratch.Columns; x++)
        {
            int h = _scratch.ColumnHeight(x);
            maxHeight = Math.Max(maxHeight, h);
            if (previous >= 0)
            {
                bumpiness += Math.Abs(h - previous);
            }

            previous = h;
        }

        score -= maxHeight * maxHeight * 0.9;
        score -= bumpiness * 1.2;

        int spawnDanger = Math.Max(_scratch.ColumnHeight(_rules.SpawnColumn), _scratch.ColumnHeight(_rules.SpawnColumn + 1));
        if (spawnDanger >= _rules.VisibleRows - 1)
        {
            score -= 4_000;
        }

        score += SetupPotential(_scratch);
        return score;
    }

    /// <summary>Rewards grouped food (bigger future meals) and animals sitting right above their food column.</summary>
    private double SetupPotential(Grid grid)
    {
        double score = 0;
        for (int y = 0; y < grid.Rows; y++)
        {
            for (int x = 0; x < grid.Columns; x++)
            {
                var block = grid[x, y];
                if (block.IsFood)
                {
                    if (grid[x + 1, y] == block) score += 2.0;
                    if (grid[x, y + 1] == block) score += 2.0;
                }
                else if (block.IsAnimal)
                {
                    // An animal buried under foreign food is hard to use later.
                    var above = grid[x, y - 1];
                    if (above.IsFood && above.Food != _rules.FoodFor(block.Animal))
                    {
                        score -= 1.5;
                    }
                }
            }
        }

        return score;
    }

    private static bool IsReachable(Grid grid, Piece piece, int targetX)
    {
        if (!CollisionSystem.Fits(grid, piece))
        {
            return false;
        }

        int step = Math.Sign(targetX - piece.Pivot.X);
        while (piece.Pivot.X != targetX)
        {
            piece = piece.MovedBy(step, 0);
            if (!CollisionSystem.Fits(grid, piece))
            {
                return false;
            }
        }

        return true;
    }
}
