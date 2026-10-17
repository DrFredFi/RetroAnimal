namespace RetroAnimalPuzzle.Gameplay;

public enum GameEventType
{
    PieceSpawned,
    PieceMoved,
    PieceRotated,
    PieceLanded,
    HardDrop,
    Eat,
    ChainEnded,
    GarbageSent,
    GarbageDropped,
    LevelUp,
    ToppedOut,
}

/// <summary>
/// Something noteworthy that happened during a tick. Rendering and audio react to events;
/// they never inspect gameplay internals to decide what happened.
/// </summary>
/// <param name="Value">Event-specific number: points for <see cref="GameEventType.Eat"/>, rows for drops,
/// garbage count, new level...</param>
/// <param name="Chain">Chain depth for eat/chain events.</param>
/// <param name="Clear">What was cleared, for eat events.</param>
public readonly record struct GameEvent(GameEventType Type, int Value = 0, int Chain = 0, ClearResult? Clear = null);
