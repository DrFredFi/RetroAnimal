using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Tests.Input;

public class InputStateTests
{
    [Fact]
    public void EmptySet_ContainsNothing()
    {
        foreach (var action in Enum.GetValues<GameAction>())
        {
            Assert.False(ActionSet.Empty.Contains(action));
        }
    }

    [Fact]
    public void With_AddsOnlyTheGivenAction()
    {
        var set = ActionSet.Empty.With(GameAction.HardDrop);

        Assert.True(set.Contains(GameAction.HardDrop));
        Assert.False(set.Contains(GameAction.MoveLeft));
    }

    [Fact]
    public void Union_CombinesBothSets()
    {
        var set = ActionSet.Empty.With(GameAction.MoveLeft)
            .Union(ActionSet.Empty.With(GameAction.Pause));

        Assert.True(set.Contains(GameAction.MoveLeft));
        Assert.True(set.Contains(GameAction.Pause));
    }

    [Fact]
    public void AllActions_FitInTheBitMask()
    {
        Assert.True(Enum.GetValues<GameAction>().Length <= 64);
    }

    [Fact]
    public void InputState_ExposesEdgesAndLevels()
    {
        var state = new InputState(
            Held: ActionSet.Empty.With(GameAction.SoftDrop),
            Pressed: ActionSet.Empty.With(GameAction.RotateClockwise),
            Released: ActionSet.Empty.With(GameAction.MoveRight));

        Assert.True(state.IsHeld(GameAction.SoftDrop));
        Assert.True(state.WasPressed(GameAction.RotateClockwise));
        Assert.True(state.WasReleased(GameAction.MoveRight));
        Assert.False(state.WasPressed(GameAction.SoftDrop));
    }
}
