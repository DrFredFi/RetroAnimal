using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Tests.Input;

public class GamepadTests
{
    private static GamepadReading Reading(ushort buttons = 0, byte lt = 0, short lx = 0, short ly = 0) =>
        new(buttons, lt, 0, lx, ly, 0, 0);

    [Fact]
    public void FromReading_MapsDigitalButtons()
    {
        var buttons = GamepadButtons.FromReading(Reading(buttons: 0x0004 | 0x1000 | 0x0010), GamepadButtons.Empty);

        Assert.True(buttons.Contains(GamepadButton.DPadLeft));
        Assert.True(buttons.Contains(GamepadButton.A));
        Assert.True(buttons.Contains(GamepadButton.Start));
        Assert.False(buttons.Contains(GamepadButton.DPadRight));
    }

    [Fact]
    public void FromReading_StickUsesHysteresis()
    {
        short between = (short)(32767 * 0.42f);

        var fromRest = GamepadButtons.FromReading(Reading(lx: between), GamepadButtons.Empty);
        var whileHeld = GamepadButtons.FromReading(Reading(lx: between), GamepadButtons.Empty.With(GamepadButton.LeftStickRight));
        var pushed = GamepadButtons.FromReading(Reading(lx: -32768, ly: 32767), GamepadButtons.Empty);

        Assert.False(fromRest.Contains(GamepadButton.LeftStickRight));
        Assert.True(whileHeld.Contains(GamepadButton.LeftStickRight));
        Assert.True(pushed.Contains(GamepadButton.LeftStickLeft));
        Assert.True(pushed.Contains(GamepadButton.LeftStickUp));
    }

    [Fact]
    public void FromReading_TriggerCountsPastThreshold()
    {
        Assert.False(GamepadButtons.FromReading(Reading(lt: 60), GamepadButtons.Empty).Contains(GamepadButton.LeftTrigger));
        Assert.True(GamepadButtons.FromReading(Reading(lt: 200), GamepadButtons.Empty).Contains(GamepadButton.LeftTrigger));
    }

    [Fact]
    public void Defaults_UseTheDigitalControls_NotTheSticks()
    {
        var bindings = GamepadBindings.CreateDefault();

        Assert.Equal(GamepadButton.DPadLeft, bindings.PrimaryButton(GameAction.MoveLeft));
        Assert.Equal(GamepadButton.DPadRight, bindings.PrimaryButton(GameAction.MoveRight));
        Assert.Equal(GamepadButton.DPadDown, bindings.PrimaryButton(GameAction.SoftDrop));
        Assert.Equal(GamepadButton.A, bindings.PrimaryButton(GameAction.RotateClockwise));
        Assert.Equal(GamepadButton.B, bindings.PrimaryButton(GameAction.RotateCounterClockwise));

        var stick = bindings.ActionsFor(GamepadButtons.Empty.With(GamepadButton.LeftStickLeft), PadRole.Main);
        Assert.False(stick.Contains(GameAction.MoveLeft));
        Assert.True(stick.Contains(GameAction.MenuLeft));
    }

    [Fact]
    public void LeftPlayerRole_ProducesTheLeftPlayersActions_AndSharesPause()
    {
        var bindings = GamepadBindings.CreateDefault();
        var held = GamepadButtons.Empty.With(GamepadButton.DPadLeft).With(GamepadButton.A).With(GamepadButton.Start);

        var actions = bindings.ActionsFor(held, PadRole.LeftPlayer);

        Assert.True(actions.Contains(GameAction.LeftPlayerMoveLeft));
        Assert.True(actions.Contains(GameAction.LeftPlayerRotateClockwise));
        Assert.True(actions.Contains(GameAction.Pause));
        Assert.False(actions.Contains(GameAction.MoveLeft));
        Assert.False(actions.Contains(GameAction.RotateClockwise));
        Assert.True(actions.Contains(GameAction.MenuLeft));
    }

    [Fact]
    public void SavedBindings_OverrideDefaults_AndATakenDefaultStaysUnbound()
    {
        var saved = new Dictionary<string, string>
        {
            [nameof(GameAction.MoveLeft)] = nameof(GamepadButton.LeftStickLeft),
            [nameof(GameAction.SoftDrop)] = nameof(GamepadButton.A),
        };

        var bindings = GamepadBindings.FromSettings(saved);

        Assert.Equal(GamepadButton.LeftStickLeft, bindings.PrimaryButton(GameAction.MoveLeft));
        Assert.Equal(GamepadButton.A, bindings.PrimaryButton(GameAction.SoftDrop));
        Assert.Null(bindings.PrimaryButton(GameAction.RotateClockwise));
        Assert.True(bindings.ActionsFor(GamepadButtons.Empty.With(GamepadButton.LeftStickLeft), PadRole.Main)
            .Contains(GameAction.MoveLeft));
    }

    [Fact]
    public void ViewButton_IsReservedForBackAndPause()
    {
        var bindings = GamepadBindings.FromSettings(new Dictionary<string, string> { [nameof(GameAction.Pause)] = nameof(GamepadButton.Back) });

        Assert.False(GamepadBindings.IsBindable(GamepadButton.Back));
        Assert.Equal(GamepadButton.Start, bindings.PrimaryButton(GameAction.Pause));
        var view = bindings.ActionsFor(GamepadButtons.Empty.With(GamepadButton.Back), PadRole.Main);
        Assert.True(view.Contains(GameAction.Back));
        Assert.True(view.Contains(GameAction.Pause));
    }
}
