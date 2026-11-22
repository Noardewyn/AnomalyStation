using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M5 slice 7 (D142): what the client draws for the construction states of the slice. A table built on a frame is the table piece
/// (a top on four legs, smoothing by its icon state); the table frame, the rack parts, the pipe fittings, the machine frame (its
/// state is tg's icon state box_0, box_1, box_2), the circuit boards and the wallframes are tg's sprites on billboards (D113's art
/// level for things without a shape of their own), never mistaken for the shape of a type whose path they share a prefix with.
/// </summary>
public class FurniturePieceTests
{
    [Fact]
    public void TheSliceConstructionStatesAreDrawnAsTheirOwnKind()
    {
        Assert.Equal(RenderKind.Table, PropKinds.Classify("/obj/structure/table"));
        Assert.Equal(RenderKind.Table, PropKinds.Classify("/obj/structure/table/glass"));
        foreach (string path in new[]
        {
            "/obj/structure/table_frame", "/obj/structure/table_frame/wood", "/obj/structure/rack", "/obj/item/rack_parts",
            "/obj/item/pipe/quaternary/pipe", "/obj/item/pipe/directional/vent", "/obj/structure/frame/machine",
            "/obj/item/circuitboard/machine/suit_storage_unit", "/obj/item/wallframe/firealarm", "/obj/item/wallframe/airalarm",
            "/obj/item/wallframe/button", "/obj/structure/chair/stool", "/obj/item/chair/stool",
        })
            Assert.Equal(RenderKind.Billboard, PropKinds.Classify(path));
    }
}
