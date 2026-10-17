namespace RetroAnimalPuzzle.Core;

/// <summary>
/// Authoritative store of locked blocks. Rows <c>0 .. HiddenRows-1</c> sit above the visible playfield.
/// Out-of-range reads return <see cref="Block.Empty"/> for queries and count as blocked for collisions.
/// </summary>
public sealed class Grid
{
    private readonly Block[] _cells;

    public Grid(int columns, int rows, int hiddenRows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(hiddenRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(hiddenRows, rows);

        Columns = columns;
        Rows = rows;
        HiddenRows = hiddenRows;
        _cells = new Block[columns * rows];
    }

    public static Grid For(GameRules rules) => new(rules.Columns, rules.TotalRows, rules.HiddenRows);

    public int Columns { get; }

    public int Rows { get; }

    public int HiddenRows { get; }

    public Block this[int x, int y] => InBounds(x, y) ? _cells[y * Columns + x] : Block.Empty;

    public Block this[Position p] => this[p.X, p.Y];

    public bool InBounds(int x, int y) => x >= 0 && x < Columns && y >= 0 && y < Rows;

    public bool InBounds(Position p) => InBounds(p.X, p.Y);

    /// <summary>True when the cell exists and holds no block. Use this for every collision test.</summary>
    public bool IsFree(int x, int y) => InBounds(x, y) && _cells[y * Columns + x].IsEmpty;

    public bool IsFree(Position p) => IsFree(p.X, p.Y);

    public bool IsOccupied(int x, int y) => InBounds(x, y) && !_cells[y * Columns + x].IsEmpty;

    /// <summary>Places a block into an empty, in-range cell.</summary>
    public void Place(Position p, Block block)
    {
        if (!InBounds(p))
        {
            throw new ArgumentOutOfRangeException(nameof(p), p, "Position is outside the grid.");
        }

        if (block.IsEmpty)
        {
            throw new ArgumentException("Use Clear to empty a cell.", nameof(block));
        }

        ref Block cell = ref _cells[p.Y * Columns + p.X];
        if (!cell.IsEmpty)
        {
            throw new InvalidOperationException($"Cell {p} is already occupied by {cell}.");
        }

        cell = block;
    }

    public void Clear(Position p)
    {
        if (InBounds(p))
        {
            _cells[p.Y * Columns + p.X] = Block.Empty;
        }
    }

    public void ClearAll() => Array.Clear(_cells);

    /// <summary>Moves a block without validation of what lies between; used by gravity.</summary>
    internal void Move(Position from, Position to)
    {
        var block = this[from];
        _cells[from.Y * Columns + from.X] = Block.Empty;
        _cells[to.Y * Columns + to.X] = block;
    }

    /// <summary>True when any locked block sits in the hidden rows above the visible field.</summary>
    public bool HasBlocksInHiddenRows()
    {
        for (int i = 0; i < HiddenRows * Columns; i++)
        {
            if (!_cells[i].IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lowest free row the column can be filled to (-1 when the column is full).</summary>
    public int LowestFreeRow(int x)
    {
        for (int y = Rows - 1; y >= 0; y--)
        {
            if (IsFree(x, y))
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>Number of occupied cells counted from the floor up to the highest block in the column.</summary>
    public int ColumnHeight(int x)
    {
        for (int y = 0; y < Rows; y++)
        {
            if (IsOccupied(x, y))
            {
                return Rows - y;
            }
        }

        return 0;
    }

    public int CountBlocks()
    {
        int count = 0;
        foreach (var cell in _cells)
        {
            if (!cell.IsEmpty)
            {
                count++;
            }
        }

        return count;
    }

    public Grid Clone()
    {
        var copy = new Grid(Columns, Rows, HiddenRows);
        Array.Copy(_cells, copy._cells, _cells.Length);
        return copy;
    }

    public void CopyFrom(Grid other)
    {
        if (other.Columns != Columns || other.Rows != Rows)
        {
            throw new ArgumentException("Grid sizes differ.", nameof(other));
        }

        Array.Copy(other._cells, _cells, _cells.Length);
    }
}
