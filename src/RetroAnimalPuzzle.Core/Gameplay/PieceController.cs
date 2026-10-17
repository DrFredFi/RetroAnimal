using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>What the player did with the falling piece during one tick.</summary>
/// <param name="SoftDropHeldSeconds">How long Down has been held continuously (drives progressive acceleration).</param>
public readonly record struct PieceInputResult(bool Moved, bool Rotated, bool HardDrop, bool SoftDropping, double SoftDropHeldSeconds);

/// <summary>
/// Translates input into movement of the falling piece: single steps on press, timed auto-repeat while a
/// direction is held (initial delay, then a steady interval), rotation with wall kicks, and a soft drop that
/// speeds up the longer Down is held. Hard drop is honoured only when <see cref="GameRules.HardDropEnabled"/>.
/// </summary>
public sealed class PieceController
{
    private readonly GameRules _rules;
    private double _leftRepeat;
    private double _rightRepeat;
    private double _softDropHeld;

    public PieceController(GameRules rules)
    {
        _rules = rules;
    }

    public void Reset()
    {
        _leftRepeat = 0;
        _rightRepeat = 0;
        _softDropHeld = 0;
    }

    /// <summary>Restarts the soft-drop acceleration (a new piece starts slow even if Down is still held).</summary>
    public void ResetSoftDrop() => _softDropHeld = 0;

    public PieceInputResult Apply(Grid grid, ref Piece piece, double deltaSeconds, in InputState input)
    {
        bool moved = false;
        bool rotated = false;

        if (input.WasPressed(GameAction.RotateClockwise) && CollisionSystem.TryRotate(grid, piece, clockwise: true, out var cw))
        {
            piece = cw;
            rotated = true;
        }

        if (input.WasPressed(GameAction.RotateCounterClockwise) && CollisionSystem.TryRotate(grid, piece, clockwise: false, out var ccw))
        {
            piece = ccw;
            rotated = true;
        }

        moved |= Horizontal(grid, ref piece, deltaSeconds, input, GameAction.MoveLeft, -1, ref _leftRepeat);
        moved |= Horizontal(grid, ref piece, deltaSeconds, input, GameAction.MoveRight, 1, ref _rightRepeat);

        bool hardDrop = _rules.HardDropEnabled && input.WasPressed(GameAction.HardDrop);
        bool softDrop = !hardDrop && input.IsHeld(GameAction.SoftDrop);
        _softDropHeld = softDrop ? _softDropHeld + deltaSeconds : 0;
        return new PieceInputResult(moved, rotated, hardDrop, softDrop, _softDropHeld);
    }

    private bool Horizontal(Grid grid, ref Piece piece, double deltaSeconds, in InputState input,
        GameAction action, int dx, ref double repeatTimer)
    {
        int steps = 0;
        if (input.WasPressed(action))
        {
            steps = 1;
            repeatTimer = _rules.AutoRepeatDelay;
        }
        else if (input.IsHeld(action))
        {
            repeatTimer -= deltaSeconds;
            while (repeatTimer <= 0)
            {
                steps++;
                repeatTimer += _rules.AutoRepeatInterval;
            }
        }
        else
        {
            repeatTimer = 0;
        }

        bool moved = false;
        for (int i = 0; i < steps && CollisionSystem.CanMove(grid, piece, dx, 0); i++)
        {
            piece = piece.MovedBy(dx, 0);
            moved = true;
        }

        return moved;
    }
}
