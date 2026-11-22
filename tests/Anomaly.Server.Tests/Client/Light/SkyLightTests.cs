using Anomaly.Client.Shared.Light;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>D288: the light of space reaches the cells open to the sky, in the stars' colour from above and the lava planet's from below.</summary>
public class SkyLightTests
{
    [Fact]
    public void OnlyOpenSpaceIsOpenToTheSkyWithoutCeilings()
    {
        var cells = new ArrayCells(4, 3);
        cells.SetSpace(1, 1, true);  // open space
        cells.SetSpace(2, 1, true);
        cells.SetOpaque(2, 1, true); // an opaque thing in space
        cells.SetOpaque(3, 1, true); // a wall
        var texels = new byte[5 * 4];
        texels[0] = 7; // stale data is cleared
        SkyLight.Fill(texels, cells, 1);
        Assert.Equal(255, texels[1 * 5 + 1]);
        Assert.Equal(0, texels[1 * 5 + 2]);
        Assert.Equal(0, texels[1 * 5 + 3]);
        Assert.Equal(0, texels[1 * 5 + 4]); // a floor
        Assert.Equal(0, texels[0]);
        Assert.Equal(255, SkyLight.At(cells, 1, 1, 1));
    }

    [Fact]
    public void TheStarsAreBlueFromAboveThePlanetRedFromBelowAndNoneAtZero()
    {
        var (up, side, down) = SkyLight.Cube(1f);
        Assert.True(up.B > up.R, "the stars: tg's COLOR_STARLIGHT is blue");
        Assert.True(down.R > 2f * down.G && down.R > 2f * down.B, "the planet: magma red");
        Assert.True(side.B > 0f && side.R > up.R * SkyLight.StarSide / SkyLight.StarUp, "the sides see a little of the planet");
        var (u0, s0, d0) = SkyLight.Cube(0f);
        Assert.Equal((0f, 0f, 0f), u0);
        Assert.Equal((0f, 0f, 0f), s0);
        Assert.Equal((0f, 0f, 0f), d0);
    }
}
