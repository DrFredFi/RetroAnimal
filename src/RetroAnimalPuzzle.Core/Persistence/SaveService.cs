namespace RetroAnimalPuzzle.Persistence;

/// <summary>Loads and stores high scores in <c>highscores.json</c>.</summary>
public sealed class SaveService
{
    private readonly string _path;

    public SaveService(string directory)
    {
        _path = Path.Combine(directory, "highscores.json");
    }

    public SaveData Data { get; private set; } = new();

    public SaveData Load()
    {
        var loaded = JsonStore.TryLoad<SaveData>(_path);
        // Files from an unknown future version are ignored rather than misread.
        Data = loaded is { Version: SaveData.CurrentVersion } ? loaded : new SaveData();
        Data.HighScores ??= [];
        Data.HighScores.RemoveAll(e => e is null);
        return Data;
    }

    public bool Save() => JsonStore.TrySave(_path, Data);

    /// <summary>Records a finished game and saves. Returns the rank achieved (0 = not in the table).</summary>
    public int Record(HighScoreEntry entry)
    {
        int rank = Data.Add(entry);
        if (rank > 0)
        {
            Save();
        }

        return rank;
    }
}

/// <summary>Loads and stores user settings in <c>settings.json</c>.</summary>
public sealed class SettingsService
{
    private readonly string _path;

    public SettingsService(string directory)
    {
        _path = Path.Combine(directory, "settings.json");
    }

    public SettingsData Settings { get; private set; } = new();

    public SettingsData Load()
    {
        var loaded = JsonStore.TryLoad<SettingsData>(_path);
        Settings = (loaded is { Version: SettingsData.CurrentVersion } ? loaded : new SettingsData()).Sanitize();
        return Settings;
    }

    public bool Save() => JsonStore.TrySave(_path, Settings.Sanitize());
}
