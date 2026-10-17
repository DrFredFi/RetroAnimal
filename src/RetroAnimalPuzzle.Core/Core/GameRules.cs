namespace RetroAnimalPuzzle.Core;

/// <summary>
/// Every tunable gameplay number lives here: grid size, animal/food pairing, piece generation,
/// timing, scoring and versus garbage. Tests create custom instances; the game uses <see cref="Default"/>.
/// </summary>
public sealed record GameRules
{
    public static GameRules Default { get; } = new();

    // ---- Grid ------------------------------------------------------------------------------
    public int Columns { get; init; } = 6;
    public int VisibleRows { get; init; } = 11;
    public int HiddenRows { get; init; } = 2;
    public int TotalRows => VisibleRows + HiddenRows;

    /// <summary>Column of the left block of a freshly spawned (horizontal) pair.</summary>
    public int SpawnColumn { get; init; } = 2;

    // ---- Animals and food ------------------------------------------------------------------
    /// <summary>Animals that appear in play. Each must have an entry in <see cref="Diet"/>.</summary>
    public IReadOnlyList<AnimalType> Animals { get; init; } =
        [AnimalType.Monkey, AnimalType.Rabbit, AnimalType.Panda, AnimalType.Dog];

    /// <summary>Which food each animal eats. Original design choice; adjustable.</summary>
    public IReadOnlyDictionary<AnimalType, FoodType> Diet { get; init; } = new Dictionary<AnimalType, FoodType>
    {
        [AnimalType.Monkey] = FoodType.Banana,
        [AnimalType.Rabbit] = FoodType.Carrot,
        [AnimalType.Panda] = FoodType.Bamboo,
        [AnimalType.Dog] = FoodType.Bone,
        [AnimalType.Cat] = FoodType.Fish,
    };

    public FoodType FoodFor(AnimalType animal) => Diet[animal];

    public IEnumerable<FoodType> FoodsInPlay => Animals.Select(FoodFor);

    // ---- Piece generation ------------------------------------------------------------------
    /// <summary>Copies of each animal placed in every shuffled bag.</summary>
    public int AnimalsPerBag { get; init; } = 2;

    /// <summary>Copies of each food placed in every shuffled bag.</summary>
    public int FoodPerBag { get; init; } = 3;

    /// <summary>Probability that a piece carries a coin (0 disables coins).</summary>
    public double CoinChance { get; init; } = 0.05;

    /// <summary>No coins are generated among the first pieces of a game.</summary>
    public int PiecesBeforeFirstCoin { get; init; } = 8;

    // ---- Timing (seconds) ------------------------------------------------------------------
    public double LockDelay { get; init; } = 0.3;
    public double AutoRepeatDelay { get; init; } = 0.17;
    public double AutoRepeatInterval { get; init; } = 0.055;
    public double SettleCellsPerSecond { get; init; } = 22.0;

    // Soft drop accelerates the longer Down is held, from the start interval to the minimum.
    public double SoftDropStartInterval { get; init; } = 0.11;
    public double SoftDropMinInterval { get; init; } = 0.02;
    public double SoftDropRampSeconds { get; init; } = 0.7;

    /// <summary>Instant drop to the bottom. Off by default: in the classic game you can only accelerate.</summary>
    public bool HardDropEnabled { get; init; }

    /// <summary>Seconds per row while soft-dropping, after Down has been held for <paramref name="heldSeconds"/>.</summary>
    public double SoftDropInterval(double heldSeconds)
    {
        double k = SoftDropRampSeconds <= 0 ? 1 : Math.Clamp(heldSeconds / SoftDropRampSeconds, 0, 1);
        // Ease-in so the speed-up is felt progressively rather than linearly.
        k = k * k;
        return SoftDropStartInterval + (SoftDropMinInterval - SoftDropStartInterval) * k;
    }

    // Eating: a short pop, then the animal walks over each food tile of its route, then a short pause.
    public double EatStartDelay { get; init; } = 0.18;
    public double EatSecondsPerFood { get; init; } = 0.14;
    public double EatEndDelay { get; init; } = 0.12;
    public double CoinClearDuration { get; init; } = 0.55;

    /// <summary>How long the eating phase of a clear lasts: driven by the longest eating route.</summary>
    public double EatDuration(Gameplay.ClearResult result)
    {
        int longest = 0;
        foreach (var group in result.Groups)
        {
            foreach (var route in group.Routes)
            {
                longest = Math.Max(longest, route.Path.Count);
            }
        }

        double eating = result.Groups.Count == 0 ? 0 : EatStartDelay + longest * EatSecondsPerFood + EatEndDelay;
        return Math.Max(eating, result.Coins.Count > 0 ? CoinClearDuration : 0);
    }
    public double SpawnDelay { get; init; } = 0.08;

    public double BaseFallInterval { get; init; } = 0.85;
    public double FallSpeedUpPerLevel { get; init; } = 0.86;
    public double MinFallInterval { get; init; } = 0.06;

    /// <summary>Seconds per one-cell step at the given speed level (1 = slowest).</summary>
    public double FallInterval(int speedLevel)
    {
        double interval = BaseFallInterval * Math.Pow(FallSpeedUpPerLevel, Math.Max(0, speedLevel - 1));
        return Math.Max(MinFallInterval, interval);
    }

    // ---- Scoring ---------------------------------------------------------------------------
    public int PointsPerFood { get; init; } = 10;
    public int PointsPerAnimal { get; init; } = 20;
    public int PointsPerCoinClear { get; init; } = 5;

    /// <summary>Extra points per food beyond the third in one connected group.</summary>
    public int BigGroupBonusPerFood { get; init; } = 10;

    public int SoftDropPointsPerRow { get; init; } = 1;
    public int HardDropPointsPerRow { get; init; } = 2;
    public int LevelCompleteBonus { get; init; } = 250;

    /// <summary>Score multiplier for chain step 1, 2, 3...; the last value repeats.</summary>
    public IReadOnlyList<int> ChainMultipliers { get; init; } = [1, 2, 4, 6, 8, 12, 16];

    public int ChainMultiplier(int chain) =>
        ChainMultipliers[Math.Clamp(chain, 1, ChainMultipliers.Count) - 1];

    // ---- Levels (single player) ------------------------------------------------------------
    public int FoodPerLevel { get; init; } = 15;
    public int MaxLevel { get; init; } = 20;

    // ---- Versus ----------------------------------------------------------------------------
    /// <summary>Garbage sent for one clear step: (food eaten × chain) / divisor, rounded down.</summary>
    public int GarbageDivisor { get; init; } = 2;

    /// <summary>Maximum garbage blocks dropped onto a board at once (fits in the hidden rows).</summary>
    public int MaxGarbagePerDrop => Columns * HiddenRows;

    public int RoundsToWinMatch { get; init; } = 2;

    /// <summary>In versus, the speed level rises by one every this many seconds of a round.</summary>
    public double VersusSecondsPerSpeedLevel { get; init; } = 25.0;
}
