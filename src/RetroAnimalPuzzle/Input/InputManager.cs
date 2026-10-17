namespace RetroAnimalPuzzle.Input;

/// <summary>
/// Collects WinForms keyboard events between simulation ticks and turns them into <see cref="InputState"/> snapshots.
/// Edges are buffered so a tap shorter than one tick is never lost; OS auto-repeat is ignored
/// (gameplay implements its own repeat timing).
/// </summary>
internal sealed class InputManager
{
    private readonly HashSet<Keys> _heldKeys = [];
    private ActionSet _pendingPressed;
    private ActionSet _pendingReleased;
    private Keys _lastPressed;

    public InputManager(KeyboardBindings bindings)
    {
        Bindings = bindings;
    }

    /// <summary>Active bindings; replaced when the player changes controls.</summary>
    public KeyboardBindings Bindings { get; set; }

    /// <summary>The most recent physical key pressed during the last snapshot window (for key rebinding).</summary>
    public Keys LastPressedKey { get; private set; }

    public void OnKeyDown(Keys key)
    {
        if (_heldKeys.Add(key))
        {
            _pendingPressed = _pendingPressed.Union(Bindings.ActionsFor(key));
            _lastPressed = key;
        }
    }

    public void OnKeyUp(Keys key)
    {
        if (_heldKeys.Remove(key))
        {
            _pendingReleased = _pendingReleased.Union(Bindings.ActionsFor(key));
        }
    }

    /// <summary>Releases every key, e.g. when the window loses focus and key-up events would be missed.</summary>
    public void ReleaseAll()
    {
        foreach (var key in _heldKeys)
        {
            _pendingReleased = _pendingReleased.Union(Bindings.ActionsFor(key));
        }

        _heldKeys.Clear();
    }

    /// <summary>Returns the state for the next tick and clears the buffered edges.</summary>
    public InputState Snapshot()
    {
        var held = ActionSet.Empty;
        foreach (var key in _heldKeys)
        {
            held = held.Union(Bindings.ActionsFor(key));
        }

        var state = new InputState(held, _pendingPressed, _pendingReleased);
        _pendingPressed = ActionSet.Empty;
        _pendingReleased = ActionSet.Empty;
        LastPressedKey = _lastPressed;
        _lastPressed = Keys.None;
        return state;
    }
}
