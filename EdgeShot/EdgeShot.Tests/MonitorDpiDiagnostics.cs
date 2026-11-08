using System.Drawing;
using System.Runtime.InteropServices;

internal readonly record struct MonitorDpiResult(bool Success, uint X, uint Y, int HResult);

internal static class MonitorDpiDiagnostics
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int EffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll", ExactSpelling = true)]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    public static MonitorDpiResult ReadAt(Point point)
    {
        var monitor = MonitorFromPoint(new NativePoint(point.X, point.Y), MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return new MonitorDpiResult(false, 0, 0, Marshal.GetLastWin32Error());
        }

        var result = GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out var dpiY);
        return new MonitorDpiResult(result >= 0, dpiX, dpiY, result);
    }
}
