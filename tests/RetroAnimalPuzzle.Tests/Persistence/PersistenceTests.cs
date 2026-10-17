using RetroAnimalPuzzle.Persistence;

namespace RetroAnimalPuzzle.Tests.Persistence;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rap-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static HighScoreEntry Entry(int score, GameMode mode = GameMode.Endless) =>
        new(score, 3, 40, 2, mode, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(score));

    [Fact]
    public void MissingFiles_GiveDefaults()
    {
        var saves = new SaveService(_directory);
        var settings = new SettingsService(_directory);

        Assert.Empty(saves.Load().HighScores);
        Assert.Equal(new SettingsData().MasterVolume, settings.Load().MasterVolume);
    }

    [Fact]
    public void HighScores_RoundTrip()
    {
        var saves = new SaveService(_directory);
        saves.Load();
        saves.Record(Entry(500));
        saves.Record(Entry(900, GameMode.Versus));

        var reloaded = new SaveService(_directory).Load();

        Assert.Equal(500, reloaded.Best(GameMode.Endless));
        Assert.Equal(900, reloaded.Best(GameMode.Versus));
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var service = new SettingsService(_directory);
        service.Load();
        service.Settings.MusicVolume = 2;
        service.Settings.Animations = AnimationIntensity.Reduced;
        service.Settings.KeyBindings["HardDrop"] = "Enter";
        Assert.True(service.Save());

        var reloaded = new SettingsService(_directory).Load();

        Assert.Equal(2, reloaded.MusicVolume);
        Assert.Equal(AnimationIntensity.Reduced, reloaded.Animations);
        Assert.Equal("Enter", reloaded.KeyBindings["HardDrop"]);
    }

    [Fact]
    public void MalformedFiles_FallBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "highscores.json"), "{ not json");
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "[1,2,3]");

        Assert.Empty(new SaveService(_directory).Load().HighScores);
        Assert.Equal(8, new SettingsService(_directory).Load().MasterVolume);
    }

    [Fact]
    public void FutureVersions_AreIgnored()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), """{ "Version": 99, "MasterVolume": 1 }""");

        Assert.Equal(8, new SettingsService(_directory).Load().MasterVolume);
    }

    [Fact]
    public void OutOfRangeSettings_AreClamped()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), """{ "Version": 1, "MasterVolume": 50, "SfxVolume": -3 }""");

        var settings = new SettingsService(_directory).Load();

        Assert.Equal(SettingsData.MaxVolume, settings.MasterVolume);
        Assert.Equal(0, settings.SfxVolume);
    }

    [Fact]
    public void HighScoreTable_IsSortedAndCapped()
    {
        var data = new SaveData();
        for (int i = 1; i <= 15; i++)
        {
            data.Add(Entry(i * 100));
        }

        var table = data.For(GameMode.Endless).ToList();

        Assert.Equal(SaveData.MaxEntriesPerMode, table.Count);
        Assert.Equal(1500, table[0].Score);
        Assert.True(table.Zip(table.Skip(1)).All(p => p.First.Score >= p.Second.Score));
        Assert.Equal(0, data.Add(Entry(50)));
        Assert.Equal(1, data.Add(Entry(5000)));
    }

    [Fact]
    public void UnwritableLocation_FailsGracefully()
    {
        // A path below an existing *file* can never be created as a directory.
        Directory.CreateDirectory(_directory);
        string blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "x");

        var service = new SettingsService(Path.Combine(blocker, "sub"));
        service.Load();

        Assert.False(service.Save());
    }
}
