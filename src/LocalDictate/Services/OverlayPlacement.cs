namespace LocalDictate.Services;

/// <summary>
/// Pure placement math for the recording overlay.
/// The window is pinned once to the monitor that contains the cursor when
/// recording starts, then clamped to that monitor's work rectangle only.
/// </summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class OverlayPlacement
{
    public const int Margin = 8;

    public static (int X, int Y) ClampToWorkArea(
        int x,
        int y,
        int width,
        int height,
        PixelRect work,
        int margin = Margin)
    {
        if (width < 1) width = 1;
        if (height < 1) height = 1;

        var minX = work.Left + margin;
        var minY = work.Top + margin;
        var maxX = work.Right - width - margin;
        var maxY = work.Bottom - height - margin;

        if (maxX < minX)
        {
            x = work.Left + Math.Max(0, (work.Width - width) / 2);
        }
        else
        {
            x = Math.Clamp(x, minX, maxX);
        }

        if (maxY < minY)
        {
            y = work.Top + Math.Max(0, (work.Height - height) / 2);
        }
        else
        {
            y = Math.Clamp(y, minY, maxY);
        }

        return (x, y);
    }
}
