using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>
/// Owns the score and every scoring formula. Points for a clear step:
/// <c>(food × PointsPerFood + animals × PointsPerAnimal + coins × PointsPerCoinClear + big-group bonus) × chain multiplier</c>.
/// </summary>
public sealed class ScoreSystem
{
    private readonly GameRules _rules;

    public ScoreSystem(GameRules rules)
    {
        _rules = rules;
    }

    public int Score { get; private set; }

    public int FoodCleared { get; private set; }

    public int BestChain { get; private set; }

    public void Reset()
    {
        Score = 0;
        FoodCleared = 0;
        BestChain = 0;
    }

    /// <summary>Points that a clear at the given chain depth is worth, without applying them.</summary>
    public int PointsFor(ClearResult result, int chain)
    {
        if (result.IsEmpty)
        {
            return 0;
        }

        int bigGroupBonus = 0;
        foreach (var group in result.Groups)
        {
            bigGroupBonus += Math.Max(0, group.FoodCells.Count - 3) * _rules.BigGroupBonusPerFood;
        }

        int basePoints = result.FoodCleared * _rules.PointsPerFood
            + result.AnimalsCleared * _rules.PointsPerAnimal
            + result.CoinsCleared * _rules.PointsPerCoinClear
            + bigGroupBonus;
        return basePoints * _rules.ChainMultiplier(chain);
    }

    /// <summary>Applies a clear step and returns the points awarded.</summary>
    public int AddClear(ClearResult result, int chain)
    {
        int points = PointsFor(result, chain);
        FoodCleared += result.FoodCleared;
        BestChain = Math.Max(BestChain, chain);
        Add(points);
        return points;
    }

    public int AddDrop(int rows, bool hardDrop)
    {
        int points = Math.Max(0, rows) * (hardDrop ? _rules.HardDropPointsPerRow : _rules.SoftDropPointsPerRow);
        Add(points);
        return points;
    }

    public int AddLevelBonus(int completedLevel)
    {
        int points = _rules.LevelCompleteBonus * Math.Max(1, completedLevel);
        Add(points);
        return points;
    }

    /// <summary>Adds points; the score saturates instead of overflowing and never goes negative.</summary>
    public void Add(int points)
    {
        long total = (long)Score + points;
        Score = (int)Math.Clamp(total, 0, int.MaxValue);
    }
}
