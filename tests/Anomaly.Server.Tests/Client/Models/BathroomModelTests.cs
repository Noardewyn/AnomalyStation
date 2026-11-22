using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// Gaps H1: the bathroom models follow tg's states. The toilet's seat lid swings up on its hinge while tg shows the lid open
/// (toilet1x), its cistern lid is off while the cistern is open (toiletx1) and its bowl's water shows with tg's "toilet-water"
/// overlay; the running shower pours its water (tg's "water" overlay) on two planes under its head.
/// </summary>
public sealed class BathroomModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("toilet00", false, true)]
    [InlineData("toilet01", false, false)]
    [InlineData("toilet10", true, true)]
    [InlineData("toilet11", true, false)]
    public void TheToiletsLidSwingsUpAndItsCisternLidComesOffByTgsState(string state, bool lidUp, bool cisternLid)
    {
        ModelEntry toilet = Catalog.Find("icons/obj/watercloset.dmi", state)!;
        Assert.Equal("toilet", toilet.Id);
        Assert.Equal(lidUp, CrateLook.Open(toilet, state, []));
        ModelPart lid = toilet.Parts["lid"];
        Assert.True(lid.Pivot);
        Assert.Equal(100f, lid.Degrees);
        Assert.True(lid.ShownIn(state)); // the lid is always there, swung or not
        Assert.Equal(cisternLid, toilet.Parts["cistern"].ShownIn(state));
        Assert.True(toilet.Parts["model"].ShownIn(state));
    }

    [Fact]
    public void TheToiletsBowlWaterShowsWithTgsWaterOverlay()
    {
        ModelEntry toilet = Catalog.Find("icons/obj/watercloset.dmi", "toilet10")!;
        Assert.True(toilet.Parts["water"].DrawnWith(new[] { "toilet-water" }));
        Assert.False(toilet.Parts["water"].DrawnWith(System.Array.Empty<string>())); // flushing, or the lid down
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, toilet.Parts["lid"].File)));
        foreach (string node in new[] { "toilet_Porcelain_Body", "toilet_Cistern_Lid", "toilet_Seat_Lid.001", "toilet_Bowl_Water" })
            Assert.Contains(file.MeshNodes, n => n.Name == node);
    }

    [Fact]
    public void TheShowerPoursItsWaterWhileTgShowsIt()
    {
        ModelEntry shower = Catalog.Find("icons/obj/watercloset.dmi", "shower")!;
        Assert.Equal("wall_shower", shower.Id);
        ModelPart water = shower.Parts["water"];
        Assert.True(water.DrawnWith(new[] { "water" }));
        Assert.False(water.DrawnWith(System.Array.Empty<string>()));
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, water.File)));
        Assert.Single(file.Materials);
        Assert.True(file.Materials[0].Blend); // only tg's drops show
        Assert.Single(file.Images);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root, "..", "tg", "icons", "obj", "watercloset.png")), file.Images[0]);
    }
}
