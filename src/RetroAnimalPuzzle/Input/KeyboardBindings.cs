using RetroAnimalPuzzle.Persistence;

namespace RetroAnimalPuzzle.Input;

/// <summary>
/// Maps physical keys to logical actions. One key may trigger several actions (e.g. Up rotates in game and
/// navigates menus). Menu keys (arrows, Enter, Escape) are fixed; gameplay actions listed in
/// <see cref="Rebindable"/> can be reassigned and are persisted in <see cref="SettingsData.KeyBindings"/>.
/// There are two gameplay key sets: the main one (arrows), used alone and by the right player in a two-player
/// match, and the left player's (W A S D), used only in a two-player match.
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
        GameAction.LeftPlayerMoveLeft,
        GameAction.LeftPlayerMoveRight,
        GameAction.LeftPlayerSoftDrop,
        GameAction.LeftPlayerRotateClockwise,
        GameAction.LeftPlayerRotateCounterClockwise,
    ];

    private static readonly Dictionary<GameAction, Keys> DefaultKeys = new()
    {
        [GameAction.MoveLeft] = Keys.Left,
        [GameAction.MoveRight] = Keys.Right,
        [GameAction.SoftDrop] = Keys.Down,
        [GameAction.RotateClockwise] = Keys.Up,
        [GameAction.RotateCounterClockwise] = Keys.Z,
        [GameAction.Pause] = Keys.P,
        [GameAction.LeftPlayerMoveLeft] = Keys.A,
        [GameAction.LeftPlayerMoveRight] = Keys.D,
        [GameAction.LeftPlayerSoftDrop] = Keys.S,
        [GameAction.LeftPlayerRotateClockwise] = Keys.W,
        [GameAction.LeftPlayerRotateCounterClockwise] = Keys.Q,
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

        // Saved keys first, then defaults for the rest. A default already taken by a saved key stays unbound
        // (e.g. settings from before the left player's keys existed, where A might already move left).
        bool customised = false;
        var keys = new Dictionary<GameAction, Keys>();
        foreach (var action in Rebindable)
        {
            if (saved.TryGetValue(action.ToString(), out var name) && Enum.TryParse<Keys>(name, out var parsed) && IsBindable(parsed))
            {
                keys[action] = parsed;
                customised = true;
            }
        }

        foreach (var action in Rebindable)
        {
            if (!keys.ContainsKey(action))
            {
                keys[action] = keys.ContainsValue(DefaultKeys[action]) ? Keys.None : DefaultKeys[action];
            }
        }

        foreach (var action in Rebindable)
        {
            bindings._primary[action] = keys[action];
            if (keys[action] != Keys.None)
            {
                bindings.Add(keys[action], action);
            }
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
        Keys.None => "---",
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
