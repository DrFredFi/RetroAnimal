using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>Pure collision queries for falling pieces. Nothing here mutates the grid.</summary>
public static class CollisionSystem
{
    public static bool Fits(Grid grid, Piece piece) =>
        grid.IsFree(piece.Pivot) && grid.IsFree(piece.Satellite);

    public static bool CanMove(Grid grid, Piece piece, int dx, int dy) => Fits(grid, piece.MovedBy(dx, dy));

    public static bool IsGrounded(Grid grid, Piece piece) => !CanMove(grid, piece, 0, 1);

    /// <summary>The piece moved straight down as far as it legally goes.</summary>
    public static Piece DropPosition(Grid grid, Piece piece)
    {
        while (CanMove(grid, piece, 0, 1))
        {
            piece = piece.MovedBy(0, 1);
        }

        return piece;
    }

    /// <summary>
    /// Tries to rotate the pair. When the target spot is blocked, tries limited wall kicks:
    /// shifting the pivot away from the obstruction by one cell, or up by one when rotating into the floor.
    /// As a last resort in a one-column-wide gap, the pair flips 180° (swapping the blocks vertically).
    /// </summary>
    public static bool TryRotate(Grid grid, Piece piece, bool clockwise, out Piece rotated)
    {
        var target = clockwise ? piece.Orientation.Clockwise() : piece.Orientation.CounterClockwise();
        var candidate = piece.WithOrientation(target);
        if (Fits(grid, candidate))
        {
            rotated = candidate;
            return true;
        }

        var (dx, dy) = target.Offset();
        var kicked = candidate.MovedBy(-dx, -dy);
        if (Fits(grid, kicked))
        {
            rotated = kicked;
            return true;
        }

        if (piece.Orientation is Orientation.Up or Orientation.Down && target is Orientation.Left or Orientation.Right)
        {
            var flipped = piece.WithOrientation(piece.Orientation.Clockwise().Clockwise());
            var flippedKick = flipped.MovedBy(0, piece.Orientation == Orientation.Up ? -1 : 1);
            if (Fits(grid, flipped))
            {
                rotated = flipped;
                return true;
            }

            if (Fits(grid, flippedKick))
            {
                rotated = flippedKick;
                return true;
            }
        }

        rotated = piece;
        return false;
    }
}
