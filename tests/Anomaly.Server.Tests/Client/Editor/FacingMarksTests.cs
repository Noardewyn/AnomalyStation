using Anomaly.Client.Shared.Editor;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>The map editor's arrows (docs/ai/specs/map-editor.md 5.19): which things can get one, and how a way is turned.</summary>
public sealed class FacingMarksTests
{
    [Theory]
    [InlineData("/obj/structure/chair", true)]
    [InlineData("/obj/structure/closet/crate", true)]
    [InlineData("/obj/machinery/vending/cola", true)]
    [InlineData("/obj/machinery/door/window/left", true)]
    [InlineData("/mob/living/basic/mouse", true)]
    [InlineData("/obj/vehicle/ridden/janicart", true)]
    [InlineData("/obj/effect/spawner/random/vending/snackvend", true)]
    [InlineData("/obj/effect/landmark/start/assistant", true)]
    // a run's dir is its shape, a wall's thing's its wall, and what fills its tile has none to see
    [InlineData("/obj/structure/cable", false)]
    [InlineData("/obj/structure/cable/layer1", false)]
    [InlineData("/obj/structure/disposalpipe/segment", false)]
    [InlineData("/obj/machinery/atmospherics/pipe/smart/simple/general/visible", false)]
    [InlineData("/obj/structure/table/wood", false)]
    [InlineData("/obj/structure/lattice/catwalk", false)]
    [InlineData("/obj/structure/window/reinforced", false)]
    [InlineData("/obj/machinery/door/airlock/command", false)]
    [InlineData("/obj/machinery/light/directional/north", false)]
    [InlineData("/obj/structure/sign/poster/random", false)]
    // neither furniture nor a marker
    [InlineData("/obj/item/wrench", false)]
    [InlineData("/obj/effect/decal/cleanable/dirt", false)]
    [InlineData("/turf/open/floor/iron", false)]
    [InlineData("/obj/structures", false)]
    [InlineData(null, false)]
    public void FurnitureMachinesMobsAndMarkersCanGetAnArrow(string path, bool wanted) => Assert.Equal(wanted, FacingMarks.Wanted(path));

    [Fact]
    public void AMarkersArrowIsShownWhateverItsPicture()
    {
        Assert.True(FacingMarks.IsMarker("/obj/effect/spawner/random/structure/crate"));
        Assert.True(FacingMarks.IsMarker("/obj/effect/landmark/start"));
        Assert.False(FacingMarks.IsMarker("/obj/structure/chair"));
        Assert.False(FacingMarks.IsMarker(null));
    }

    [Fact]
    public void AnArrowTurnsFromNorthToItsWay()
    {
        // counter-clockwise seen from above: west is a quarter that way, east a quarter the other
        Assert.Equal((0f, 180f, -90f, 90f), (FacingMarks.Degrees(1), FacingMarks.Degrees(2), FacingMarks.Degrees(4), FacingMarks.Degrees(8)));
        Assert.Equal((-45f, 45f, -135f, 135f), (FacingMarks.Degrees(5), FacingMarks.Degrees(9), FacingMarks.Degrees(6), FacingMarks.Degrees(10)));
        Assert.Equal(180f, FacingMarks.Degrees(0)); // (no dir: south, as tg's atoms)
    }
}
