using System.Runtime.InteropServices;

namespace RetroAnimalPuzzle.Host;

/// <summary>Win32 calls used by the idle-driven game loop.</summary>
internal static partial class NativeMethods
{
    private const uint PM_NOREMOVE = 0x0000;
    private const uint QS_ALLINPUT = 0x04FF;
    private const uint MWMO_INPUTAVAILABLE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Handle;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [LibraryImport("user32.dll")]
    private static partial uint MsgWaitForMultipleObjectsEx(uint count, IntPtr handles, uint milliseconds, uint wakeMask, uint flags);

    [LibraryImport("winmm.dll")]
    private static partial uint timeBeginPeriod(uint period);

    [LibraryImport("winmm.dll")]
    private static partial uint timeEndPeriod(uint period);

    /// <summary>True when the message queue is empty and the loop may run another frame.</summary>
    public static bool IsApplicationIdle() => !PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);

    /// <summary>Blocks until the timeout elapses or a window message arrives, whichever comes first.</summary>
    public static void WaitForMessageOrTimeout(uint milliseconds) =>
        _ = MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, milliseconds, QS_ALLINPUT, MWMO_INPUTAVAILABLE);

    public static void BeginHighResolutionTimer() => _ = timeBeginPeriod(1);

    public static void EndHighResolutionTimer() => _ = timeEndPeriod(1);
}
