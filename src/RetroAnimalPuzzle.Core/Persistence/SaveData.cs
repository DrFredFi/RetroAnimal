namespace RetroAnimalPuzzle.Persistence;

public enum GameMode
{
    Endless,
    Versus,
}

public sealed record HighScoreEntry(int Score, int Level, int FoodCleared, int BestChain, GameMode Mode, DateTimeOffset Date);

/// <summary>Versioned high-score file content.</summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;
    public const int MaxEntriesPerMode = 10;

    public int Version { get; set; } = CurrentVersion;

    public List<HighScoreEntry> HighScores { get; set; } = [];

    public IEnumerable<HighScoreEntry> For(GameMode mode) =>
        HighScores.Where(e => e.Mode == mode).OrderByDescending(e => e.Score).ThenBy(e => e.Date);

    public int Best(GameMode mode) => For(mode).Select(e => e.Score).DefaultIfEmpty(0).First();

    /// <summary>Inserts an entry if it makes the table. Returns its 1-based rank, or 0 when it did not qualify.</summary>
    public int Add(HighScoreEntry entry)
    {
        if (entry.Score <= 0)
        {
            return 0;
        }

        HighScores.Add(entry);
        var kept = For(entry.Mode).Take(MaxEntriesPerMode).ToList();
        HighScores.RemoveAll(e => e.Mode == entry.Mode && !kept.Contains(e));
        int index = kept.IndexOf(entry);
        return index < 0 ? 0 : index + 1;
    }
}
