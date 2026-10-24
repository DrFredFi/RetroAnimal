using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace RetroAnimalPuzzle.Audio;

/// <summary>
/// NAudio (WinMM) implementation. A single mixer runs on NAudio's playback thread; effects are added as
/// short in-memory voices and music as a looping voice, so <see cref="Play"/> returns immediately.
/// Sounds are synthesized at start-up; a WAV or MP3 in <c>Assets/Audio</c> named after a <see cref="Sound"/>
/// (e.g. <c>eat.wav</c>) or <c>music_game</c> / <c>music_menu</c> replaces the generated one.
/// </summary>
internal sealed class AudioService : IAudioService
{
    private const int MaxVoices = 24;

    private readonly WaveOut _output;
    private readonly MixingSampleProvider _mixer;
    private readonly Dictionary<Sound, float[]> _sounds = [];
    private readonly Dictionary<MusicTrack, float[]> _music = [];
    private readonly Lock _musicLock = new();
    private LoopVoice? _musicVoice;
    private MusicTrack _requestedTrack;
    private volatile float _effectsGain = 0.64f;
    private volatile float _musicGain = 0.4f;
    private int _voices;

    private AudioService(string assetDirectory)
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(Synth.SampleRate, 1)) { ReadFully = true };
        _mixer.MixerInputEnded += (_, e) =>
        {
            if (e.SampleProvider is Voice)
            {
                Interlocked.Decrement(ref _voices);
            }
        };

        foreach (var sound in Enum.GetValues<Sound>())
        {
            _sounds[sound] = LoadClip(Path.Combine(assetDirectory, sound.ToString().ToLowerInvariant())) ?? Synth.Render(sound);
        }

        _output = new WaveOut { BufferMilliseconds = 30, NumberOfBuffers = 3 };
        _output.Init(_mixer);
        _output.Play();

        // Music takes a moment to render; do it off the UI thread.
        Task.Run(() =>
        {
            foreach (var track in new[] { MusicTrack.Menu, MusicTrack.Game })
            {
                var samples = LoadClip(Path.Combine(assetDirectory, $"music_{track.ToString().ToLowerInvariant()}"))
                    ?? Synth.RenderMusic(track);
                lock (_musicLock)
                {
                    _music[track] = samples;
                }
            }

            PlayMusic(_requestedTrack);
        });
    }

    /// <summary>Creates the real service, or a silent one if no audio device can be opened.</summary>
    public static IAudioService CreateOrSilent()
    {
        try
        {
            return new AudioService(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio"));
        }
        catch (Exception e) when (e is NAudio.MmException or InvalidOperationException or IOException or DllNotFoundException)
        {
            return new NullAudioService();
        }
    }

    public void Play(Sound sound, float pitch = 1f, float volume = 1f)
    {
        if (_effectsGain <= 0f || Volatile.Read(ref _voices) >= MaxVoices)
        {
            return;
        }

        Interlocked.Increment(ref _voices);
        _mixer.AddMixerInput(new Voice(_sounds[sound], pitch, volume * _effectsGain, _mixer.WaveFormat));
    }

    public void PlayMusic(MusicTrack track)
    {
        lock (_musicLock)
        {
            _requestedTrack = track;
            if (_musicVoice is { } current && current.Track == track && !current.Stopped)
            {
                return;
            }

            _musicVoice?.Stop();
            _musicVoice = null;
            if (track != MusicTrack.None && _music.TryGetValue(track, out var samples))
            {
                _musicVoice = new LoopVoice(track, samples, () => _musicGain, _mixer.WaveFormat);
                _mixer.AddMixerInput(_musicVoice);
            }
        }
    }

    public void SetVolumes(float master, float effects, float music)
    {
        _effectsGain = Math.Clamp(master * effects, 0f, 1f);
        _musicGain = Math.Clamp(master * music, 0f, 1f) * 0.8f;
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }

    /// <summary>Decodes <c>basePath.wav</c>, or else <c>basePath.mp3</c>, to mono samples at <see cref="Synth.SampleRate"/>.</summary>
    private static float[]? LoadClip(string basePath)
    {
        string wav = basePath + ".wav";
        string mp3 = basePath + ".mp3";
        if (!File.Exists(wav) && !File.Exists(mp3))
        {
            return null;
        }

        try
        {
            using WaveStream reader = File.Exists(wav)
                ? new WaveFileReader(wav)
                : new Mp3FileReaderBase(mp3, format => new AcmMp3FrameDecompressor(format));
            ISampleProvider provider = reader.ToSampleProvider();
            if (provider.WaveFormat.Channels == 2)
            {
                provider = new StereoToMonoSampleProvider(provider);
            }

            if (provider.WaveFormat.SampleRate != Synth.SampleRate)
            {
                provider = new WdlResamplingSampleProvider(provider, Synth.SampleRate);
            }

            var samples = new List<float>();
            var chunk = new float[Synth.SampleRate];
            int read;
            while ((read = provider.Read(chunk)) > 0)
            {
                samples.AddRange(chunk.AsSpan(0, read));
            }

            return samples.ToArray();
        }
        catch (Exception e) when (e is IOException or FormatException or InvalidOperationException or ArgumentException or NAudio.MmException)
        {
            return null;
        }
    }

    /// <summary>One-shot playback of a cached buffer with a fixed rate (pitch) and gain.</summary>
    private sealed class Voice(float[] samples, float rate, float gain, WaveFormat format) : ISampleProvider
    {
        private double _position;

        public WaveFormat WaveFormat { get; } = format;

        public int Read(Span<float> buffer)
        {
            int written = 0;
            while (written < buffer.Length)
            {
                int index = (int)_position;
                if (index >= samples.Length - 1)
                {
                    break;
                }

                double frac = _position - index;
                buffer[written++] = (float)(samples[index] + (samples[index + 1] - samples[index]) * frac) * gain;
                _position += rate;
            }

            return written;
        }
    }

    /// <summary>Endless loop whose gain is read live so volume changes apply immediately.</summary>
    private sealed class LoopVoice(MusicTrack track, float[] samples, Func<float> gain, WaveFormat format) : ISampleProvider
    {
        private int _position;
        private volatile bool _stopped;

        public MusicTrack Track { get; } = track;

        public bool Stopped => _stopped;

        public WaveFormat WaveFormat { get; } = format;

        public void Stop() => _stopped = true;

        public int Read(Span<float> buffer)
        {
            if (_stopped)
            {
                return 0;
            }

            float g = gain();
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = samples[_position] * g;
                if (++_position >= samples.Length)
                {
                    _position = 0;
                }
            }

            return buffer.Length;
        }
    }
}
