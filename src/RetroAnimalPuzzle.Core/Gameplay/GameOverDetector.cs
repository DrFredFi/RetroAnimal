using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>A board is lost when blocks remain above the visible field or a new pair has no room to appear.</summary>
public static class GameOverDetector
{
    public static bool CanSpawn(Grid grid, GameRules rules, PiecePair next) =>
        CollisionSystem.Fits(grid, Piece.Spawn(next, rules));

    public static bool IsToppedOut(Grid grid, GameRules rules, PiecePair next) =>
        grid.HasBlocksInHiddenRows() || !CanSpawn(grid, rules, next);
}
