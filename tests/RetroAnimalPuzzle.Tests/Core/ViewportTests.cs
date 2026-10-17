using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Tests.Core;

public class ViewportTests
{
    [Fact]
    public void ExactLogicalSize_HasUnitScaleAndNoOffset()
    {
        var viewport = Viewport.Fit(800, 600);

        Assert.Equal(1f, viewport.Scale);
        Assert.Equal(0f, viewport.OffsetX);
        Assert.Equal(0f, viewport.OffsetY);
    }

    [Fact]
    public void WideWindow_IsPillarboxedAndCentered()
    {
        var viewport = Viewport.Fit(1920, 1080);

        Assert.Equal(1.8f, viewport.Scale, 3);
        Assert.Equal((1920 - 800 * 1.8f) / 2f, viewport.OffsetX, 3);
        Assert.Equal(0f, viewport.OffsetY, 3);
    }

    [Fact]
    public void TallWindow_IsLetterboxed()
    {
        var viewport = Viewport.Fit(800, 1000);

        Assert.Equal(1f, viewport.Scale);
        Assert.Equal(0f, viewport.OffsetX);
        Assert.Equal(200f, viewport.OffsetY);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(-5, -5)]
    public void DegenerateWindow_FallsBackToIdentity(int width, int height)
    {
        var viewport = Viewport.Fit(width, height);

        Assert.Equal(new Viewport(1f, 0f, 0f), viewport);
    }

    [Fact]
    public void ScreenAndLogicalConversions_RoundTrip()
    {
        var viewport = Viewport.Fit(1366, 768);

        var (sx, sy) = viewport.ToScreen(123f, 456f);
        var (lx, ly) = viewport.ToLogical(sx, sy);

        Assert.Equal(123f, lx, 3);
        Assert.Equal(456f, ly, 3);
    }
}
