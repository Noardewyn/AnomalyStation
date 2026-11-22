using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>DV62: tg's visible gases tint, dim and shorten a beam, and add to the fog it lights.</summary>
public class GasOpticsTests
{
    private const float Tile = 2f;

    // Kind 1 plasma (purple), 2 water vapour (white), 3 miasma (brown), as the client's sprites look.
    private static GasLook Look(int kind) => kind switch
    {
        1 => new GasLook("plasma", 0.75f, 0.2f, 0.85f, 0.6f),
        2 => new GasLook("water_vapor", 0.95f, 0.95f, 0.95f, 0.6f),
        3 => new GasLook("miasma", 0.45f, 0.35f, 0.2f, 0.6f),
        _ => new GasLook(null, 0.8f, 0.8f, 0.8f, 0.5f),
    };

    private static CellOptics Cell(int kind, int level) => GasOptics.Cell(new[] { new GasLevel((byte)kind, (byte)level) }, Look, Tile);

    private static (float R, float G, float B) Through(CellOptics everywhere, float range = 7f) =>
        GasOptics.Transmittance((_, _) => everywhere, 10f, 10f, 1f, 0f, range, Tile);

    [Fact]
    public void NoGasLetsTheWholeBeamThrough()
    {
        Assert.False(GasOptics.Cell(Array.Empty<GasLevel>(), Look, Tile).Any);
        Assert.Equal((1f, 1f, 1f), Through(CellOptics.Clear));
    }

    [Fact]
    public void AGasStrengthIsTheHazesDensityOfItsOverlay()
    {
        float a = GasVisual.Alpha(40) / 255f * 0.6f;
        Assert.Equal(-MathF.Log(1f - a) / (GasOptics.TilesToOpacity * Tile), GasOptics.Strength(40, 0.6f, Tile), 5);
        Assert.Equal(0f, GasOptics.Strength(0, 0.6f, Tile));
    }

    [Fact]
    public void PlasmaLetsItsPurpleThroughAndStopsTheGreen()
    {
        (float r, float g, float b) = Through(Cell(1, 60));
        Assert.True(g < r && g < b, $"plasma lets through r {r} g {g} b {b}");
        Assert.True(r < 1f && b < 1f);
    }

    [Fact]
    public void SteamGlowsWhiteAndBrightWhereMiasmaEatsTheLight()
    {
        CellOptics steam = Cell(2, 60), miasma = Cell(3, 60);
        Assert.True(steam.AlbedoR > 0.8f && steam.AlbedoG > 0.8f && steam.AlbedoB > 0.8f, $"steam albedo {steam}");
        Assert.True(miasma.AlbedoR < 0.3f, $"miasma albedo {miasma}");
        (float sr, float sg, float sb) = Through(steam);
        Assert.True(Math.Abs(sr - sg) < 0.02f && Math.Abs(sg - sb) < 0.02f, "steam does not tint");
        (float mr, _, _) = Through(miasma);
        Assert.True(mr < sr, $"miasma {mr} lets less through than steam {sr}");
    }

    [Fact]
    public void MoreGasLetsLessThroughAndAFarPocketMattersLess()
    {
        Assert.True(Through(Cell(1, 70)).G < Through(Cell(1, 20)).G);
        CellOptics plasma = Cell(1, 60);
        var near = GasOptics.Transmittance((x, _) => x <= 12 ? plasma : CellOptics.Clear, 10f, 10f, 1f, 0f, 7f, Tile);
        var far = GasOptics.Transmittance((x, _) => x >= 15 ? plasma : CellOptics.Clear, 10f, 10f, 1f, 0f, 7f, Tile);
        Assert.True(near.G < far.G, $"near {near.G} far {far.G}");
    }

    [Fact]
    public void TwoGasesInOneCellAddUp()
    {
        CellOptics both = GasOptics.Cell(new[] { new GasLevel(1, 40), new GasLevel(2, 40) }, Look, Tile);
        CellOptics plasma = Cell(1, 40), steam = Cell(2, 40);
        Assert.Equal(plasma.ExtG + steam.ExtG, both.ExtG, 4);
        Assert.Equal(plasma.FogDensity + steam.FogDensity, both.FogDensity, 4);
    }
}
