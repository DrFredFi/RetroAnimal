namespace RetroAnimalPuzzle.Input;

/// <summary>
/// Inputs of an Xbox-style controller. Stick directions and triggers count as buttons once pushed far enough,
/// so they can be bound like any other button.
/// </summary>
public enum GamepadButton
{
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    Start,
    Back,
    LeftStickPress,
    RightStickPress,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
}

/// <summary>One raw controller reading in XInput layout (button mask, triggers 0..255, sticks -32768..32767, +Y up).</summary>
public readonly record struct GamepadReading(
    ushort Buttons, byte LeftTrigger, byte RightTrigger, short LeftX, short LeftY, short RightX, short RightY);

/// <summary>Allocation-free set of <see cref="GamepadButton"/> values backed by a bit mask.</summary>
public readonly record struct GamepadButtons(uint Bits)
{
    /// <summary>How far a stick or trigger must travel to count as pressed, and to count as released again.</summary>
    public const float PressThreshold = 0.5f, ReleaseThreshold = 0.35f;

    private static readonly (ushort Mask, GamepadButton Button)[] DigitalButtons =
    [
        (0x0001, GamepadButton.DPadUp),
        (0x0002, GamepadButton.DPadDown),
        (0x0004, GamepadButton.DPadLeft),
        (0x0008, GamepadButton.DPadRight),
        (0x0010, GamepadButton.Start),
        (0x0020, GamepadButton.Back),
        (0x0040, GamepadButton.LeftStickPress),
        (0x0080, GamepadButton.RightStickPress),
        (0x0100, GamepadButton.LeftShoulder),
        (0x0200, GamepadButton.RightShoulder),
        (0x1000, GamepadButton.A),
        (0x2000, GamepadButton.B),
        (0x4000, GamepadButton.X),
        (0x8000, GamepadButton.Y),
    ];

    public static GamepadButtons Empty => default;

    public bool Contains(GamepadButton button) => (Bits & Mask(button)) != 0;

    public GamepadButtons With(GamepadButton button) => new(Bits | Mask(button));

    public bool IsEmpty => Bits == 0;

    /// <summary>Buttons in this set that are not in <paramref name="other"/> (e.g. now minus before = just pressed).</summary>
    public GamepadButtons Except(GamepadButtons other) => new(Bits & ~other.Bits);

    /// <summary>The lowest-numbered button in the set, if any.</summary>
    public GamepadButton? First() => Bits == 0 ? null : (GamepadButton)System.Numerics.BitOperations.TrailingZeroCount(Bits);

    /// <summary>
    /// Converts a raw reading to buttons. Analog inputs use hysteresis against <paramref name="previous"/>, so a
    /// stick resting near the threshold does not flicker between pressed and released.
    /// </summary>
    public static GamepadButtons FromReading(in GamepadReading reading, GamepadButtons previous)
    {
        var set = Empty;
        foreach (var (mask, button) in DigitalButtons)
        {
            if ((reading.Buttons & mask) != 0)
            {
                set = set.With(button);
            }
        }

        set = Analog(set, previous, GamepadButton.LeftTrigger, reading.LeftTrigger / 255f);
        set = Analog(set, previous, GamepadButton.RightTrigger, reading.RightTrigger / 255f);
        set = Stick(set, previous, reading.LeftX, reading.LeftY,
            GamepadButton.LeftStickLeft, GamepadButton.LeftStickRight, GamepadButton.LeftStickUp, GamepadButton.LeftStickDown);
        set = Stick(set, previous, reading.RightX, reading.RightY,
            GamepadButton.RightStickLeft, GamepadButton.RightStickRight, GamepadButton.RightStickUp, GamepadButton.RightStickDown);
        return set;
    }

    private static GamepadButtons Stick(GamepadButtons set, GamepadButtons previous, short x, short y,
        GamepadButton left, GamepadButton right, GamepadButton up, GamepadButton down)
    {
        float fx = Math.Max(-1f, x / 32767f);
        float fy = Math.Max(-1f, y / 32767f);
        set = Analog(set, previous, left, -fx);
        set = Analog(set, previous, right, fx);
        set = Analog(set, previous, up, fy);
        return Analog(set, previous, down, -fy);
    }

    private static GamepadButtons Analog(GamepadButtons set, GamepadButtons previous, GamepadButton button, float amount)
    {
        float threshold = previous.Contains(button) ? ReleaseThreshold : PressThreshold;
        return amount >= threshold ? set.With(button) : set;
    }

    private static uint Mask(GamepadButton button) => 1u << (int)button;
}
