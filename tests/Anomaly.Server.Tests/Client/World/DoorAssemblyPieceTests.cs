using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M5 slice 6 (D142): an airlock assembly is drawn as the airlock's two panels standing closed across the passage with its own
/// picture (tg's construction frame and overlays), placed by the door rule; it is not a frame the windows join. A door frame
/// taken apart or destroyed is forgotten by the planner's index, as S5's windows and grilles are, so its neighbours let it go.
/// </summary>
public class DoorAssemblyPieceTests
{
    private static PlanEntity Entity(string path, int x, int y)
        => new PlanEntity(1, path, x, y, 1, 2, TableRef.Public(1), 0, 0, LightState.None, null);

    [Fact]
    public void AnAssemblyIsTheAirlocksClosedPanelsAcrossThePassage()
    {
        foreach (string path in new[] { "/obj/structure/door_assembly", "/obj/structure/door_assembly/door_assembly_eng", "/obj/structure/door_assembly/multi_tile/door_assembly_public" })
            Assert.Equal(RenderKind.DoorAssembly, PropKinds.Classify(path));
        Assert.Equal(PieceParts.PartsOf(RenderKind.Airlock), PieceParts.PartsOf(RenderKind.DoorAssembly));
        Assert.False(PieceParts.IsDoor(RenderKind.DoorAssembly), "nothing slides: the panels stay closed");

        // between walls east and west: across the north-south passage, as an airlock there
        CellClass Class(int x, int y, int z) => (x == 4 || x == 6) && y == 5 ? CellClass.Blocking : CellClass.Open;
        var probe = new PlanProbe { IsWall = (x, y, z) => Class(x, y, z) == CellClass.Blocking, Classify = Class };
        Assert.True(ChunkPlanner.TryPlace(Entity("/obj/structure/door_assembly", 5, 5), probe, out Placed assembly));
        Assert.True(ChunkPlanner.TryPlace(Entity("/obj/machinery/door/airlock", 5, 5), probe, out Placed airlock));
        Assert.Equal(PlacedKind.Piece, assembly.Kind);
        Assert.Equal(RenderKind.DoorAssembly, assembly.Shape);
        Assert.Equal(airlock.Face, assembly.Face);
        Assert.NotEmpty(PieceBoxes.For(assembly, 2.0, open: false));

        // the index: an assembly is no door frame
        var index = new PlanIndex();
        index.Add(Entity("/obj/structure/door_assembly", 5, 5));
        Assert.DoesNotContain((5, 5, 1), index.FrameDoors);
        Assert.DoesNotContain((5, 5, 1), index.Doors);
    }

    [Fact]
    public void ADoorFrameTakenApartIsForgotten()
    {
        var index = new PlanIndex();
        index.Add(Entity("/obj/machinery/door/airlock", 5, 5));
        index.DoorFaces[(5, 5, 1)] = 4;
        Assert.Contains((5, 5, 1), index.FrameDoors);
        Assert.Contains((5, 5, 1), index.Doors);

        // a firelock still stands in the cell: still a door across the passage, no longer a frame
        index.Forget((5, 5, 1), windowLeft: false, grilleLeft: false, frameDoorLeft: false, doorLeft: true);
        Assert.DoesNotContain((5, 5, 1), index.FrameDoors);
        Assert.Contains((5, 5, 1), index.Doors);

        index.Forget((5, 5, 1), windowLeft: false, grilleLeft: false, frameDoorLeft: false, doorLeft: false);
        Assert.DoesNotContain((5, 5, 1), index.Doors);
        Assert.False(index.DoorFaces.ContainsKey((5, 5, 1)));

        // S5's call (windows and grilles only) keeps the doors
        index.Add(Entity("/obj/machinery/door/airlock", 6, 5));
        index.Forget((6, 5, 1), windowLeft: false, grilleLeft: false);
        Assert.Contains((6, 5, 1), index.FrameDoors);
    }
}
