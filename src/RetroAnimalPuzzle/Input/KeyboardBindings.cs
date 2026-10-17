using RetroAnimalPuzzle.Persistence;

namespace RetroAnimalPuzzle.Input;

/// <summary>
/// Maps physical keys to logical actions. One key may trigger several actions (e.g. Up rotates in game and
/// navigates menus). Menu keys (arrows, Enter, Escape) are fixed; gameplay actions listed in
/// <see cref="Rebindable"/> can be reassigned and are persisted in <see cref="SettingsData.KeyBindings"/>.
/// </summary>
internal sealed class KeyboardBindings
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

    private static readonly Dictionary<GameAction, Keys> DefaultKeys = new()
    {
        [GameAction.MoveLeft] = Keys.Left,
        [GameAction.MoveRight] = Keys.Right,
        [GameAction.SoftDrop] = Keys.Down,
        [GameAction.RotateClockwise] = Keys.Up,
        [GameAction.RotateCounterClockwise] = Keys.Z,
        [GameAction.Pause] = Keys.P,
    };

    private readonly Dictionary<Keys, List<GameAction>> _map = [];
    private readonly Dictionary<GameAction, Keys> _primary = [];

    public static KeyboardBindings CreateDefault() => FromSettings(new Dictionary<string, string>());

    /// <summary>Builds bindings from saved settings; unknown or missing entries fall back to defaults.</summary>
    public static KeyboardBindings FromSettings(IReadOnlyDictionary<string, string> saved)
    {
        var bindings = new KeyboardBindings();

        // Fixed menu / system keys.
        bindings.Add(Keys.Left, GameAction.MenuLeft);
        bindings.Add(Keys.Right, GameAction.MenuRight);
        bindings.Add(Keys.Up, GameAction.MenuUp);
        bindings.Add(Keys.Down, GameAction.MenuDown);
        bindings.Add(Keys.Enter, GameAction.Confirm);
        bindings.Add(Keys.Escape, GameAction.Back);
        bindings.Add(Keys.Escape, GameAction.Pause);
        bindings.Add(Keys.R, GameAction.Restart);
        bindings.Add(Keys.F1, GameAction.Help);
        bindings.Add(Keys.F3, GameAction.ToggleDebugOverlay);

        bool customised = false;
        foreach (var action in Rebindable)
        {
            var key = DefaultKeys[action];
            if (saved.TryGetValue(action.ToString(), out var name) && Enum.TryParse<Keys>(name, out var parsed) && IsBindable(parsed))
            {
                key = parsed;
                customised = true;
            }

            bindings._primary[action] = key;
            bindings.Add(key, action);
        }

        if (!customised)
        {
            bindings.Add(Keys.X, GameAction.RotateClockwise);
        }

        return bindings;
    }

    /// <summary>Keys that may be assigned to gameplay actions (not system/menu-critical keys).</summary>
    public static bool IsBindable(Keys key) =>
        key is not (Keys.None or Keys.Escape or Keys.Enter or Keys.F1 or Keys.F3 or Keys.F11 or Keys.Menu or Keys.LWin or Keys.RWin);

    public Keys PrimaryKey(GameAction action) => _primary.TryGetValue(action, out var key) ? key : Keys.None;

    public static string KeyName(Keys key) => key switch
    {
        Keys.Space => "Space",
        Keys.Left => "Left",
        Keys.Right => "Right",
        Keys.Up => "Up",
        Keys.Down => "Down",
        Keys.ShiftKey => "Shift",
        Keys.ControlKey => "Ctrl",
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        _ => key.ToString(),
    };

    public ActionSet ActionsFor(Keys key)
    {
        var set = ActionSet.Empty;
        if (_map.TryGetValue(key, out var actions))
        {
            foreach (var action in actions)
            {
                set = set.With(action);
            }
        }

        return set;
    }

    private void Add(Keys key, GameAction action)
    {
        if (!_map.TryGetValue(key, out var list))
        {
            list = [];
            _map[key] = list;
        }

        if (!list.Contains(action))
        {
            list.Add(action);
        }
    }
}
