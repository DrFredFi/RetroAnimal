namespace RetroAnimalPuzzle.Core;

/// <summary>Where the satellite block sits relative to the pivot.</summary>
public enum Orientation : byte
{
    Up,
    Right,
    Down,
    Left,
}

public static class OrientationExtensions
{
    public static Orientation Clockwise(this Orientation o) => (Orientation)(((int)o + 1) % 4);

    public static Orientation CounterClockwise(this Orientation o) => (Orientation)(((int)o + 3) % 4);

    public static (int Dx, int Dy) Offset(this Orientation o) => o switch
    {
        Orientation.Up => (0, -1),
        Orientation.Right => (1, 0),
        Orientation.Down => (0, 1),
        _ => (-1, 0),
    };
}

/// <summary>The two blocks of a falling pair, before they are positioned on a board.</summary>
public readonly record struct PiecePair(Block Pivot, Block Satellite);

/// <summary>A positioned falling pair. Immutable: movement returns a new value.</summary>
public readonly record struct Piece(PiecePair Blocks, Position Pivot, Orientation Orientation)
{
    public Position Satellite
    {
        get
        {
            var (dx, dy) = Orientation.Offset();
            return Pivot.Offset(dx, dy);
        }
    }

    public Piece MovedBy(int dx, int dy) => this with { Pivot = Pivot.Offset(dx, dy) };

    public Piece WithOrientation(Orientation orientation) => this with { Orientation = orientation };

    /// <summary>A freshly spawned pair lies horizontally with the pivot on the left.</summary>
    public static Piece Spawn(PiecePair blocks, GameRules rules) =>
        new(blocks, new Position(rules.SpawnColumn, rules.HiddenRows), Orientation.Right);
}
