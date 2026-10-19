using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RetroAnimalPuzzle.Input;

/// <summary>
/// Reads up to four Xbox-style controllers through XInput (built into Windows, no driver or package needed).
/// Asking XInput about an empty slot is slow, so slots without a controller are re-checked only about once a second.
/// </summary>
internal sealed partial class XInputGamepads
{
    public const int MaxPads = 4;

    private const uint ErrorSuccess = 0;
    private const double ReconnectCheckSeconds = 1.0;

    private readonly bool[] _connected = new bool[MaxPads];
    private readonly double[] _nextCheck = new double[MaxPads];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _available = true;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [LibraryImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static partial uint XInputGetState(uint userIndex, out XInputState state);

    /// <summary>Whether the controller in this slot answered at the last poll.</summary>
    public bool IsConnected(int slot) => _connected[slot];

    /// <summary>Reads one slot; returns false when no controller is there (or it was not checked this time).</summary>
    public bool TryRead(int slot, out GamepadReading reading)
    {
        reading = default;
        double now = _clock.Elapsed.TotalSeconds;
        if (!_available || (!_connected[slot] && now < _nextCheck[slot]))
        {
            return false;
        }

        uint result;
        XInputState state;
        try
        {
            result = XInputGetState((uint)slot, out state);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _available = false;
            return false;
        }

        _connected[slot] = result == ErrorSuccess;
        if (!_connected[slot])
        {
            // Spread the slow empty-slot checks so they never land on the same frame.
            _nextCheck[slot] = now + ReconnectCheckSeconds + slot * 0.25;
            return false;
        }

        reading = new GamepadReading(state.Buttons, state.LeftTrigger, state.RightTrigger,
            state.ThumbLX, state.ThumbLY, state.ThumbRX, state.ThumbRY);
        return true;
    }
}
