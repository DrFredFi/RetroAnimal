using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class MatchingTests
{
    private static readonly GameRules Rules = GameRules.Default;

    private static string[] ResolveOnce(Grid grid, int rows)
    {
        var result = MatchDetector.Find(grid, Rules);
        MatchDetector.Apply(grid, result);
        return TestGrid.Dump(grid, rows);
    }

    [Fact]
    public void CompatibleAnimal_EatsAdjacentFood_AndDisappears()
    {
        var grid = TestGrid.Parse(Rules, "Mb....");

        var result = MatchDetector.Find(grid, Rules);

        Assert.Equal(1, result.FoodCleared);
        Assert.Equal(1, result.AnimalsCleared);
        Assert.Single(result.Groups);
        Assert.Equal(AnimalType.Monkey, result.Groups[0].Animal);
        Assert.Equal(["......"], ResolveOnce(grid, 1));
    }

    [Fact]
    public void FoodOnTopOfAnimal_IsAlsoEaten()
    {
        var grid = TestGrid.Parse(Rules,
            "c.....",
            "R.....");

        var result = MatchDetector.Find(grid, Rules);

        Assert.Equal(1, result.FoodCleared);
        Assert.Equal(1, result.AnimalsCleared);
    }

    [Fact]
    public void IncompatibleAnimal_DoesNotEat()
    {
        var grid = TestGrid.Parse(Rules, "Mc.Rb.");

        var result = MatchDetector.Find(grid, Rules);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void DiagonalContact_DoesNotCount()
    {
        var grid = TestGrid.Parse(Rules,
            ".b....",
            "M.....");

        Assert.True(MatchDetector.Find(grid, Rules).IsEmpty);
    }

    [Fact]
    public void ConnectedFoodGroup_IsEatenEntirely()
    {
        var grid = TestGrid.Parse(Rules,
            ".zz...",
            ".z.z..",
            "Pz....");

        var result = MatchDetector.Find(grid, Rules);

        // The bamboo at (3, row 1) touches the group only diagonally and must survive.
        Assert.Equal(4, result.FoodCleared);
        Assert.Equal(["......", "...z..", "......"], ResolveOnce(grid, 3));
    }

    [Fact]
    public void IndependentMatches_AreAllDetected()
    {
        var grid = TestGrid.Parse(Rules,
            "Do..cR",
            "Mb..Kf");

        var result = MatchDetector.Find(grid, Rules);

        Assert.Equal(4, result.Groups.Count);
        Assert.Equal(4, result.FoodCleared);
        Assert.Equal(4, result.AnimalsCleared);
    }

    [Fact]
    public void UnrelatedFood_IsNotRemoved()
    {
        var grid = TestGrid.Parse(Rules,
            "cc....",
            "Mbo...");

        Assert.Equal(["cc....", "..o..."], ResolveOnce(grid, 2));
    }

    [Fact]
    public void AnimalsWithoutFood_StayInTheGrid()
    {
        var grid = TestGrid.Parse(Rules, "MbR.P.");

        Assert.Equal(["..R.P."], ResolveOnce(grid, 1));
    }

    [Fact]
    public void SeveralAnimals_ShareOneGroup()
    {
        var grid = TestGrid.Parse(Rules, "MbbM..");

        var result = MatchDetector.Find(grid, Rules);

        Assert.Single(result.Groups);
        Assert.Equal(2, result.Groups[0].Animals.Count);
        Assert.Equal(2, result.FoodCleared);
    }

    [Fact]
    public void CoinOnBlock_RemovesEveryBlockOfThatType()
    {
        var grid = TestGrid.Parse(Rules,
            "..$...",
            "b.b.c.",
            "Dcb.bz");

        var result = MatchDetector.Find(grid, Rules);

        Assert.Single(result.Coins);
        Assert.Equal(Block.Of(FoodType.Banana), result.Coins[0].Target);
        Assert.Equal(["......", "....c.", "Dc...z"], ResolveOnce(grid, 3));
    }

    [Fact]
    public void CoinOnFloor_JustVanishes()
    {
        var grid = TestGrid.Parse(Rules, "$b....");

        var result = MatchDetector.Find(grid, Rules);

        Assert.Equal(1, result.CoinsCleared);
        Assert.Equal(0, result.FoodCleared);
        Assert.Equal([".b...."], ResolveOnce(grid, 1));
    }

    [Fact]
    public void Detection_IsDeterministic()
    {
        var a = MatchDetector.Find(TestGrid.Parse(Rules, "Mb.Dob", "bM.oDM"), Rules);
        var b = MatchDetector.Find(TestGrid.Parse(Rules, "Mb.Dob", "bM.oDM"), Rules);

        Assert.Equal(a.Cells, b.Cells);
        Assert.Equal(a.Groups.Select(g => g.FoodCells.ToArray()), b.Groups.Select(g => g.FoodCells.ToArray()));
    }
}
