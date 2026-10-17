using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>Endless single-player game: one board, levels raise the falling speed, ends on top-out.</summary>
public sealed class SinglePlayerSession
{
    private readonly List<GameEvent> _events = [];

    public SinglePlayerSession(GameRules rules, int seed, int startLevel = 1)
    {
        Rules = rules;
        Board = new PlayerBoard(rules, seed);
        Levels = new LevelSystem(rules, startLevel);
        Board.SpeedLevel = Levels.Level;
    }

    public GameRules Rules { get; }

    public PlayerBoard Board { get; }

    public LevelSystem Levels { get; }

    public TimeSpan Elapsed { get; private set; }

    public bool IsOver => Board.IsToppedOut;

    /// <summary>Board events plus session events (level-ups) from the last update.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    public void Update(double deltaSeconds, in InputState input)
    {
        _events.Clear();
        if (IsOver)
        {
            return;
        }

        Elapsed += TimeSpan.FromSeconds(deltaSeconds);
        Board.SpeedLevel = Levels.Level;
        Board.Update(deltaSeconds, input);
        _events.AddRange(Board.Events);

        foreach (var e in Board.Events)
        {
            if (e.Type != GameEventType.Eat)
            {
                continue;
            }

            int before = Levels.Level;
            int gained = Levels.Update(Board.Score.FoodCleared);
            for (int i = 0; i < gained; i++)
            {
                Board.Score.AddLevelBonus(before + i);
                _events.Add(new GameEvent(GameEventType.LevelUp, before + i + 1));
            }
        }
    }
}
