namespace RetroAnimalPuzzle.Input;

/// <summary>
/// Collects WinForms keyboard events between simulation ticks, polls the controllers, and turns both into
/// <see cref="InputState"/> snapshots. Keyboard edges are buffered so a tap shorter than one tick is never lost;
/// OS auto-repeat is ignored (gameplay implements its own repeat timing). Controllers are polled once per tick.
/// </summary>
internal sealed class InputManager
{
    private readonly HashSet<Keys> _heldKeys = [];
    private readonly XInputGamepads? _pads;
    private readonly GamepadButtons[] _padButtons = new GamepadButtons[XInputGamepads.MaxPads];
    private ActionSet _pendingPressed;
    private ActionSet _pendingReleased;
    private ActionSet _padHeld;
    private Keys _lastPressed;

    /// <param name="pads">The controller reader, or <c>null</c> for keyboard only (off-screen tools).</param>
    public InputManager(KeyboardBindings bindings, XInputGamepads? pads = null)
    {
        Bindings = bindings;
        _pads = pads;
    }

    /// <summary>Active bindings; replaced when the player changes controls.</summary>
    public KeyboardBindings Bindings { get; set; }

    /// <summary>Controller mapping, shared by every controller.</summary>
    public GamepadBindings PadBindings { get; set; } = GamepadBindings.CreateDefault();

    public bool GamepadsEnabled { get; set; } = true;

    /// <summary>In a two-player match, whether the first connected controller plays the left field.</summary>
    public bool FirstPadPlaysLeft { get; set; } = true;

    /// <summary>
    /// Set by the two-player screen: the first two connected controllers then drive one field each. Otherwise
    /// every controller drives the ordinary (single-player) actions.
    /// </summary>
    public bool TwoPlayerPads { get; set; }

    /// <summary>Controllers are ignored while the window is in the background.</summary>
    public bool WindowActive { get; set; } = true;

    /// <summary>Controllers that answered at the last poll.</summary>
    public int ConnectedPads { get; private set; }

    /// <summary>The most recent physical key pressed during the last snapshot window (for key rebinding).</summary>
    public Keys LastPressedKey { get; private set; }

    /// <summary>A controller button first pressed at the last snapshot, on any controller (for button rebinding).</summary>
    public GamepadButton? LastPressedButton { get; private set; }

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
        var keyHeld = ActionSet.Empty;
        foreach (var key in _heldKeys)
        {
            keyHeld = keyHeld.Union(Bindings.ActionsFor(key));
        }

        // Controller edges come from comparing with the previous poll; a release only counts if no key still holds it.
        var padHeld = PollPads();
        var pressed = _pendingPressed.Union(padHeld.Except(_padHeld));
        var released = _pendingReleased.Union(_padHeld.Except(padHeld).Except(keyHeld));
        _padHeld = padHeld;

        var state = new InputState(keyHeld.Union(padHeld), pressed, released);
        _pendingPressed = ActionSet.Empty;
        _pendingReleased = ActionSet.Empty;
        LastPressedKey = _lastPressed;
        _lastPressed = Keys.None;
        return state;
    }

    private ActionSet PollPads()
    {
        LastPressedButton = null;
        if (_pads is null || !GamepadsEnabled || !WindowActive)
        {
            Array.Clear(_padButtons);
            if (_pads is null || !GamepadsEnabled) ConnectedPads = 0;
            return ActionSet.Empty;
        }

        var held = ActionSet.Empty;
        int connected = 0;
        for (int slot = 0; slot < XInputGamepads.MaxPads; slot++)
        {
            if (!_pads.TryRead(slot, out var reading))
            {
                _padButtons[slot] = GamepadButtons.Empty;
                continue;
            }

            var buttons = GamepadButtons.FromReading(reading, _padButtons[slot]);
            LastPressedButton ??= buttons.Except(_padButtons[slot]).First();
            _padButtons[slot] = buttons;

            var role = TwoPlayerPads && connected < 2 && (connected == 0) == FirstPadPlaysLeft ? PadRole.LeftPlayer : PadRole.Main;
            held = held.Union(PadBindings.ActionsFor(buttons, role));
            connected++;
        }

        ConnectedPads = connected;
        return held;
    }
}
