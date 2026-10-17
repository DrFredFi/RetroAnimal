using RetroAnimalPuzzle.Input;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>
/// Stack of screens with fade-to-black transitions. <see cref="Push"/> keeps the current screen alive
/// underneath (e.g. options opened from the pause menu); <see cref="Replace"/> clears the stack.
/// Input is withheld while a transition runs so a key press cannot hit two screens.
/// </summary>
internal sealed class ScreenManager : IDisposable
{
    private const float FadeSeconds = 0.18f;

    private readonly Stack<IScreen> _stack = new();
    private readonly SKPaint _fade = new();
    private Action? _pending;
    private float _fadeOut;
    private float _fadeIn;

    public IScreen? Current => _stack.Count > 0 ? _stack.Peek() : null;

    public bool IsTransitioning => _pending is not null || _fadeIn > 0;

    public void Replace(IScreen screen) => Transition(() =>
    {
        while (_stack.Count > 0)
        {
            DisposeScreen(_stack.Pop());
        }

        _stack.Push(screen);
    });

    public void Push(IScreen screen) => Transition(() => _stack.Push(screen));

    public void Pop() => Transition(() =>
    {
        if (_stack.Count > 1)
        {
            DisposeScreen(_stack.Pop());
        }
    });

    /// <summary>Shows the first screen immediately, without a fade-out.</summary>
    public void Start(IScreen screen)
    {
        _stack.Push(screen);
        _fadeIn = FadeSeconds;
        screen.OnEnter();
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        float dt = (float)deltaSeconds;
        if (_pending is not null)
        {
            _fadeOut += dt;
            if (_fadeOut >= FadeSeconds)
            {
                var action = _pending;
                _pending = null;
                _fadeOut = 0;
                action();
                _fadeIn = FadeSeconds;
                Current?.OnEnter();
            }

            Current?.Update(deltaSeconds, InputState.None);
            return;
        }

        if (_fadeIn > 0)
        {
            _fadeIn = Math.Max(0, _fadeIn - dt);
            Current?.Update(deltaSeconds, InputState.None);
            return;
        }

        Current?.Update(deltaSeconds, input);
    }

    public void Render(SKCanvas canvas)
    {
        Current?.Render(canvas);

        float alpha = _pending is not null ? _fadeOut / FadeSeconds : _fadeIn / FadeSeconds;
        if (alpha > 0)
        {
            _fade.Color = SKColors.Black.WithAlpha((byte)(255 * Math.Clamp(alpha, 0f, 1f)));
            canvas.DrawRect(0, 0, 800, 600, _fade);
        }
    }

    public void NotifyFocusLost() => Current?.OnFocusLost();

    public void Dispose()
    {
        while (_stack.Count > 0)
        {
            DisposeScreen(_stack.Pop());
        }

        _fade.Dispose();
    }

    private void Transition(Action action)
    {
        if (_pending is null)
        {
            _pending = action;
            _fadeOut = 0;
        }
    }

    private static void DisposeScreen(IScreen screen) => (screen as IDisposable)?.Dispose();
}
