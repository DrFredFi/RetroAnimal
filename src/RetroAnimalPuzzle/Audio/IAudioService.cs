namespace RetroAnimalPuzzle.Audio;

public enum Sound
{
    MenuMove,
    MenuSelect,
    MenuBack,
    Move,
    Rotate,
    Land,
    HardDrop,
    Eat,
    Chew,
    Chain,
    Coin,
    LevelUp,
    GarbageDrop,
    SendSmall,
    SendMedium,
    SendLarge,
    HeartArrive,
    Ready,
    Go,
    Pause,
    RoundWin,
    RoundLose,
    GameOver,
}

public enum MusicTrack
{
    None,
    Menu,
    Game,
}

/// <summary>Fire-and-forget audio. Implementations must never block or throw into the game loop.</summary>
internal interface IAudioService : IDisposable
{
    /// <param name="pitch">Playback-rate multiplier (1 = original).</param>
    void Play(Sound sound, float pitch = 1f, float volume = 1f);

    void PlayMusic(MusicTrack track);

    /// <summary>Volumes in 0..1.</summary>
    void SetVolumes(float master, float effects, float music);
}

/// <summary>Silent fallback used when no audio device is available.</summary>
internal sealed class NullAudioService : IAudioService
{
    public void Play(Sound sound, float pitch = 1f, float volume = 1f)
    {
    }

    public void PlayMusic(MusicTrack track)
    {
    }

    public void SetVolumes(float master, float effects, float music)
    {
    }

    public void Dispose()
    {
    }
}
