namespace RetroAnimalPuzzle.Audio;

/// <summary>
/// Tiny offline synthesizer that renders every sound effect and the two music loops as mono float
/// samples. All audio in the game is generated here, so no audio files are required.
/// </summary>
internal static class Synth
{
    public const int SampleRate = 44100;

    private enum Wave
    {
        Square,
        Pulse25,
        Triangle,
        Sine,
    }

    // ---- Note helpers --------------------------------------------------------------------
    private static double Note(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    private const int C4 = 60, D4 = 62, E4 = 64, F4 = 65, G4 = 67, A4 = 69, B4 = 71;
    private const int C5 = 72, E5 = 76, G5 = 79, C6 = 84, E6 = 88, G6 = 91;

    // ---- Effects -------------------------------------------------------------------------
    public static float[] Render(Sound sound) => sound switch
    {
        Sound.MenuMove => Build(0.05, b => Tone(b, 0, 0.04, 880, 880, Wave.Square, 0.16, 60)),
        Sound.MenuSelect => Build(0.22, b =>
        {
            Tone(b, 0, 0.06, Note(E5), Note(E5), Wave.Square, 0.18, 20);
            Tone(b, 0.06, 0.15, Note(C6), Note(C6), Wave.Square, 0.18, 14);
        }),
        Sound.MenuBack => Build(0.16, b =>
        {
            Tone(b, 0, 0.06, Note(G5), Note(G5), Wave.Square, 0.16, 20);
            Tone(b, 0.06, 0.1, Note(C5), Note(C5), Wave.Square, 0.16, 20);
        }),
        Sound.Move => Build(0.04, b => Tone(b, 0, 0.035, 520, 500, Wave.Pulse25, 0.12, 70)),
        Sound.Rotate => Build(0.07, b => Tone(b, 0, 0.065, 650, 1050, Wave.Square, 0.12, 30)),
        Sound.Land => Build(0.14, b =>
        {
            Tone(b, 0, 0.13, 170, 55, Wave.Sine, 0.55, 18);
            Noise(b, 0, 0.04, 0.18, 60, 0.35);
        }),
        Sound.HardDrop => Build(0.2, b =>
        {
            Noise(b, 0, 0.1, 0.22, 10, 0.12);
            Tone(b, 0.06, 0.14, 220, 45, Wave.Sine, 0.65, 16);
        }),
        // "Ham!": the mouth opens when an animal starts eating.
        Sound.Eat => Build(0.16, b =>
        {
            Tone(b, 0, 0.12, 260, 720, Wave.Sine, 0.45, 14);
            Noise(b, 0, 0.03, 0.12, 60, 0.4);
        }),
        // One bite, played for every food tile an animal walks over.
        Sound.Chew => Build(0.09, b =>
        {
            Noise(b, 0, 0.07, 0.36, 38, 0.22);
            Tone(b, 0, 0.05, 190, 110, Wave.Square, 0.12, 45);
        }),
        Sound.Chain => Build(0.34, b =>
        {
            int[] notes = [C5, E5, G5, C6];
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(b, i * 0.06, 0.1, Note(notes[i]), Note(notes[i]), Wave.Pulse25, 0.16, 18);
            }
        }),
        Sound.Coin => Build(0.42, b =>
        {
            Tone(b, 0, 0.07, 988, 988, Wave.Square, 0.16, 5);
            Tone(b, 0.07, 0.34, 1319, 1319, Wave.Square, 0.16, 9);
        }),
        Sound.LevelUp => Build(0.8, b =>
        {
            int[] notes = [C5, E5, G5, C6, E6, G6];
            for (int i = 0; i < notes.Length; i++)
            {
                double length = i == notes.Length - 1 ? 0.36 : 0.08;
                Tone(b, i * 0.075, length, Note(notes[i]), Note(notes[i]), Wave.Triangle, 0.3, i == notes.Length - 1 ? 6 : 14);
            }
        }),
        Sound.GarbageDrop => Build(0.4, b =>
        {
            Tone(b, 0, 0.38, 210, 55, Wave.Square, 0.14, 7);
            Noise(b, 0, 0.3, 0.16, 9, 0.08);
        }),
        Sound.SendSmall => Build(0.16, b => Tone(b, 0, 0.14, 420, 980, Wave.Square, 0.13, 12)),
        Sound.SendMedium => Build(0.34, b =>
        {
            Tone(b, 0, 0.14, 380, 900, Wave.Square, 0.13, 10);
            Tone(b, 0.1, 0.18, 520, 1250, Wave.Square, 0.13, 10);
            Noise(b, 0, 0.3, 0.1, 8, 0.15);
        }),
        Sound.SendLarge => Build(0.7, b =>
        {
            Tone(b, 0, 0.45, 160, 1300, Wave.Square, 0.14, 3);
            Tone(b, 0, 0.5, 80, 330, Wave.Triangle, 0.3, 3);
            Noise(b, 0, 0.55, 0.14, 4, 0.12);
            int[] notes = [C5, E5, G5, C6];
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(b, 0.35 + i * 0.07, 0.1, Note(notes[i]), Note(notes[i]), Wave.Pulse25, 0.12, 15);
            }
        }),
        Sound.HeartArrive => Build(0.3, b =>
        {
            Tone(b, 0, 0.2, 1568, 1568, Wave.Triangle, 0.28, 14);
            Tone(b, 0.05, 0.22, 2093, 2093, Wave.Triangle, 0.22, 14);
        }),
        Sound.Ready => Build(0.16, b => Tone(b, 0, 0.14, Note(A4), Note(A4), Wave.Square, 0.16, 12)),
        Sound.Go => Build(0.4, b => Tone(b, 0, 0.38, Note(A4 + 12), Note(A4 + 12), Wave.Square, 0.18, 6)),
        Sound.Pause => Build(0.2, b =>
        {
            Tone(b, 0, 0.07, Note(C6), Note(C6), Wave.Triangle, 0.25, 15);
            Tone(b, 0.08, 0.1, Note(G5), Note(G5), Wave.Triangle, 0.25, 15);
        }),
        Sound.RoundWin => Build(1.0, b =>
        {
            (int Note, double Start, double Length)[] fanfare = [(C5, 0, 0.1), (C5, 0.12, 0.1), (G5, 0.24, 0.14), (C6, 0.4, 0.55)];
            foreach (var (n, start, length) in fanfare)
            {
                Tone(b, start, length, Note(n), Note(n), Wave.Square, 0.17, length > 0.3 ? 4 : 12);
                Tone(b, start, length, Note(n - 12), Note(n - 12), Wave.Triangle, 0.2, length > 0.3 ? 4 : 12);
            }
        }),
        Sound.RoundLose => Build(0.95, b =>
        {
            Tone(b, 0, 0.3, Note(E4), Note(D4), Wave.Square, 0.16, 5);
            Tone(b, 0.32, 0.6, Note(D4), Note(C4 - 1), Wave.Square, 0.16, 3);
        }),
        Sound.GameOver => Build(1.2, b =>
        {
            int[] notes = [G4, E4, C4, G4 - 12];
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(b, i * 0.22, i == 3 ? 0.5 : 0.2, Note(notes[i]), Note(notes[i]), Wave.Triangle, 0.32, i == 3 ? 3 : 8);
            }
        }),
        _ => Build(0.01, _ => { }),
    };

    // ---- Music ---------------------------------------------------------------------------
    /// <summary>Renders a seamless music loop: bass, arpeggiated chords, a melody and light drums.</summary>
    public static float[] RenderMusic(MusicTrack track)
    {
        bool game = track == MusicTrack.Game;
        double bpm = game ? 144 : 112;
        double beat = 60.0 / bpm;
        int bars = 8;
        int[][] chords = game
            ? [[C4, E4, G4], [A4 - 12, C4, E4], [F4 - 12, A4 - 12, C4], [G4 - 12, B4 - 12, D4]]
            : [[F4 - 12, A4 - 12, C4], [C4, E4, G4], [D4, F4, A4], [A4 - 14, D4, F4]];
        int[] scale = game ? [C5, D4 + 12, E5, G5, A4 + 12] : [F4 + 12, G4 + 12, A4 + 12, C6, D4 + 24];

        var buffer = new float[(int)(bars * 4 * beat * SampleRate)];
        var random = new Random(game ? 1995 : 1996);

        for (int bar = 0; bar < bars; bar++)
        {
            int[] chord = chords[bar / 2 % chords.Length];
            double barStart = bar * 4 * beat;

            for (int eighth = 0; eighth < 8; eighth++)
            {
                double t = barStart + eighth * beat / 2;
                int bassNote = chord[0] - 12 + (eighth % 4 == 3 ? 7 : 0);
                Tone(buffer, t, beat / 2 * 0.9, Note(bassNote), Note(bassNote), Wave.Triangle, 0.32, 3);
                int arp = chord[eighth % 3] + 12;
                Tone(buffer, t, beat / 2 * 0.5, Note(arp), Note(arp), Wave.Pulse25, 0.045, 10);

                // Drums: kick on beats 1 and 3, snare on 2 and 4, hats on every eighth.
                if (eighth % 4 == 0) Tone(buffer, t, 0.12, 150, 45, Wave.Sine, game ? 0.5 : 0.35, 20);
                if (eighth % 4 == 2) Noise(buffer, t, 0.1, game ? 0.16 : 0.1, 25, 0.5);
                Noise(buffer, t, 0.03, 0.04, 80, 1.0);
            }

            // Melody: a fixed pseudo-random phrase over the scale, repeating every two bars with a variation.
            int previous = 2;
            for (int step = 0; step < 8; step++)
            {
                if (random.NextDouble() < 0.25)
                {
                    continue;
                }

                previous = Math.Clamp(previous + random.Next(-2, 3), 0, scale.Length - 1);
                double t = barStart + step * beat / 2;
                double length = random.NextDouble() < 0.3 ? beat : beat / 2;
                Tone(buffer, t, length * 0.85, Note(scale[previous]), Note(scale[previous]), Wave.Square, 0.075, 4);
            }
        }

        Normalize(buffer, 0.55f);
        return buffer;
    }

    // ---- Primitives ----------------------------------------------------------------------
    private static float[] Build(double seconds, Action<float[]> render)
    {
        var buffer = new float[(int)(seconds * SampleRate)];
        render(buffer);
        return buffer;
    }

    /// <summary>Adds a tone with a linear pitch glide and an exponential decay envelope (short attack/release).</summary>
    private static void Tone(float[] buffer, double start, double length, double fromHz, double toHz, Wave wave, double volume, double decay)
    {
        int first = (int)(start * SampleRate);
        int count = (int)(length * SampleRate);
        double phase = 0;
        for (int i = 0; i < count && first + i < buffer.Length; i++)
        {
            double t = (double)i / SampleRate;
            double k = (double)i / count;
            double hz = fromHz + (toHz - fromHz) * k;
            phase += hz / SampleRate;
            double p = phase - Math.Floor(phase);
            double sample = wave switch
            {
                Wave.Square => p < 0.5 ? 1 : -1,
                Wave.Pulse25 => p < 0.25 ? 1 : -1,
                Wave.Triangle => 4 * Math.Abs(p - 0.5) - 1,
                _ => Math.Sin(p * Math.Tau),
            };
            double envelope = Math.Exp(-decay * t) * Math.Min(1, i / (0.002 * SampleRate)) * Math.Min(1, (count - i) / (0.004 * SampleRate));
            buffer[first + i] += (float)(sample * volume * envelope);
        }
    }

    /// <summary>Adds decaying noise passed through a one-pole low-pass (smaller <paramref name="brightness"/> = darker).</summary>
    private static void Noise(float[] buffer, double start, double length, double volume, double decay, double brightness)
    {
        var random = new Random(unchecked((int)(start * 1000) + 7));
        int first = (int)(start * SampleRate);
        int count = (int)(length * SampleRate);
        double filtered = 0;
        for (int i = 0; i < count && first + i < buffer.Length; i++)
        {
            double white = random.NextDouble() * 2 - 1;
            filtered += (white - filtered) * brightness;
            double envelope = Math.Exp(-decay * i / SampleRate) * Math.Min(1, (count - i) / (0.003 * SampleRate));
            buffer[first + i] += (float)(filtered * volume * envelope);
        }
    }

    private static void Normalize(float[] buffer, float peak)
    {
        float max = 0;
        foreach (float s in buffer)
        {
            max = Math.Max(max, Math.Abs(s));
        }

        if (max > 0)
        {
            float gain = peak / max;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= gain;
            }
        }
    }
}
