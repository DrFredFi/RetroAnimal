using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class EatRouteTests
{
    private static readonly GameRules Rules = GameRules.Default;

    private static int Distance(Position a, Position b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    [Fact]
    public void Route_VisitsEveryFoodTileOnce_StartingNextToTheAnimal()
    {
        var grid = TestGrid.Parse(Rules,
            ".zzz..",
            ".z....",
            "Pz....");
        var group = Assert.Single(MatchDetector.Find(grid, Rules).Groups);

        var route = Assert.Single(group.Routes);

        Assert.Equal(group.FoodCells.OrderBy(p => p.Y).ThenBy(p => p.X), route.Path.OrderBy(p => p.Y).ThenBy(p => p.X));
        Assert.Equal(1, Distance(route.Animal, route.Path[0]));
    }

    [Fact]
    public void Route_WalksBetweenNeighbouringTiles_WhenTheGroupIsAChain()
    {
        var grid = TestGrid.Parse(Rules,
            "..bbb.",
            "..b...",
            "Mbb...");
        var route = Assert.Single(Assert.Single(MatchDetector.Find(grid, Rules).Groups).Routes);

        for (int i = 1; i < route.Path.Count; i++)
        {
            Assert.Equal(1, Distance(route.Path[i - 1], route.Path[i]));
        }
    }

    [Fact]
    public void SeveralEaters_ShareTheGroupWithoutOverlap()
    {
        var grid = TestGrid.Parse(Rules, "MbbbbM");
        var group = Assert.Single(MatchDetector.Find(grid, Rules).Groups);

        Assert.Equal(2, group.Routes.Count);
        var all = group.Routes.SelectMany(r => r.Path).ToList();
        Assert.Equal(4, all.Count);
        Assert.Equal(4, all.Distinct().Count());
        Assert.All(group.Routes, r => Assert.Equal(2, r.Path.Count));
    }

    [Fact]
    public void EatDuration_GrowsWithTheLongestRoute()
    {
        var small = MatchDetector.Find(TestGrid.Parse(Rules, "Mb...."), Rules);
        var big = MatchDetector.Find(TestGrid.Parse(Rules, "Mbbbbb"), Rules);

        Assert.Equal(Rules.EatStartDelay + Rules.EatSecondsPerFood + Rules.EatEndDelay, Rules.EatDuration(small), 6);
        Assert.Equal(Rules.EatStartDelay + 5 * Rules.EatSecondsPerFood + Rules.EatEndDelay, Rules.EatDuration(big), 6);
    }

    [Fact]
    public void CoinOnlyClear_UsesTheCoinDuration()
    {
        var clear = MatchDetector.Find(TestGrid.Parse(Rules, "$....."), Rules);

        Assert.Equal(Rules.CoinClearDuration, Rules.EatDuration(clear), 6);
    }
}
