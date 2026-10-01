using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LocalDictate.Services;

/// <summary>
/// Pins a window to the monitor under the cursor.
/// Call this once per recording. Do not call it on level ticks — re-reading
/// the cursor and clamping to <see cref="SystemParameters.WorkArea"/> (primary
/// only) is what made the overlay hop between screens.
/// </summary>
public static class MonitorPlacement
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint MdTEffectiveDpi = 0;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;

    public static void PinNearCursor(Window window, int offsetXDip = 16, int offsetYDip = 18)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var dpiX = 96u;
        var dpiY = 96u;
        try
        {
            if (GetDpiForMonitor(monitor, MdTEffectiveDpi, out var mx, out var my) == 0)
            {
                if (mx > 0) dpiX = mx;
                if (my > 0) dpiY = my;
            }
        }
        catch (DllNotFoundException)
        {
            // Older hosts: 96 DPI is a safe physical-pixel fallback.
        }

        var scaleX = dpiX / 96.0;
        var scaleY = dpiY / 96.0;
        var width = Math.Max(1, (int)Math.Round(window.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(window.Height * scaleY));
        var desiredX = cursor.X + (int)Math.Round(offsetXDip * scaleX);
        var desiredY = cursor.Y + (int)Math.Round(offsetYDip * scaleY);
        var work = new PixelRect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
        var (x, y) = OverlayPlacement.ClampToWorkArea(desiredX, desiredY, width, height, work);

        SetWindowPos(hwnd, HwndTopmost, x, y, width, height, SwpNoActivate);
        SyncWpfOrigin(window, x, y);
    }

    private static void SyncWpfOrigin(Window window, int deviceX, int deviceY)
    {
        if (PresentationSource.FromVisual(window)?.CompositionTarget is not { } target)
        {
            return;
        }

        var dip = target.TransformFromDevice.Transform(new Point(deviceX, deviceY));
        window.Left = dip.X;
        window.Top = dip.Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointNative lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(PointNative pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
