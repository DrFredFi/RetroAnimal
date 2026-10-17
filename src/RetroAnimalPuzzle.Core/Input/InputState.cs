namespace RetroAnimalPuzzle.Input;

/// <summary>Allocation-free set of <see cref="GameAction"/> values backed by a bit mask.</summary>
public readonly record struct ActionSet(ulong Bits)
{
    public static ActionSet Empty => default;

    public bool Contains(GameAction action) => (Bits & Mask(action)) != 0;

    public ActionSet With(GameAction action) => new(Bits | Mask(action));

    public ActionSet Union(ActionSet other) => new(Bits | other.Bits);

    public bool IsEmpty => Bits == 0;

    private static ulong Mask(GameAction action) => 1UL << (int)action;
}

/// <summary>
/// Snapshot of the input for one simulation tick. <see cref="Pressed"/> and <see cref="Released"/>
/// contain edges that happened since the previous tick; <see cref="Held"/> is the current level.
/// </summary>
public readonly record struct InputState(ActionSet Held, ActionSet Pressed, ActionSet Released)
{
    public static InputState None => default;

    public bool IsHeld(GameAction action) => Held.Contains(action);

    public bool WasPressed(GameAction action) => Pressed.Contains(action);

    public bool WasReleased(GameAction action) => Released.Contains(action);
}
