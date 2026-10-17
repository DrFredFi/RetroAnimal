using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>One step of a chain: what was cleared and at which chain depth (1 = first clear).</summary>
public sealed record ChainStep(int Chain, ClearResult Result);

/// <summary>
/// Synchronous chain resolution: gravity → find → clear → gravity → ... until the grid is stable.
/// The live board performs the same steps one at a time with animations in between; this helper is
/// used by the CPU planner and by tests. A hard step limit guards against any infinite loop.
/// </summary>
public static class MatchResolver
{
    public const int MaxSteps = 64;

    public static List<ChainStep> ResolveAll(Grid grid, GameRules rules)
    {
        var steps = new List<ChainStep>();
        GravitySystem.Apply(grid);
        for (int chain = 1; chain <= MaxSteps; chain++)
        {
            var result = MatchDetector.Find(grid, rules);
            if (result.IsEmpty)
            {
                break;
            }

            steps.Add(new ChainStep(chain, result));
            MatchDetector.Apply(grid, result);
            GravitySystem.Apply(grid);
        }

        return steps;
    }

    /// <summary>Garbage a clear step sends to the opponent in versus play.</summary>
    public static int GarbageFor(ClearResult result, int chain, GameRules rules) =>
        result.FoodCleared * Math.Max(1, chain) / Math.Max(1, rules.GarbageDivisor);
}
