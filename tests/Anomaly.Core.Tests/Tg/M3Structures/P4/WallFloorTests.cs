using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.P4;

// M3 P4's smoke tests of the walls and floors subset (task 3): walls.dm, reinf_walls.dm, floor.dm, iron_floor.dm,
// plating.dm, dirtystation.dm and is_station_level.
public class WallFloorTests
{
    [Fact]
    public void WallsAndTheCommonFloorsAreLive()
    {
        foreach (Type t in new[] { typeof(turf_closed_wall), typeof(turf_closed_wall_r_wall), typeof(turf_open_floor), typeof(turf_open_floor_iron), typeof(turf_open_floor_plating), typeof(turf_open_floor_iron_dark) })
            Assert.False(InertSeam.IsInertType(t), t.Name);
    }

    [Fact]
    public void AFloorHasItsAirAndTgsBrokenAndBurntStates()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            var iron = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            var plating = Dm.@new<turf_open_floor_plating>(locate(5, 4, 1));
            Assert.Empty(room.RuntimeText);
            Assert.NotNull(iron.air); // /turf/open/Initialize under /turf/open/floor/Initialize
            Assert.IsType<DmList>(iron.smoothing_groups);
            Assert.Equal(new object[] { "damaged1", "damaged2", "damaged3", "damaged4", "damaged5" }, Dm.each(iron.broken_states() as DmList).Cast<object>().ToArray());
            Assert.Equal(new object[] { "floorscorched1", "floorscorched2" }, Dm.each(iron.burnt_states() as DmList).Cast<object>().ToArray());
            Assert.Equal(new object[] { "damaged1", "damaged2", "damaged4" }, Dm.each(plating.broken_states() as DmList).Cast<object>().ToArray());
            Assert.Null(Waiting.NoSleep(plating.make_plating())); // plating is already plating (plating.dm:155)
            Assert.Equal(typeof(obj_item_stack_tile_iron_base), iron.has_tile()); // floor.dm:176: the floor_tile
            Assert.Null(plating.has_tile());
        }
    }

    [Fact]
    public void ABrokenOrBurntTileHasTgsDamagedOverlay()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            var iron = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            var burnt = Dm.@new<turf_open_floor_iron>(locate(5, 4, 1));
            Assert.Equal(TRUE, iron.break_tile()); // _open.dm:50
            Assert.Equal(TRUE, burnt.burn_tile()); // _open.dm:60
            Assert.Empty(room.RuntimeText);

            // _open.dm:76: mutable_appearance(damaged_dmi, pick(broken_states())), in the turf's overlays
            var broken_states = Dm.each(iron.broken_states() as DmList).Cast<object>().ToHashSet();
            IDmAppearance broken = Dm.each_cast<object>(iron.overlays).OfType<IDmAppearance>()
                .Single(a => Dm.eq(a.icon, iron.damaged_dmi) && broken_states.Contains(a.icon_state));
            Assert.Null(broken.transform); // the iron floor does not smooth: no large-turf translation (_open.dm:78)

            // _open.dm:92: the burnt states when the floor has them
            var burnt_states = Dm.each(burnt.burnt_states() as DmList).Cast<object>().ToHashSet();
            Assert.Single(Dm.each_cast<object>(burnt.overlays).OfType<IDmAppearance>(),
                a => Dm.eq(a.icon, burnt.damaged_dmi) && burnt_states.Contains(a.icon_state));

            // _open.dm:78: a smoothing floor whose damage does not smooth moves the overlay back by the large-turf offset
            iron.smoothing_flags = SMOOTH_BITMASK;
            iron.smooth_broken = FALSE;
            var overlays = Waiting.NoSleep(iron.update_overlays()) as DmList;
            var moved = Dm.each_cast<object>(overlays).OfType<IDmAppearance>().Single(a => Dm.eq(a.icon, iron.damaged_dmi));
            var translation = Assert.IsType<matrix>(moved.transform);
            Assert.Equal(-LARGE_TURF_SMOOTHING_X_OFFSET, translation.c);
            Assert.Equal(-LARGE_TURF_SMOOTHING_Y_OFFSET, translation.f);
        }
    }

    [Fact]
    public void ABrokenFloorKeepsItsPictureWhenItSmooths()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            var iron = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            iron.icon_state = "floor";
            iron.broken = TRUE;
            iron.set_smoothed_icon_state(NORTH_JUNCTION); // /turf/open/floor/set_smoothed_icon_state (icon_smoothing.dm:332)
            Assert.Equal("floor", iron.icon_state);
            iron.broken = FALSE;
            iron.set_smoothed_icon_state(NORTH_JUNCTION);
            Assert.Equal("floor-" + NORTH_JUNCTION, iron.icon_state);
        }
    }

    [Fact]
    public void AReinforcedWallShowsItsDeconstructionState()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            var wall = Dm.@new<turf_closed_wall_r_wall>(locate(4, 4, 1));
            Waiting.NoSleep(SSicon_smooth.Initialize());
            Assert.Equal("reinforced_wall-0", wall.icon_state);
            wall.d_state = SUPPORT_LINES;
            Waiting.NoSleep(wall.update_icon());
            Assert.Equal("r_wall-" + SUPPORT_LINES, wall.icon_state); // reinf_walls.dm:205-208
            Assert.True(Dm.eq(wall.smoothing_flags, NONE)); // :195-197
            wall.d_state = INTACT;
            Waiting.NoSleep(wall.update_icon());
            Assert.True(Dm.eq(wall.smoothing_flags, SMOOTH_BITMASK | SMOOTH_QUEUED)); // :199-202: queued again
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void IsStationLevelCachesTheTraitPerLevel()
    {
        var room = TgToyRoom.Create(5, 5);
        using (room.World.Enter())
        {
            object first = is_station_level(1);
            Assert.True(Dm.eq(GLOB.station_levels_cache.len, 1));
            Assert.True(Dm.eq(GLOB.station_levels_cache[1], first)); // the cache holds the trait's truth value
            Assert.True(Dm.eq(is_station_level(1), first));
            Assert.True(Dm.eq(is_station_level(0), 0));
        }
    }

    [Fact]
    public void MakeDirtyLeavesFloorsThatCannotBeDirtyAndGrilledFloorsAlone()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            var floor = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            Dm.mask(ref floor.flags_1, Dm.bitnot(CAN_BE_DIRTY_1));
            Assert.Null(floor.MakeDirty()); // dirtystation.dm:13-14
            Dm.combine(ref floor.flags_1, CAN_BE_DIRTY_1);
            Dm.@new<obj_structure_grille>(floor);
            Assert.Null(floor.MakeDirty()); // :16-17
            int before = Dm.each_cast<atom>(floor).Count();
            Assert.Equal(before, Dm.each_cast<atom>(floor).Count());
        }
    }

    [Fact]
    public void MakeDirtyIsTheSameForTheSameSeed()
    {
        static string Dirt(ulong seed)
        {
            var room = TgToyRoom.Create(12, 12, seed: seed);
            using (room.World.Enter())
            {
                var floors = new List<turf_open_floor>();
                for (int x = 2; x <= 11; x++)
                    for (int y = 2; y <= 11; y++)
                        floors.Add(Dm.@new<turf_open_floor_iron>(locate(x, y, 1)));
                var log = new System.Text.StringBuilder();
                foreach (turf_open_floor f in floors)
                {
                    f.MakeDirty();
                    foreach (atom a in Dm.each_cast<atom>(f))
                        log.Append(f.x).Append(',').Append(f.y).Append(':').Append(DmTypes.PathOf(a.GetType())).Append(';');
                }
                return log.ToString();
            }
        }
        Assert.Equal(Dirt(5), Dirt(5));
    }
}
