namespace RetroAnimalPuzzle.Core;

/// <summary>
/// Deterministic, balanced piece source. Blocks are dealt from shuffled "bags" containing a fixed mix of
/// every animal and food in play, so no type can be starved for long. Coins replace one block of a pair
/// with a small, configurable probability. The same seed always produces the same sequence.
/// </summary>
public sealed class RandomPieceGenerator
{
    private readonly GameRules _rules;
    private readonly Random _random;
    private readonly List<Block> _bag = [];
    private readonly List<PiecePair> _queue = [];
    private int _bagIndex;
    private int _generated;

    public RandomPieceGenerator(GameRules rules, int seed)
    {
        _rules = rules;
        _random = new Random(seed);
    }

    /// <summary>Test hook: these pairs are dealt first, in order, before random generation takes over.</summary>
    internal void Script(params PiecePair[] pairs)
    {
        _queue.Clear();
        _queue.AddRange(pairs);
    }

    /// <summary>Removes and returns the next pair.</summary>
    public PiecePair Next()
    {
        EnsureQueued(1);
        var pair = _queue[0];
        _queue.RemoveAt(0);
        return pair;
    }

    /// <summary>Returns an upcoming pair without consuming it (0 = the next one).</summary>
    public PiecePair Peek(int index)
    {
        EnsureQueued(index + 1);
        return _queue[index];
    }

    /// <summary>Random food from the foods in play; used for versus garbage.</summary>
    public Block RandomFood()
    {
        var foods = _rules.Animals;
        return Block.Of(_rules.FoodFor(foods[_random.Next(foods.Count)]));
    }

    private void EnsureQueued(int count)
    {
        while (_queue.Count < count)
        {
            _queue.Add(Generate());
        }
    }

    private PiecePair Generate()
    {
        var pivot = DrawFromBag();
        var satellite = DrawFromBag();
        _generated++;

        if (_generated > _rules.PiecesBeforeFirstCoin && _random.NextDouble() < _rules.CoinChance)
        {
            if (_random.Next(2) == 0)
            {
                pivot = Block.Coin;
            }
            else
            {
                satellite = Block.Coin;
            }
        }

        return new PiecePair(pivot, satellite);
    }

    private Block DrawFromBag()
    {
        if (_bagIndex >= _bag.Count)
        {
            RefillBag();
        }

        return _bag[_bagIndex++];
    }

    private void RefillBag()
    {
        _bag.Clear();
        _bagIndex = 0;
        foreach (var animal in _rules.Animals)
        {
            for (int i = 0; i < _rules.AnimalsPerBag; i++)
            {
                _bag.Add(Block.Of(animal));
            }

            for (int i = 0; i < _rules.FoodPerBag; i++)
            {
                _bag.Add(Block.Of(_rules.FoodFor(animal)));
            }
        }

        // Fisher–Yates shuffle.
        for (int i = _bag.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
        }
    }
}
