using System.IO;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// Tram walls are blocks (the owner, 2026-11-19): tg's /obj/structure/tram is drawn as a full-tile window of its smoothing states, not a flat panel model, so the
/// signs and the controller tg maps in or on it hang on it as wall furniture. The washing machine's glass swings with its door.
/// </summary>
public sealed class TramPieceTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Fact]
    public void ATramWallIsABlockAndItsSpoilerIsNot()
    {
        Assert.Equal(RenderKind.FullWindow, PropKinds.Classify("/obj/structure/tram", "tram-part-12"));
        Assert.Equal(RenderKind.FullWindow, PropKinds.Classify("/obj/structure/tram/split", "tram-split-12"));
        Assert.Equal(RenderKind.FullWindow, PropKinds.Classify("/obj/structure/tram/alt/titanium", "tram-part-3"));
        Assert.Equal(RenderKind.Billboard, PropKinds.Classify("/obj/structure/tram/spoiler", "tram-spoiler-retracted"));
        Assert.Null(Catalog.Find("icons/obj/tram/tram_structure.dmi", "tram-part-12"));
    }

    [Theory]
    [InlineData("/obj/machinery/transport/tram_controller")]
    [InlineData("/obj/machinery/transport/destination_sign/split/north")]
    public void TheControllerAndTheSplitSignHangOnTheTramWallTheyShare(string type)
    {
        // the tram wall at (5, 5), open floor to its south
        bool Wall(int x, int y, int z) => x == 5 && y == 5;
        Assert.True(WallMounts.TryPlaceInWall(type, 5, 5, 1, ByondDirConst.South, Wall, (x, y, z) => !Wall(x, y, z), out byte face, out _));
        Assert.Equal(ByondDirConst.South, face);
    }

    [Fact]
    public void TheInformationPlateIsTheOwnersModel()
    {
        ModelEntry plate = Catalog.Find("icons/obj/signs.dmi", "tram_plate")!;
        Assert.Equal("tram_information_plate", plate.Id);
        Assert.True(File.Exists(Path.Combine(Root, plate.Parts["model"].File)));
    }

    [Fact]
    public void TheTramDoorDrawsBothPanesOfEachLeaf()
    {
        ModelEntry door = Catalog.Find("icons/obj/doors/airlocks/tram/tram.dmi", "closed")!;
        Assert.Equal("Left glass 0", door.Parts["model"].Node);
        foreach (string pane in new[] { "Left glass 1", "Right glass 0", "Right glass 1" })
            Assert.Equal(pane, door.Parts[pane].Node);
    }

    [Fact]
    public void TheWashingMachinesGlassSwingsOnItsDoorsHinge()
    {
        ModelEntry washer = Catalog.Find("icons/obj/machines/washing_machine.dmi", "wm_1_0")!;
        ModelPart door = washer.Parts["Round_Door"], glass = washer.Parts["Washer_Glass"];
        Assert.True(glass.Pivot);
        Assert.Equal(new System.Numerics.Vector3(-0.43f, 0.61f, 0.49f), glass.PivotAt);
        Assert.Equal(door.Axis, glass.Axis);
        Assert.Equal(door.Degrees, glass.Degrees);
    }
}
