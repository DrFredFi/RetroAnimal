using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Short-lived presentation effects for one playfield: floating score popups, big chain/level
/// announcements, and screen shake. Purely cosmetic and driven by elapsed time.
/// </summary>
internal sealed class AnimationSystem
{
    private const int MaxPopups = 16;

    private readonly Popup[] _popups = new Popup[MaxPopups];
    private int _nextPopup;
    private Announcement _announcement;
    private float _shake;
    private float _time;

    private struct Popup
    {
        public string Text;
        public float X, Y, Age, Life;
        public SKColor Color;
    }

    private struct Announcement
    {
        public string? Text;
        public string? Subtitle;
        public float Age, Life;
        public SKColor Color;
    }

    /// <summary>When false, shake is suppressed (reduced-motion setting).</summary>
    public bool ShakeEnabled { get; set; } = true;

    public void AddPopup(string text, float x, float y, SKColor color)
    {
        _popups[_nextPopup] = new Popup { Text = text, X = x, Y = y, Age = 0, Life = 1.0f, Color = color };
        _nextPopup = (_nextPopup + 1) % MaxPopups;
    }

    public void Announce(string text, SKColor color, string? subtitle = null, float life = 1.2f)
    {
        _announcement = new Announcement { Text = text, Subtitle = subtitle, Age = 0, Life = life, Color = color };
    }

    public void Shake(float amount)
    {
        if (ShakeEnabled)
        {
            _shake = Math.Max(_shake, amount);
        }
    }

    public void Clear()
    {
        Array.Clear(_popups);
        _announcement = default;
        _shake = 0;
    }

    public void Update(float deltaSeconds)
    {
        _time += deltaSeconds;
        for (int i = 0; i < MaxPopups; i++)
        {
            if (_popups[i].Text is not null)
            {
                _popups[i].Age += deltaSeconds;
                if (_popups[i].Age >= _popups[i].Life)
                {
                    _popups[i].Text = null!;
                }
            }
        }

        if (_announcement.Text is not null)
        {
            _announcement.Age += deltaSeconds;
            if (_announcement.Age >= _announcement.Life)
            {
                _announcement.Text = null;
            }
        }

        _shake = Math.Max(0, _shake - deltaSeconds * 18f);
    }

    /// <summary>Current shake offset to apply to the playfield.</summary>
    public SKPoint ShakeOffset => _shake <= 0
        ? SKPoint.Empty
        : new SKPoint(MathF.Sin(_time * 71f) * _shake, MathF.Cos(_time * 53f) * _shake);

    public void Draw(SKCanvas canvas, SKRect field, UiRenderer ui)
    {
        for (int i = 0; i < MaxPopups; i++)
        {
            ref var p = ref _popups[i];
            if (p.Text is null)
            {
                continue;
            }

            float k = p.Age / p.Life;
            byte alpha = (byte)(255 * Math.Clamp((1f - k) * 2f, 0f, 1f));
            ui.OutlinedText(canvas, p.Text, p.X, p.Y - k * 34f, 18f, p.Color.WithAlpha(alpha),
                Palette.Outline.WithAlpha(alpha), UiFont.Heading);
        }

        if (_announcement.Text is { } text)
        {
            float k = _announcement.Age / _announcement.Life;
            float pop = k < 0.15f ? 0.4f + k / 0.15f * 0.8f : k < 0.25f ? 1.2f - (k - 0.15f) / 0.1f * 0.2f : 1f;
            byte alpha = (byte)(255 * Math.Clamp((1f - k) * 4f, 0f, 1f));
            float size = Math.Min(40f, field.Width / Math.Max(4, text.Length) * 1.6f) * pop;
            float y = field.MidY - 30f;
            ui.OutlinedText(canvas, text, field.MidX, y, size, _announcement.Color.WithAlpha(alpha),
                Palette.Outline.WithAlpha(alpha), UiFont.Heading, outlineWidth: 5f);
            if (_announcement.Subtitle is { } sub)
            {
                ui.OutlinedText(canvas, sub, field.MidX, y + 30f, 18f * Math.Min(pop, 1f), SKColors.White.WithAlpha(alpha),
                    Palette.Outline.WithAlpha(alpha), UiFont.Heading);
            }
        }
    }
}
