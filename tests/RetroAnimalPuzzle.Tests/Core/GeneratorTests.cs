using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Tests.Core;

public class GeneratorTests
{
    private static readonly GameRules Rules = GameRules.Default;

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new RandomPieceGenerator(Rules, 1234);
        var b = new RandomPieceGenerator(Rules, 1234);

        for (int i = 0; i < 500; i++)
        {
            Assert.Equal(a.Next(), b.Next());
        }
    }

    [Fact]
    public void Peek_DoesNotConsume()
    {
        var generator = new RandomPieceGenerator(Rules, 9);

        var first = generator.Peek(0);
        var second = generator.Peek(1);

        Assert.Equal(first, generator.Next());
        Assert.Equal(second, generator.Next());
    }

    [Fact]
    public void Distribution_IsBalancedAcrossTypes()
    {
        var generator = new RandomPieceGenerator(Rules with { CoinChance = 0 }, 77);
        var counts = new Dictionary<Block, int>();
        for (int i = 0; i < 2000; i++)
        {
            var pair = generator.Next();
            counts[pair.Pivot] = counts.GetValueOrDefault(pair.Pivot) + 1;
            counts[pair.Satellite] = counts.GetValueOrDefault(pair.Satellite) + 1;
        }

        int bag = Rules.Animals.Count * (Rules.AnimalsPerBag + Rules.FoodPerBag);
        double bags = 4000.0 / bag;
        foreach (var animal in Rules.Animals)
        {
            Assert.InRange(counts[Block.Of(animal)], bags * Rules.AnimalsPerBag - Rules.AnimalsPerBag, bags * Rules.AnimalsPerBag + Rules.AnimalsPerBag);
            var food = Block.Of(Rules.FoodFor(animal));
            Assert.InRange(counts[food], bags * Rules.FoodPerBag - Rules.FoodPerBag, bags * Rules.FoodPerBag + Rules.FoodPerBag);
        }

        Assert.DoesNotContain(counts.Keys, b => b.IsAnimal && !Rules.Animals.Contains(b.Animal));
    }

    [Fact]
    public void Coins_NeverAppearEarly_AndNeverTwoPerPair()
    {
        var generator = new RandomPieceGenerator(Rules with { CoinChance = 0.5 }, 5);

        for (int i = 0; i < Rules.PiecesBeforeFirstCoin; i++)
        {
            var pair = generator.Next();
            Assert.False(pair.Pivot.IsCoin || pair.Satellite.IsCoin);
        }

        int coins = 0;
        for (int i = 0; i < 1000; i++)
        {
            var pair = generator.Next();
            Assert.False(pair.Pivot.IsCoin && pair.Satellite.IsCoin);
            if (pair.Pivot.IsCoin || pair.Satellite.IsCoin) coins++;
        }

        Assert.InRange(coins, 400, 600);
    }
}
