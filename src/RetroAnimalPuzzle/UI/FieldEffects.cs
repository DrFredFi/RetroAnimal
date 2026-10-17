using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>
/// Cosmetic effects attached to one playfield: board visuals (tile spin, NEXT slide, incoming heart),
/// popups/announcements/shake, particles, and the animals that fly to the heart after eating.
/// Points and food are counted on the HUD only when an animal reaches the heart; until then they are
/// "pending" so the HUD can subtract them from the real (already updated) totals.
/// </summary>
internal sealed class FieldEffects : IDisposable
{
    private const double FlightSeconds = 0.75;
    private static readonly SKColor TrailColor = new(0xFF, 0xF0, 0x80);

    private readonly List<Flight> _flights = [];
    private double _time;
    private float _heartPulse;

    private sealed class Flight
    {
        public Block Animal;
        public SKPoint From;
        public double StartAt;
        public int Points;
        public int Food;
    }

    public FieldEffects(SKPoint heartCenter)
    {
        HeartCenter = heartCenter;
    }

    public BoardVisuals Visuals { get; } = new();

    public AnimationSystem Animations { get; } = new();

    public ParticleSystem Particles { get; } = new();

    /// <summary>Where flying animals go (centre of this side's heart counter).</summary>
    public SKPoint HeartCenter { get; }

    /// <summary>Points already scored by the board but not yet delivered to the heart.</summary>
    public int PendingPoints { get; private set; }

    /// <summary>Food already eaten but not yet delivered to the heart.</summary>
    public int PendingFood { get; private set; }

    /// <summary>0..1 pulse of the heart after an arrival.</summary>
    public float HeartPulse => _heartPulse;

    public void Configure(bool reduced)
    {
        Animations.ShakeEnabled = !reduced;
        Particles.Density = reduced ? 0.35f : 1f;
    }

    /// <summary>
    /// Schedules every eater of a clear to fly to the heart once its meal ends.
    /// </summary>
    public void QueueFlights(PlayerBoard board, SKRect field, ClearResult clear, int points)
    {
        var routes = clear.Groups.SelectMany(g => g.Routes).ToList();
        if (routes.Count == 0)
        {
            return;
        }

        double startAt = _time + Math.Max(0, board.PhaseDuration - board.PhaseElapsed);
        int share = points / routes.Count;
        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            int flightPoints = share + (i == 0 ? points - share * routes.Count : 0);
            _flights.Add(new Flight
            {
                Animal = board.Grid[route.Animal],
                From = PlayfieldRenderer.CellCenter(board.Rules, field, EatTimeline.FinalCell(route)),
                StartAt = startAt,
                Points = flightPoints,
                Food = route.Path.Count,
            });
            PendingPoints += flightPoints;
            PendingFood += route.Path.Count;
        }
    }

    /// <summary>Advances everything. Returns the bites taken and the animals that reached the heart this tick.</summary>
    public (int Bites, int Arrivals) Update(double deltaSeconds, PlayerBoard? board)
    {
        _time += deltaSeconds;
        Animations.Update((float)deltaSeconds);
        Particles.Update((float)deltaSeconds);
        _heartPulse = Math.Max(0f, _heartPulse - (float)deltaSeconds * 3f);
        int bites = board is null ? 0 : Visuals.Update(board, deltaSeconds);

        int arrivals = 0;
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var flight = _flights[i];
            double k = (_time - flight.StartAt) / FlightSeconds;
            if (k < 0)
            {
                continue;
            }

            if (k >= 1)
            {
                PendingPoints -= flight.Points;
                PendingFood -= flight.Food;
                _flights.RemoveAt(i);
                _heartPulse = 1f;
                arrivals++;
                if (flight.Points > 0)
                {
                    Animations.AddPopup($"+{flight.Points}", HeartCenter.X, HeartCenter.Y - 34, new SKColor(0xFF, 0xE4, 0x5C));
                }

                Particles.Burst(HeartCenter.X, HeartCenter.Y, TrailColor, 8, 120f, stars: true);
                continue;
            }

            var p = FlightPosition(flight, (float)k);
            Particles.Burst(p.X, p.Y, TrailColor, 2, 30f, stars: true);
        }

        return (bites, arrivals);
    }

    /// <summary>Draws the animals in flight (on top of everything else).</summary>
    public void DrawFlights(SKCanvas canvas, BlockArt art, float tile)
    {
        foreach (var flight in _flights)
        {
            double k = (_time - flight.StartAt) / FlightSeconds;
            if (k < 0)
            {
                continue;
            }

            var p = FlightPosition(flight, (float)k);
            float scale = 1.4f - 0.7f * (float)k;
            bool chomp = ((int)(_time * 10) & 1) == 0;
            art.DrawCentered(canvas, flight.Animal, p.X, p.Y, tile * scale, chomp ? BlockPose.Chomp : BlockPose.Normal);
        }
    }

    public void Clear()
    {
        Animations.Clear();
        Particles.Clear();
        Visuals.Reset();
        _flights.Clear();
        PendingPoints = 0;
        PendingFood = 0;
        _heartPulse = 0;
    }

    public void Dispose() => Particles.Dispose();

    /// <summary>A curved path that rises first, then swoops into the heart.</summary>
    private SKPoint FlightPosition(Flight flight, float k)
    {
        float t = k * k * (3 - 2 * k);
        var from = flight.From;
        var to = HeartCenter;
        var control = new SKPoint((from.X + to.X) / 2f, Math.Min(from.Y, to.Y) - 120f);
        float u = 1 - t;
        return new SKPoint(
            u * u * from.X + 2 * u * t * control.X + t * t * to.X,
            u * u * from.Y + 2 * u * t * control.Y + t * t * to.Y);
    }
}
