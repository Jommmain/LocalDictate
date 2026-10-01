using LocalDictate.Services;
using Xunit;

namespace LocalDictate.Placement.Tests;

public class OverlayPlacementTests
{
    [Fact]
    public void LeftOfPrimary_StaysOnThatMonitor()
    {
        var work = new PixelRect(-1920, 0, 0, 1080);
        var (x, y) = OverlayPlacement.ClampToWorkArea(-240, 420, 280, 88, work);

        Assert.InRange(x, work.Left + OverlayPlacement.Margin, work.Right - 280 - OverlayPlacement.Margin);
        Assert.InRange(y, work.Top + OverlayPlacement.Margin, work.Bottom - 88 - OverlayPlacement.Margin);
        Assert.True(x + 280 <= work.Right - OverlayPlacement.Margin);
        Assert.True(x < 0);
    }

    [Fact]
    public void RightOfPrimary_DoesNotSnapToOrigin()
    {
        var work = new PixelRect(1920, 0, 3840, 1080);
        var (x, _) = OverlayPlacement.ClampToWorkArea(1800, 120, 280, 88, work);

        Assert.Equal(1920 + OverlayPlacement.Margin, x);
        Assert.NotEqual(0, x);
    }

    [Fact]
    public void NearRightEdge_StaysInsideWorkArea()
    {
        var work = new PixelRect(1920, 0, 3840, 1080);
        var (x, y) = OverlayPlacement.ClampToWorkArea(3700, 1040, 280, 88, work);

        Assert.Equal(3840 - 280 - OverlayPlacement.Margin, x);
        Assert.Equal(1080 - 88 - OverlayPlacement.Margin, y);
        Assert.True(x >= work.Left);
        Assert.True(y >= work.Top);
    }
}
