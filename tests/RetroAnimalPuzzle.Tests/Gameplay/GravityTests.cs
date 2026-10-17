using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class GravityTests
{
    private static readonly GameRules Rules = GameRules.Default;

    [Fact]
    public void UnsupportedBlocks_Fall()
    {
        var grid = TestGrid.Parse(Rules,
            "M.....",
            "......",
            "......");

        var falls = GravitySystem.Apply(grid);

        Assert.Single(falls);
        Assert.Equal(2, falls[0].Distance);
        Assert.Equal(["......", "......", "M....."], TestGrid.Dump(grid, 3));
    }

    [Fact]
    public void SupportedBlocks_StayInPlace()
    {
        var grid = TestGrid.Parse(Rules,
            "b.....",
            "M..o..");

        var falls = GravitySystem.Apply(grid);

        Assert.Empty(falls);
        Assert.False(GravitySystem.HasUnsupportedBlocks(grid));
    }

    [Fact]
    public void Gravity_PreservesOrderWithinAColumn()
    {
        var grid = TestGrid.Parse(Rules,
            "M.....",
            "......",
            "c.....",
            "......",
            "o.....");

        GravitySystem.Apply(grid);

        Assert.Equal(["......", "......", "M.....", "c.....", "o....."], TestGrid.Dump(grid, 5));
    }

    [Fact]
    public void GravityAndMatching_ProduceChainReactions()
    {
        // 1) The monkey eats the banana and leaves. 2) The carrot drops next to the rabbit. 3) The rabbit eats it.
        var grid = TestGrid.Parse(Rules,
            ".c....",
            "RbM...");

        var steps = MatchResolver.ResolveAll(grid, Rules);

        Assert.Equal(2, steps.Count);
        Assert.Equal(1, steps[0].Chain);
        Assert.Equal(AnimalType.Monkey, steps[0].Result.Groups[0].Animal);
        Assert.Equal(2, steps[1].Chain);
        Assert.Equal(AnimalType.Rabbit, steps[1].Result.Groups[0].Animal);
        Assert.Equal(0, grid.CountBlocks());
    }

    [Fact]
    public void ThreeStepChain_IsResolvedAndTerminates()
    {
        // Monkey eats banana → carrot drops beside rabbit → rabbit eats and leaves → bone drops beside dog.
        var grid = TestGrid.Parse(Rules,
            ".oc...",
            "DRbM..");

        var steps = MatchResolver.ResolveAll(grid, Rules);

        Assert.Equal(3, steps.Count);
        Assert.Equal([1, 2, 3], steps.Select(s => s.Chain));
    }
}
