using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>
/// Drives a board like a player would: when a new pair appears it waits its reaction time, picks a target with
/// <see cref="CpuPlanner"/>, then presses rotate/move one action at a time and finally drops. It produces the same
/// <see cref="InputState"/> a keyboard would, so the board treats human and CPU identically.
/// </summary>
public sealed class CpuController
{
    private const int MaxActionsPerPiece = 16;

    private readonly CpuPlanner _planner;
    private readonly Random _random;
    private CpuSkill _skill;
    private int _plannedSerial = -1;
    private CpuTarget _target;
    private double _wait;
    private bool _softDrop;
    private int _actions;
    private int _lastX;

    public CpuController(GameRules rules, CpuSkill skill, int seed)
    {
        _planner = new CpuPlanner(rules);
        _skill = skill;
        _random = new Random(seed);
    }

    public CpuSkill Skill
    {
        get => _skill;
        set => _skill = value;
    }

    public InputState Think(PlayerBoard board, double deltaSeconds)
    {
        if (board.Phase != BoardPhase.Falling || board.Current is not { } piece)
        {
            return InputState.None;
        }

        if (board.PieceSerial != _plannedSerial)
        {
            _plannedSerial = board.PieceSerial;
            _target = _planner.Choose(board.Grid, piece, _skill, _random);
            _wait = _skill.ReactionDelay;
            _softDrop = _random.NextDouble() < _skill.SoftDropChance;
            _actions = 0;
            _lastX = int.MinValue;
        }

        _wait -= deltaSeconds;
        if (_wait > 0)
        {
            return InputState.None;
        }

        if (_actions < MaxActionsPerPiece)
        {
            if (piece.Orientation != _target.Orientation)
            {
                return Press(_target.Orientation == piece.Orientation.CounterClockwise()
                    ? GameAction.RotateCounterClockwise
                    : GameAction.RotateClockwise);
            }

            if (piece.Pivot.X != _target.PivotX)
            {
                if (piece.Pivot.X == _lastX)
                {
                    // Blocked on the way (e.g. the stack rose): settle for the current column.
                    _target = _target with { PivotX = piece.Pivot.X };
                }
                else
                {
                    _lastX = piece.Pivot.X;
                    return Press(piece.Pivot.X < _target.PivotX ? GameAction.MoveRight : GameAction.MoveLeft);
                }
            }
        }

        return _softDrop
            ? new InputState(ActionSet.Empty.With(GameAction.SoftDrop), ActionSet.Empty, ActionSet.Empty)
            : InputState.None;
    }

    private InputState Press(GameAction action)
    {
        _actions++;
        _wait = _skill.ActionInterval;
        return new InputState(ActionSet.Empty, ActionSet.Empty.With(action), ActionSet.Empty);
    }
}
