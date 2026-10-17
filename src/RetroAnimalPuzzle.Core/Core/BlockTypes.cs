namespace RetroAnimalPuzzle.Core;

public enum AnimalType : byte
{
    Monkey,
    Rabbit,
    Panda,
    Dog,
    Cat,
}

public enum FoodType : byte
{
    Banana,
    Carrot,
    Bamboo,
    Bone,
    Fish,
}

public enum BlockKind : byte
{
    Empty,
    Animal,
    Food,
    Coin,
}

/// <summary>
/// Immutable value describing what occupies a grid cell. <c>default(Block)</c> is the empty cell,
/// so the grid never allocates objects for empty space.
/// </summary>
public readonly record struct Block(BlockKind Kind, byte Code)
{
    public static Block Empty => default;

    public static Block Coin => new(BlockKind.Coin, 0);

    public static Block Of(AnimalType animal) => new(BlockKind.Animal, (byte)animal);

    public static Block Of(FoodType food) => new(BlockKind.Food, (byte)food);

    public bool IsEmpty => Kind == BlockKind.Empty;

    public bool IsAnimal => Kind == BlockKind.Animal;

    public bool IsFood => Kind == BlockKind.Food;

    public bool IsCoin => Kind == BlockKind.Coin;

    public AnimalType Animal => IsAnimal ? (AnimalType)Code : throw new InvalidOperationException($"{this} is not an animal.");

    public FoodType Food => IsFood ? (FoodType)Code : throw new InvalidOperationException($"{this} is not food.");

    public override string ToString() => Kind switch
    {
        BlockKind.Empty => "Empty",
        BlockKind.Animal => ((AnimalType)Code).ToString(),
        BlockKind.Food => ((FoodType)Code).ToString(),
        _ => "Coin",
    };
}
