namespace RetroAnimalPuzzle.Input;

/// <summary>Which board a controller drives.</summary>
public enum PadRole
{
    /// <summary>The ordinary gameplay actions: one-player modes, or the right player in a two-player match.</summary>
    Main,

    /// <summary>The left player in a two-player match (the <c>LeftPlayer*</c> actions).</summary>
    LeftPlayer,
}

/// <summary>
/// Maps controller buttons to actions; every connected controller uses the same mapping. The menu buttons are
/// fixed (D-pad and left stick navigate, A confirms, B goes back, Start confirms, Back/View pauses and goes back);
/// the gameplay actions in <see cref="Rebindable"/> can be reassigned and default to the digital controls
/// (D-pad and face buttons), never the analog sticks.
/// </summary>
public sealed class GamepadBindings
{
    public static readonly GameAction[] Rebindable =
    [
        GameAction.MoveLeft,
        GameAction.MoveRight,
        GameAction.SoftDrop,
        GameAction.RotateClockwise,
        GameAction.RotateCounterClockwise,
        GameAction.Pause,
    ];

    private static readonly Dictionary<GameAction, GamepadButton> DefaultButtons = new()
    {
        [GameAction.MoveLeft] = GamepadButton.DPadLeft,
        [GameAction.MoveRight] = GamepadButton.DPadRight,
        [GameAction.SoftDrop] = GamepadButton.DPadDown,
        [GameAction.RotateClockwise] = GamepadButton.A,
        [GameAction.RotateCounterClockwise] = GamepadButton.B,
        [GameAction.Pause] = GamepadButton.Start,
    };

    private readonly Dictionary<GamepadButton, List<GameAction>> _fixed = [];
    private readonly Dictionary<GamepadButton, List<GameAction>> _gameplay = [];
    private readonly Dictionary<GameAction, GamepadButton> _primary = [];

    public static GamepadBindings CreateDefault() => FromSettings(new Dictionary<string, string>());

    /// <summary>Builds bindings from saved settings; unknown or missing entries fall back to defaults.</summary>
    public static GamepadBindings FromSettings(IReadOnlyDictionary<string, string> saved)
    {
        var bindings = new GamepadBindings();
        Add(bindings._fixed, GamepadButton.DPadUp, GameAction.MenuUp);
        Add(bindings._fixed, GamepadButton.DPadDown, GameAction.MenuDown);
        Add(bindings._fixed, GamepadButton.DPadLeft, GameAction.MenuLeft);
        Add(bindings._fixed, GamepadButton.DPadRight, GameAction.MenuRight);
        Add(bindings._fixed, GamepadButton.LeftStickUp, GameAction.MenuUp);
        Add(bindings._fixed, GamepadButton.LeftStickDown, GameAction.MenuDown);
        Add(bindings._fixed, GamepadButton.LeftStickLeft, GameAction.MenuLeft);
        Add(bindings._fixed, GamepadButton.LeftStickRight, GameAction.MenuRight);
        Add(bindings._fixed, GamepadButton.A, GameAction.Confirm);
        Add(bindings._fixed, GamepadButton.Start, GameAction.Confirm);
        Add(bindings._fixed, GamepadButton.B, GameAction.Back);
        Add(bindings._fixed, GamepadButton.Back, GameAction.Back);
        Add(bindings._fixed, GamepadButton.Back, GameAction.Pause);

        // Saved buttons first, then defaults for the rest; a default already taken by a saved button stays unbound.
        bool customised = false;
        var buttons = new Dictionary<GameAction, GamepadButton>();
        foreach (var action in Rebindable)
        {
            if (saved.TryGetValue(action.ToString(), out var name) && Enum.TryParse<GamepadButton>(name, out var parsed) &&
                IsBindable(parsed))
            {
                buttons[action] = parsed;
                customised = true;
            }
        }

        foreach (var action in Rebindable)
        {
            if (!buttons.ContainsKey(action) && !buttons.ContainsValue(DefaultButtons[action]))
            {
                buttons[action] = DefaultButtons[action];
            }
        }

        foreach (var (action, button) in buttons)
        {
            bindings._primary[action] = button;
            Add(bindings._gameplay, button, action);
        }

        if (!customised)
        {
            // Like the Up arrow on the keyboard.
            Add(bindings._gameplay, GamepadButton.DPadUp, GameAction.RotateClockwise);
        }

        return bindings;
    }

    /// <summary>Back/View is reserved for leaving menus and pausing, so a player can always get out.</summary>
    public static bool IsBindable(GamepadButton button) => button != GamepadButton.Back;

    public GamepadButton? PrimaryButton(GameAction action) => _primary.TryGetValue(action, out var b) ? b : null;

    /// <summary>The actions the held buttons stand for, for a controller playing the given role.</summary>
    public ActionSet ActionsFor(GamepadButtons held, PadRole role)
    {
        var set = ActionSet.Empty;
        for (uint bits = held.Bits; bits != 0; bits &= bits - 1)
        {
            var button = (GamepadButton)System.Numerics.BitOperations.TrailingZeroCount(bits);
            if (_fixed.TryGetValue(button, out var menu))
            {
                foreach (var action in menu)
                {
                    set = set.With(action);
                }
            }

            if (_gameplay.TryGetValue(button, out var gameplay))
            {
                foreach (var action in gameplay)
                {
                    set = set.With(role == PadRole.LeftPlayer ? ForLeftPlayer(action) : action);
                }
            }
        }

        return set;
    }

    public static string ButtonName(GamepadButton? button) => button switch
    {
        null => "---",
        GamepadButton.DPadUp => "D-Pad Up",
        GamepadButton.DPadDown => "D-Pad Down",
        GamepadButton.DPadLeft => "D-Pad Left",
        GamepadButton.DPadRight => "D-Pad Right",
        GamepadButton.LeftShoulder => "LB",
        GamepadButton.RightShoulder => "RB",
        GamepadButton.LeftTrigger => "LT",
        GamepadButton.RightTrigger => "RT",
        GamepadButton.Back => "View",
        GamepadButton.Start => "Menu",
        GamepadButton.LeftStickPress => "LS Click",
        GamepadButton.RightStickPress => "RS Click",
        GamepadButton.LeftStickUp => "LS Up",
        GamepadButton.LeftStickDown => "LS Down",
        GamepadButton.LeftStickLeft => "LS Left",
        GamepadButton.LeftStickRight => "LS Right",
        GamepadButton.RightStickUp => "RS Up",
        GamepadButton.RightStickDown => "RS Down",
        GamepadButton.RightStickLeft => "RS Left",
        GamepadButton.RightStickRight => "RS Right",
        _ => button.ToString()!,
    };

    /// <summary>The left player's version of a gameplay action; Pause is shared.</summary>
    private static GameAction ForLeftPlayer(GameAction action) => action switch
    {
        GameAction.MoveLeft => GameAction.LeftPlayerMoveLeft,
        GameAction.MoveRight => GameAction.LeftPlayerMoveRight,
        GameAction.SoftDrop => GameAction.LeftPlayerSoftDrop,
        GameAction.RotateClockwise => GameAction.LeftPlayerRotateClockwise,
        GameAction.RotateCounterClockwise => GameAction.LeftPlayerRotateCounterClockwise,
        _ => action,
    };

    private static void Add(Dictionary<GamepadButton, List<GameAction>> map, GamepadButton button, GameAction action)
    {
        if (!map.TryGetValue(button, out var list))
        {
            list = [];
            map[button] = list;
        }

        if (!list.Contains(action))
        {
            list.Add(action);
        }
    }
}
