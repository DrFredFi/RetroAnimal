using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>
/// Single-player progression: every <see cref="GameRules.FoodPerLevel"/> food eaten raises the level,
/// and each level falls a little faster (see <see cref="GameRules.FallInterval"/>).
/// </summary>
public sealed class LevelSystem
{
    private readonly GameRules _rules;

    public LevelSystem(GameRules rules, int startLevel = 1)
    {
        _rules = rules;
        StartLevel = Math.Clamp(startLevel, 1, rules.MaxLevel);
        Level = StartLevel;
    }

    public int StartLevel { get; }

    public int Level { get; private set; }

    /// <summary>Food needed (total since the game started) to reach the next level.</summary>
    public int NextLevelTarget => (Level - StartLevel + 1) * _rules.FoodPerLevel;

    /// <summary>Updates the level from the total food eaten. Returns how many levels were gained.</summary>
    public int Update(int totalFoodCleared)
    {
        int gained = 0;
        while (Level < _rules.MaxLevel && totalFoodCleared >= NextLevelTarget)
        {
            Level++;
            gained++;
        }

        return gained;
    }

    public void Reset() => Level = StartLevel;
}
