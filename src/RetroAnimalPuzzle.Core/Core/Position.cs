namespace RetroAnimalPuzzle.Core;

/// <summary>Grid coordinate. X grows to the right, Y grows downwards; row 0 is the top hidden row.</summary>
public readonly record struct Position(int X, int Y)
{
    public Position Offset(int dx, int dy) => new(X + dx, Y + dy);

    public Position Up => new(X, Y - 1);

    public Position Down => new(X, Y + 1);

    public Position Left => new(X - 1, Y);

    public Position Right => new(X + 1, Y);

    public override string ToString() => $"({X},{Y})";
}
