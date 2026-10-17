using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Fixed-size pool of small particles (crumbs and sparkles). Spawning never allocates; when the pool
/// is full the oldest slots are recycled, so the count is bounded no matter how big a combo gets.
/// </summary>
internal sealed class ParticleSystem : IDisposable
{
    private struct Particle
    {
        public float X, Y, Vx, Vy, Life, MaxLife, Size, Spin, Angle;
        public SKColor Color;
        public bool Star;
    }

    private readonly Particle[] _particles;
    private readonly Random _random = new();
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private int _next;

    public ParticleSystem(int capacity = 320)
    {
        _particles = new Particle[capacity];
    }

    /// <summary>Scales how many particles bursts emit (0 disables them).</summary>
    public float Density { get; set; } = 1f;

    public void Burst(float x, float y, SKColor color, int count, float speed = 140f, bool stars = false)
    {
        int n = (int)MathF.Round(count * Density);
        for (int i = 0; i < n; i++)
        {
            float angle = (float)(_random.NextDouble() * MathF.Tau);
            float v = speed * (0.35f + (float)_random.NextDouble() * 0.9f);
            ref var p = ref _particles[_next];
            _next = (_next + 1) % _particles.Length;
            p.X = x;
            p.Y = y;
            p.Vx = MathF.Cos(angle) * v;
            p.Vy = MathF.Sin(angle) * v - speed * 0.6f;
            p.MaxLife = p.Life = 0.45f + (float)_random.NextDouble() * 0.5f;
            p.Size = stars ? 4f + (float)_random.NextDouble() * 3f : 2f + (float)_random.NextDouble() * 2.5f;
            p.Spin = ((float)_random.NextDouble() - 0.5f) * 720f;
            p.Angle = 0;
            p.Color = color;
            p.Star = stars;
        }
    }

    public void Update(float deltaSeconds)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            if (p.Life <= 0)
            {
                continue;
            }

            p.Life -= deltaSeconds;
            p.Vy += 420f * deltaSeconds;
            p.X += p.Vx * deltaSeconds;
            p.Y += p.Vy * deltaSeconds;
            p.Angle += p.Spin * deltaSeconds;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i].Life = 0;
        }
    }

    public void Draw(SKCanvas canvas)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            if (p.Life <= 0)
            {
                continue;
            }

            byte alpha = (byte)(255 * Math.Clamp(p.Life / p.MaxLife * 1.5f, 0f, 1f));
            _paint.Color = p.Color.WithAlpha(alpha);
            if (p.Star)
            {
                canvas.Save();
                canvas.Translate(p.X, p.Y);
                canvas.RotateDegrees(p.Angle);
                canvas.DrawRect(-p.Size, -p.Size * 0.25f, p.Size * 2, p.Size * 0.5f, _paint);
                canvas.DrawRect(-p.Size * 0.25f, -p.Size, p.Size * 0.5f, p.Size * 2, _paint);
                canvas.Restore();
            }
            else
            {
                canvas.DrawCircle(p.X, p.Y, p.Size, _paint);
            }
        }
    }

    public void Dispose() => _paint.Dispose();
}
