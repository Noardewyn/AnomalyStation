using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// SSspatial_grid (controllers/subsystem/spatial_gridmap.dm) with the client and hearing channels: a player's mob is in the
/// cell of its turf from its Login (enable_client_mobs_in_contents, atoms_movable.dm:1093), moves between cells with it, is
/// carried through a locker's important_recursive_contents, leaves the client channel at Logout
/// (clear_important_client_contents, :1107); hearers() and get_hearers_in_view; the cell tracker.
/// </summary>
public class SpatialGridTests
{
    private static datum_spatial_grid_cell Cell(int x, int y) => (datum_spatial_grid_cell)SSspatial_grid.get_cell_of(locate(x, y, 1));

    [Fact]
    public void TheGridCoversTheHostBuiltRoomInCellsOf17()
    {
        var room = new AudioRoom(40, 8, seed: 5301);
        room.In(() =>
        {
            Assert.Equal(1f, SSspatial_grid.initialized);
            Assert.Equal(3f, SSspatial_grid.cells_on_x_axis); // ROUND_UP(40 / 17)
            Assert.Equal(1f, SSspatial_grid.cells_on_y_axis);
            Assert.Equal(1f, length(SSspatial_grid.grids_by_z_level));
            Assert.Equal(1f, Dm.num(Cell(17, 3).cell_x));
            Assert.Equal(2f, Dm.num(Cell(18, 3).cell_x));
            Assert.Equal(2500f, length(SSspatial_grid.pregenerated_oranges_ears));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void APlayersMobEntersAndLeavesCellsAsItMoves()
    {
        var room = new AudioRoom(40, 8, seed: 5302);
        mob_living_carbon_human dummy = room.Dummy(15, 3);
        mob_living_carbon_human alice = room.Join(16, 4, "Alice");
        room.In(() =>
        {
            Assert.True(Dm.@in(alice, Cell(16, 4).client_contents));
            Assert.True(Dm.@in(alice, Cell(16, 4).hearing_contents)); // every mob is hearing sensitive (mob.dm:100)
            Assert.True(Dm.@in(dummy, Cell(16, 4).hearing_contents));
            Assert.False(Dm.@in(dummy, Cell(16, 4).client_contents));
            Assert.Equal("recursive_contents_client_mobs-recursive_contents_hearing_sensitive", alice.spatial_grid_key);

            Waiting.NoSleep(alice.forceMove(locate(19, 4, 1)));
            Assert.False(Dm.@in(alice, Cell(16, 4).client_contents));
            Assert.True(Dm.@in(alice, Cell(19, 4).client_contents));
            Assert.True(Dm.@in(alice, Cell(19, 4).hearing_contents));
            Assert.Same(SSspatial_grid.dummy_list, Cell(36, 4).client_contents); // an empty cell shares the dummy list
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AClientInALockerIsCarriedByTheLocker()
    {
        var room = new AudioRoom(40, 8, seed: 5303);
        mob_living_carbon_human alice = room.Join(16, 4, "Alice");
        room.In(() =>
        {
            var locker = Dm.@new<obj_structure_closet>(locate(16, 5, 1));
            Waiting.NoSleep(alice.forceMove(locker));
            Assert.True(Dm.@in(alice, ((DmList)locker.important_recursive_contents)[RECURSIVE_CONTENTS_CLIENT_MOBS]));
            Assert.True(Dm.@in(alice, Cell(16, 5).client_contents));

            Waiting.NoSleep(locker.forceMove(locate(20, 5, 1)));
            Assert.False(Dm.@in(alice, Cell(16, 5).client_contents));
            Assert.True(Dm.@in(alice, Cell(20, 5).client_contents));

            Waiting.NoSleep(alice.forceMove(locate(21, 5, 1)));
            Assert.Null(locker.important_recursive_contents);
            Assert.True(Dm.@in(alice, Cell(21, 5).client_contents));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LogoutTakesTheMobOutOfTheClientChannel()
    {
        var room = new AudioRoom(20, 8, seed: 5304);
        mob_living_carbon_human alice = room.Join(5, 4, "Alice");
        client player = alice.client;
        room.Command(() => ClientSeam.Disconnect(player));
        room.Step(2);
        room.In(() =>
        {
            Assert.Null(alice.client);
            Assert.False(Dm.@in(alice, Cell(5, 4).client_contents));
            Assert.True(Dm.@in(alice, Cell(5, 4).hearing_contents));
            Assert.Null(alice.important_recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS]);
            Assert.Equal("recursive_contents_hearing_sensitive", alice.spatial_grid_key);
            Assert.Empty(Dm.each<mob>(get_hearers_in_range(10, locate(5, 4, 1), RECURSIVE_CONTENTS_CLIENT_MOBS)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HearersAreTheMobsThatSeeTheCentre()
    {
        // a wall column at x = 8
        var room = new AudioRoom(16, 12, seed: 5305, mapExtras: _ =>
        {
            for (int y = 2; y <= 10; y++)
                Dm.@new<turf_closed_indestructible>(locate(8, y, 1));
        });
        mob_living_carbon_human near = room.Dummy(6, 6);
        mob_living_carbon_human behind = room.Dummy(11, 6);
        mob_living_carbon_human far = room.Dummy(5, 10);
        room.In(() =>
        {
            DmList heard = hearers(7, locate(5, 6, 1));
            Assert.True(Dm.@in(near, heard));
            Assert.False(Dm.@in(behind, heard)); // in the box, behind the wall
            Assert.True(Dm.@in(far, heard)); // 4 tiles north
            Assert.False(Dm.@in(far, hearers(3, locate(5, 6, 1)))); // outside a box of 3
            Assert.True(Dm.@in(behind, hearers(7, locate(10, 6, 1)))); // on its own side

            // get_hearers_in_view: the grid's candidates narrowed by hearers() through the oranges ears, which go back
            DmList in_view = get_hearers_in_view(7, locate(5, 6, 1)) as DmList;
            Assert.True(Dm.@in(near, in_view));
            Assert.True(Dm.@in(far, in_view));
            Assert.False(Dm.@in(behind, in_view));
            Assert.True(Dm.@in(behind, get_hearers_in_range(7, locate(5, 6, 1)) as DmList));
            foreach (mob_oranges_ear ear in Dm.each_cast<mob_oranges_ear>(SSspatial_grid.pregenerated_oranges_ears))
            {
                Assert.Null(ear.loc);
                Assert.Equal(0f, length(ear.references));
            }
            Assert.Null(locate(6, 6, 1).assigned_oranges_ear);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACellTrackerReportsTheCellsItEntersAndLeaves()
    {
        var room = new AudioRoom(60, 8, seed: 5306);
        mob_living_carbon_human alice = room.Join(40, 4, "Alice");
        room.In(() =>
        {
            var tracker = Dm.@new<datum_cell_tracker>(10, 2);
            Assert.Equal(10f, Dm.num(tracker.inner_window_x_radius));
            Assert.Equal(20f, Dm.num(tracker.outer_window_x_radius));
            var first = (DmList)tracker.recalculate_type_members(locate(5, 4, 1), SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);
            Assert.False(Dm.@in(alice, (DmList)first[1])); // x 5 +- 10 reaches cell 1 only
            var second = (DmList)tracker.recalculate_type_members(locate(30, 4, 1), SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);
            Assert.True(Dm.@in(alice, (DmList)second[1])); // x 20..40: cells 2 and 3, Alice's (x 40) newly joined
            Assert.Equal(0f, length((DmList)second[2])); // cell 1 is inside the outer window (x 10..50): kept
            var third = (DmList)tracker.recalculate_type_members(locate(5, 4, 1), SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);
            Assert.True(Dm.@in(alice, (DmList)third[2])); // outer window x 1..25: cell 3 left
        });
        room.AssertNoRuntimes();
    }
}
