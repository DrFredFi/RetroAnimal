using System.Diagnostics;
using RetroAnimalPuzzle.Host;

namespace RetroAnimalPuzzle.Application;

/// <summary>
/// Fixed-timestep loop driven by <see cref="System.Windows.Forms.Application.Idle"/>.
/// The simulation always advances in steps of exactly <see cref="StepSeconds"/>; rendering is capped
/// separately and waits with <c>MsgWaitForMultipleObjectsEx</c> so input stays responsive without busy-spinning.
/// </summary>
internal sealed class GameLoop : IDisposable
{
    private const int MaxStepsPerFrame = 5;
    private const double MaxFrameSeconds = 0.25;

    private readonly Action<double> _update;
    private readonly Action _render;
    private readonly double _minFrameSeconds;
    private readonly Stopwatch _clock = new();
    private double _previousTime;
    private double _accumulator;
    private double _lastRenderTime;
    private bool _running;

    private int _framesThisSecond;
    private double _fpsWindowStart;

    public GameLoop(Action<double> update, Action render, double updatesPerSecond = 60, double maxFramesPerSecond = 60)
    {
        _update = update;
        _render = render;
        StepSeconds = 1.0 / updatesPerSecond;
        _minFrameSeconds = 1.0 / maxFramesPerSecond;
    }

    public double StepSeconds { get; }

    public double FramesPerSecond { get; private set; }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        NativeMethods.BeginHighResolutionTimer();
        _clock.Restart();
        _previousTime = 0;
        System.Windows.Forms.Application.Idle += OnIdle;
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        System.Windows.Forms.Application.Idle -= OnIdle;
        NativeMethods.EndHighResolutionTimer();
    }

    public void Dispose() => Stop();

    private void OnIdle(object? sender, EventArgs e)
    {
        while (_running && NativeMethods.IsApplicationIdle())
        {
            RunFrame();
        }
    }

    private void RunFrame()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double elapsed = Math.Min(now - _previousTime, MaxFrameSeconds);
        _previousTime = now;
        _accumulator += elapsed;

        int steps = 0;
        while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
        {
            _update(StepSeconds);
            _accumulator -= StepSeconds;
            steps++;
        }

        if (steps == MaxStepsPerFrame)
        {
            // Too far behind (debugger, window drag): drop the backlog instead of spiralling.
            _accumulator = 0;
        }

        _render();
        _lastRenderTime = _clock.Elapsed.TotalSeconds;
        CountFrame(_lastRenderTime);

        double untilNextFrame = _minFrameSeconds - (_clock.Elapsed.TotalSeconds - now);
        if (untilNextFrame >= 0.001)
        {
            NativeMethods.WaitForMessageOrTimeout((uint)(untilNextFrame * 1000));
        }
    }

    private void CountFrame(double now)
    {
        _framesThisSecond++;
        if (now - _fpsWindowStart >= 1.0)
        {
            FramesPerSecond = _framesThisSecond / (now - _fpsWindowStart);
            _framesThisSecond = 0;
            _fpsWindowStart = now;
        }
    }
}
