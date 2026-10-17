namespace RetroAnimalPuzzle.Input;

/// <summary>Logical actions the game reacts to, independent of the physical keys bound to them.</summary>
public enum GameAction
{
    MoveLeft,
    MoveRight,
    SoftDrop,
    HardDrop,
    RotateClockwise,
    RotateCounterClockwise,
    Pause,
    Confirm,
    Back,
    Restart,
    Help,
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    ToggleDebugOverlay,
}
