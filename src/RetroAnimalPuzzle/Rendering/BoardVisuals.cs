using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using Orientation = RetroAnimalPuzzle.Core.Orientation;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Purely cosmetic per-board animation state, derived from watching a <see cref="PlayerBoard"/> tick by tick:
/// the 3-D spin of the falling tiles after a rotation, the smooth horizontal slide, the NEXT queue shifting
/// when a pair leaves it, the incoming-food heart easing along its bar, and bites taken while eating.
/// It never feeds back into gameplay.
/// </summary>
internal sealed class BoardVisuals
{
    public const double SpinSeconds = 0.2;
    public const double NextShiftSeconds = 0.22;
    private const float SlideCellsPerSecond = 18f;

    private int _serial = int.MinValue;
    private Orientation _orientation;
    private double _spinStart = double.NegativeInfinity;
    private double _nextShiftStart = double.NegativeInfinity;
    private ClearResult? _trackedClear;
    private int _bites;

    public double Time { get; private set; }

    /// <summary>Horizontal position (in cells) at which the falling pair's pivot is drawn.</summary>
    public float PivotX { get; private set; }

    /// <summary>Pending incoming food, smoothed for the heart on the incoming bar.</summary>
    public float IncomingShown { get; private set; }

    /// <summary>0..1 through the current tile spin (1 = not spinning).</summary>
    public float SpinProgress => (float)Math.Clamp((Time - _spinStart) / SpinSeconds, 0, 1);

    /// <summary>0..1 through the NEXT queue shift (1 = settled), eased.</summary>
    public float NextShiftProgress
    {
        get
        {
            float k = (float)Math.Clamp((Time - _nextShiftStart) / NextShiftSeconds, 0, 1);
            return k * k * (3 - 2 * k);
        }
    }

    public void Reset()
    {
        _serial = int.MinValue;
        _spinStart = double.NegativeInfinity;
        _nextShiftStart = double.NegativeInfinity;
        _trackedClear = null;
        _bites = 0;
        IncomingShown = 0;
    }

    /// <summary>Advances the animations. Returns how many bites (food tiles reached) happened this tick.</summary>
    public int Update(PlayerBoard board, double deltaSeconds)
    {
        Time += deltaSeconds;

        if (board.PieceSerial != _serial)
        {
            if (_serial != int.MinValue)
            {
                _nextShiftStart = Time;
            }

            _serial = board.PieceSerial;
            _spinStart = double.NegativeInfinity;
            if (board.Current is { } spawned)
            {
                _orientation = spawned.Orientation;
                PivotX = spawned.Pivot.X;
            }
        }

        if (board.Current is { } piece)
        {
            if (piece.Orientation != _orientation)
            {
                _orientation = piece.Orientation;
                _spinStart = Time;
            }

            float step = SlideCellsPerSecond * (float)deltaSeconds;
            float target = piece.Pivot.X;
            PivotX = Math.Abs(target - PivotX) <= step ? target : PivotX + Math.Sign(target - PivotX) * step;
        }

        IncomingShown += (board.IncomingGarbage - IncomingShown) * (float)Math.Min(1, deltaSeconds * 6);

        return CountBites(board);
    }

    private int CountBites(PlayerBoard board)
    {
        if (board.Phase != BoardPhase.Eating || board.PendingClear is not { } clear)
        {
            _trackedClear = null;
            return 0;
        }

        if (!ReferenceEquals(clear, _trackedClear))
        {
            _trackedClear = clear;
            _bites = 0;
        }

        int bites = 0;
        foreach (var group in clear.Groups)
        {
            foreach (var route in group.Routes)
            {
                bites += EatTimeline.TilesEaten(board.Rules, route, board.PhaseElapsed);
            }
        }

        int fresh = bites - _bites;
        _bites = bites;
        return Math.Max(0, fresh);
    }
}

/// <summary>
/// Timing of one animal's meal, shared by drawing and sound: a pop, then for each tile on its route a quick hop
/// (first 55 % of the slot) followed by chewing; the tile disappears when the animal lands on it.
/// </summary>
internal static class EatTimeline
{
    public const double HopShare = 0.55;

    /// <summary>When the animal lands on tile <paramref name="index"/> of its route (seconds into the phase).</summary>
    public static double Arrival(GameRules rules, int index) =>
        rules.EatStartDelay + (index + HopShare) * rules.EatSecondsPerFood;

    public static int TilesEaten(GameRules rules, EaterRoute route, double elapsed)
    {
        int eaten = 0;
        while (eaten < route.Path.Count && elapsed >= Arrival(rules, eaten))
        {
            eaten++;
        }

        return eaten;
    }

    /// <summary>Where the animal is drawn (in cells, fractional) and whether it is mid-hop.</summary>
    public static (float X, float Y, bool Hopping, float Hop) Position(GameRules rules, EaterRoute route, double elapsed)
    {
        var start = route.Animal;
        if (route.Path.Count == 0 || elapsed < rules.EatStartDelay)
        {
            return (start.X, start.Y, false, 0f);
        }

        double slot = (elapsed - rules.EatStartDelay) / rules.EatSecondsPerFood;
        int index = (int)Math.Floor(slot);
        if (index >= route.Path.Count)
        {
            var last = route.Path[^1];
            return (last.X, last.Y, false, 0f);
        }

        var from = index == 0 ? start : route.Path[index - 1];
        var to = route.Path[index];
        float f = (float)(slot - index);
        if (f >= HopShare)
        {
            return (to.X, to.Y, false, 0f);
        }

        float k = f / (float)HopShare;
        k = k * k * (3 - 2 * k);
        return (from.X + (to.X - from.X) * k, from.Y + (to.Y - from.Y) * k, true, MathF.Sin(k * MathF.PI));
    }

    /// <summary>The cell the animal ends its meal on (start of its flight to the heart).</summary>
    public static Position FinalCell(EaterRoute route) => route.Path.Count > 0 ? route.Path[^1] : route.Animal;
}
