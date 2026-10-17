using RetroAnimalPuzzle.Gameplay;

namespace RetroAnimalPuzzle.Persistence;

public enum AnimationIntensity
{
    Full,
    Reduced,
}

/// <summary>Versioned user settings. Values are clamped by <see cref="Sanitize"/> after loading.</summary>
public sealed class SettingsData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public int MasterVolume { get; set; } = 8;

    public int SfxVolume { get; set; } = 8;

    public int MusicVolume { get; set; } = 5;

    public AnimationIntensity Animations { get; set; } = AnimationIntensity.Full;

    public bool Fullscreen { get; set; }

    /// <summary>Landing preview. Off by default, like the classic game.</summary>
    public bool ShowGhost { get; set; }

    public CpuDifficulty CpuDifficulty { get; set; } = CpuDifficulty.Normal;

    /// <summary>Gameplay action name → key name. Empty means defaults; interpreted by the desktop host.</summary>
    public Dictionary<string, string> KeyBindings { get; set; } = [];

    public const int MaxVolume = 10;

    public SettingsData Sanitize()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0, MaxVolume);
        SfxVolume = Math.Clamp(SfxVolume, 0, MaxVolume);
        MusicVolume = Math.Clamp(MusicVolume, 0, MaxVolume);
        if (!Enum.IsDefined(Animations)) Animations = AnimationIntensity.Full;
        if (!Enum.IsDefined(CpuDifficulty)) CpuDifficulty = CpuDifficulty.Normal;
        KeyBindings ??= [];
        return this;
    }
}
