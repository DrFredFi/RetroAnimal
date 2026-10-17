using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class ScoringTests
{
    private static readonly GameRules Rules = GameRules.Default;

    private static ClearResult MonkeyEatsBananas(int bananas)
    {
        var row = "M" + new string('b', bananas);
        return MatchDetector.Find(TestGrid.Parse(Rules, row.PadRight(Rules.Columns, '.')), Rules);
    }

    [Fact]
    public void ClearingFood_IncreasesTheScore()
    {
        var score = new ScoreSystem(Rules);

        int points = score.AddClear(MonkeyEatsBananas(2), chain: 1);

        Assert.True(points > 0);
        Assert.Equal(points, score.Score);
        Assert.Equal(2, score.FoodCleared);
        Assert.Equal(2 * Rules.PointsPerFood + Rules.PointsPerAnimal, points);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void ChainMultiplier_IsApplied(int chain)
    {
        var score = new ScoreSystem(Rules);
        var clear = MonkeyEatsBananas(2);

        int basePoints = score.PointsFor(clear, 1);
        int chained = score.PointsFor(clear, chain);

        Assert.Equal(basePoints * Rules.ChainMultiplier(chain), chained);
    }

    [Fact]
    public void ChainMultipliers_GrowWithChainDepth()
    {
        for (int chain = 2; chain <= 10; chain++)
        {
            Assert.True(Rules.ChainMultiplier(chain) >= Rules.ChainMultiplier(chain - 1));
        }

        Assert.True(Rules.ChainMultiplier(2) > Rules.ChainMultiplier(1));
    }

    [Fact]
    public void BigGroups_EarnABonus()
    {
        var score = new ScoreSystem(Rules);

        int small = score.PointsFor(MonkeyEatsBananas(3), 1);
        int big = score.PointsFor(MonkeyEatsBananas(5), 1);

        Assert.Equal(small + 2 * Rules.PointsPerFood + 2 * Rules.BigGroupBonusPerFood, big);
    }

    [Fact]
    public void Score_CannotBecomeNegative()
    {
        var score = new ScoreSystem(Rules);
        score.Add(50);

        score.Add(-1000);
        score.AddDrop(-5, hardDrop: true);

        Assert.Equal(0, score.Score);
    }

    [Fact]
    public void Score_SaturatesInsteadOfOverflowing()
    {
        var score = new ScoreSystem(Rules);
        score.Add(int.MaxValue);
        score.Add(int.MaxValue);

        Assert.Equal(int.MaxValue, score.Score);
    }

    [Fact]
    public void Drops_AwardSmallBonuses()
    {
        var score = new ScoreSystem(Rules);

        score.AddDrop(10, hardDrop: true);
        score.AddDrop(4, hardDrop: false);

        Assert.Equal(10 * Rules.HardDropPointsPerRow + 4 * Rules.SoftDropPointsPerRow, score.Score);
    }

    [Fact]
    public void LevelProgression_FollowsFoodTargets()
    {
        var levels = new LevelSystem(Rules);

        Assert.Equal(1, levels.Level);
        Assert.Equal(0, levels.Update(Rules.FoodPerLevel - 1));
        Assert.Equal(1, levels.Update(Rules.FoodPerLevel));
        Assert.Equal(2, levels.Level);
        Assert.Equal(3, levels.Update(Rules.FoodPerLevel * 4));
        Assert.Equal(5, levels.Level);
    }

    [Fact]
    public void Level_IsCappedAtMaximum()
    {
        var levels = new LevelSystem(Rules);

        levels.Update(int.MaxValue / 2);

        Assert.Equal(Rules.MaxLevel, levels.Level);
    }

    [Fact]
    public void HigherLevels_FallFaster()
    {
        for (int level = 2; level <= Rules.MaxLevel; level++)
        {
            Assert.True(Rules.FallInterval(level) <= Rules.FallInterval(level - 1));
        }

        Assert.True(Rules.FallInterval(Rules.MaxLevel) >= Rules.MinFallInterval);
    }

    [Fact]
    public void Garbage_ScalesWithFoodAndChain()
    {
        var clear = MonkeyEatsBananas(4);

        Assert.Equal(4 / Rules.GarbageDivisor, MatchResolver.GarbageFor(clear, 1, Rules));
        Assert.Equal(4 * 3 / Rules.GarbageDivisor, MatchResolver.GarbageFor(clear, 3, Rules));
    }
}
