using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>Listens to a turf's COMSIG_TURF_CHANGE and adds a post-change callback (change_turf.dm:94-95, :107-108).</summary>
[DmPath("/datum/t2_turf_listener")]
public class datum_t2_turf_listener : datum
{
    [NotHashed] public readonly List<string> Seen = new();
    [NotHashed] public object ChangedTurf;

    [SignalHandler]
    public virtual object on_turf_change(datum source = null, object path = null, object new_baseturfs = null, object flags = null, object post_change_callbacks = null)
    {
        Seen.Add(Dm.text("change []->[] flags [] new_baseturfs []", DmTypes.PathOf(source.GetType()), DmTypes.PathOf((Type)path), flags, new_baseturfs));
        ((DmList)post_change_callbacks).Add(Dm.@new<datum_callback>(this, nameof(after_change)));
        return NONE;
    }

    public virtual void after_change(object new_turf = null)
    {
        ChangedTurf = new_turf;
        Seen.Add("callback with " + DmTypes.PathOf(((turf)new_turf).GetType()));
    }
}

/// <summary>
/// M3 T2: ChangeTurf and ScrapeAway against tg's DM (code/game/turfs/change_turf.dm, baseturfs.dm, turf.dm
/// assemble_baseturfs, __HELPERS/string_lists.dm baseturfs_string_list). Fixture: tests/fixtures/maps/t2/turfs.dmm, 8 x 4.
/// <code>
/// y4:  .  iron  plating  r_wall  wall  space(hall)  iron(nearstation)  .
/// y3:  .  iron+crowbar+start landmark  indestructible  lattice+space(nearstation)  .  iron  iron  .
/// </code>
/// </summary>
public class ChangeTurfTests
{
    private static readonly Type Plating = typeof(turf_open_floor_plating);
    private static readonly Type Bottom = typeof(turf_baseturf_bottom);

    private static object Change(turf t, object path, object new_baseturfs = null, object flags = null)
        => Waiting.NoSleep(t.dm_change_turf_virtual(path, new_baseturfs, flags)); // tg's ChangeTurf, its overrides included

    private static object Scrape(turf t, float? amount = null, object flags = null) => Waiting.NoSleep(t.ScrapeAway(amount, flags));

    private static void AssertStack(object baseturfs, params Type[] bottomToTop)
    {
        if (bottomToTop.Length == 1)
        {
            Assert.Equal(bottomToTop[0], baseturfs); // a single type
            return;
        }
        var list = Assert.IsType<DmList>(baseturfs);
        Assert.Equal(bottomToTop, list.Items().Cast<Type>().ToArray());
    }

    // ---- the stacks assemble_baseturfs builds at Initialize (turf.dm:469-515)

    [Fact]
    public void The_loaded_turfs_have_the_baseturfs_stacks_tg_assembles_from_the_initial_chain()
    {
        World world = T2Maps.Boot("turfs", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            // iron: baseturfs = plating (floor.dm:6), plating: /turf/baseturf_bottom (plating.dm:12) -> list(bottom, plating)
            AssertStack(locate(2, 4, 1).baseturfs, Bottom, Plating);
            // plating, indestructible floors and walls over the bottom: bottom alone is a single type (current == next target)
            AssertStack(locate(3, 4, 1).baseturfs, Bottom);
            AssertStack(locate(3, 3, 1).baseturfs, Bottom);
            // a wall: baseturfs = plating (walls.dm:13)
            AssertStack(locate(5, 4, 1).baseturfs, Bottom, Plating);
            AssertStack(locate(4, 4, 1).baseturfs, Bottom, Plating);
            // the space tiles the load skipped (the space key) keep BYOND's world.turf: they were never initialised by new
            // here, SSatoms initialised them; their baseturfs is the bottom marker
            AssertStack(locate(1, 1, 1).baseturfs, Bottom);
        }
    }

    [Fact]
    public void Equal_stacks_share_one_cached_list_as_baseturfs_string_list_does()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // tg: string_list caches a list by its Join text, so the iron floors (and the walls) hold the very same list
            Assert.Same(locate(2, 4, 1).baseturfs, locate(6, 3, 1).baseturfs);
            Assert.Same(locate(2, 4, 1).baseturfs, locate(7, 3, 1).baseturfs);
            Assert.Same(locate(2, 4, 1).baseturfs, locate(5, 4, 1).baseturfs);
            Assert.Same(locate(2, 4, 1).baseturfs, GLOB.string_lists[Dm.text("[]-[]", Bottom, Plating)]);
        }
    }

    // ---- ChangeTurf (change_turf.dm:56-204)

    [Fact]
    public void ChangeTurf_null_does_nothing_and_a_matching_type_and_stack_is_a_no_op()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf iron = locate(2, 4, 1);
            Assert.Null(Change(iron, null));
            Assert.Same(iron, locate(2, 4, 1));
            // `path == type && baseturfs == new_baseturfs` (line 72): the same type with the very same stack list
            Assert.Same(iron, Change(iron, iron.type, iron.baseturfs));
            // new_baseturfs = null does not equal the assembled list, so the turf IS replaced (a tg quirk: callers that
            // want the no-op pass the stack, ScrapeAway/place_on_top always do)
            object replaced = Change(iron, iron.type);
            Assert.NotSame(iron, replaced);
            Assert.IsType<turf_open_floor_iron>(replaced);
            Assert.Same(replaced, locate(2, 4, 1));
        }
    }

    [Fact]
    public void CHANGETURF_FORCEOP_replaces_even_when_type_and_stack_match()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf iron = locate(2, 4, 1);
            object forced = Change(iron, iron.type, iron.baseturfs, CHANGETURF_FORCEOP);
            Assert.NotSame(iron, forced);
            Assert.Same(forced, locate(2, 4, 1));
        }
    }

    [Fact]
    public void Space_basic_becomes_space_and_the_bottom_marker_becomes_the_levels_baseturf()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // line 66: /turf/open/space/basic -> /turf/open/space ("basic doesn't initialize")
            turf wall = locate(5, 4, 1);
            object space = Change(wall, typeof(turf_open_space_basic));
            Assert.IsType<turf_open_space>(space);
            // line 58-65: /turf/baseturf_bottom -> the z-level's Baseturf trait, else /turf/open/space (this level has none)
            turf plating = locate(3, 4, 1);
            object bottom = Change(plating, Bottom);
            Assert.IsType<turf_open_space>(bottom);
        }
    }

    [Fact]
    public void The_new_turf_takes_the_cell_the_area_and_every_movable_in_order()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf old = locate(2, 3, 1);
            var area_before = (area)old.loc;
            var before = old.contents.Items().ToArray();
            Assert.Equal(2, before.Length);
            var wall = (turf)Change(old, typeof(turf_closed_wall));
            Assert.IsType<turf_closed_wall>(wall);
            Assert.Same(wall, locate(2, 3, 1));
            Assert.Equal((2f, 3f, 1f), (wall.x, wall.y, wall.z));
            Assert.Same(area_before, wall.loc);
            Assert.Equal(before, wall.contents.Items().ToArray());
            Assert.All(before, o => Assert.Same(wall, ((atom)o).loc));
            // the area lists the new turf in place of the old one (contents), and the old one is gone from the map
            Assert.Contains(wall, area_before.contents.Items());
            Assert.DoesNotContain(old, area_before.contents.Items());
            Assert.True(QDELETED(old));
            Assert.Equal(0f, old.x);
            // the new turf is initialised (SSatoms is past its init: the Initialize ran inside `new`, mapload false)
            Assert.True(Dm.truthy(Dm.bitand(wall.flags_1, INITIALIZED_1)));
            Assert.False(QDELETED(wall));
        }
    }

    [Fact]
    public void The_stack_of_the_old_turf_goes_to_the_new_one_unless_new_baseturfs_are_given()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // lines 109-112: no new_baseturfs -> baseturfs_string_list(old_baseturfs, new_turf)
            var wall = (turf)Change(locate(2, 4, 1), typeof(turf_closed_wall));
            AssertStack(wall.baseturfs, Bottom, Plating);
            Assert.Same(locate(6, 3, 1).baseturfs, wall.baseturfs); // still the cached list
            // a given stack is used as it is (a list is cached by text, a single type is kept)
            var stack = Dm.list(Bottom, Plating, typeof(turf_open_floor_iron));
            var floor = (turf)Change(locate(6, 3, 1), typeof(turf_open_indestructible), stack);
            AssertStack(floor.baseturfs, Bottom, Plating, typeof(turf_open_floor_iron));
            var single = (turf)Change(locate(7, 3, 1), typeof(turf_closed_wall), Plating);
            Assert.Equal(Plating, single.baseturfs);
        }
    }

    [Fact]
    public void CHANGETURF_SKIP_is_a_plain_new_and_CHANGETURF_DEFER_CHANGE_skips_AfterChange()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // line 74: `new path(src)` without any of the carried-over state; the new turf still initialises itself
            turf plating = locate(3, 4, 1);
            object skipped = Change(plating, typeof(turf_open_floor_iron), null, CHANGETURF_SKIP);
            Assert.IsType<turf_open_floor_iron>(skipped);
            Assert.Same(skipped, locate(3, 4, 1));
            Assert.True(Dm.truthy(Dm.bitand(((turf)skipped).flags_1, INITIALIZED_1)));
            AssertStack(((turf)skipped).baseturfs, Bottom, Plating); // assemble_baseturfs at Initialize, not the old turf's stack

            // AfterChange of a /turf/open runs RemoveLattice (change_turf.dm:249-252): the lattice of (4,3) goes with a plain
            // ChangeTurf to a floor, and stays with CHANGETURF_DEFER_CHANGE
            turf with_lattice = locate(4, 3, 1);
            var lattice = Assert.IsType<obj_structure_lattice>(Assert.Single(with_lattice.contents.Items()));
            var kept = (turf)Change(with_lattice, typeof(turf_open_floor_plating), null, CHANGETURF_DEFER_CHANGE);
            Assert.IsType<turf_open_floor_plating>(kept);
            Assert.False(QDELETED(lattice));
            Assert.Same(kept, lattice.loc);
        }
    }

    [Fact]
    public void AfterChange_removes_the_lattice_of_a_turf_that_became_a_floor()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf with_lattice = locate(4, 3, 1);
            var lattice = Assert.IsType<obj_structure_lattice>(Assert.Single(with_lattice.contents.Items()));
            var floor = (turf)Change(with_lattice, typeof(turf_open_floor_plating));
            Assert.True(QDELETED(lattice)); // RemoveLattice (turf.dm:527-531)
            Assert.Empty(floor.contents.Items());
        }
    }

    [Fact]
    public void The_turfs_own_state_is_carried_over_the_replacement()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf old = locate(6, 3, 1);
            old.lattice_underneath = FALSE;
            old.rcd_memory = 3f;
            var blueprint = Dm.list("bp");
            old.blueprint_data = blueprint;
            var wall = (turf)Change(old, typeof(turf_closed_wall));
            Assert.Equal(FALSE, wall.lattice_underneath); // line 149
            Assert.True(Dm.eq(wall.rcd_memory, 3f)); // line 139
            Assert.Same(blueprint, wall.blueprint_data); // lines 83-84, 138
        }
    }

    [Fact]
    public void The_turf_change_signal_is_sent_first_and_its_callbacks_get_the_new_turf()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf iron = locate(2, 4, 1);
            var listener = Dm.@new<datum_t2_turf_listener>();
            listener.RegisterSignal(iron, COMSIG_TURF_CHANGE, nameof(datum_t2_turf_listener.on_turf_change));
            var wall = (turf)Change(iron, typeof(turf_closed_wall), null, CHANGETURF_DEFER_CHANGE);
            Assert.Equal(2, listener.Seen.Count);
            // flags 33: /turf/open/ChangeTurf (change_turf.dm:228) adds CHANGETURF_RECALC_ADJACENT for a closed turf before ..()
            Assert.Equal("change /turf/open/floor/iron->/turf/closed/wall flags 33 new_baseturfs ", listener.Seen[0]); // null prints as nothing
            Assert.Equal("callback with /turf/closed/wall", listener.Seen[1]);
            Assert.Same(wall, listener.ChangedTurf);
        }
    }

    // tg: space.dm:200-210 /turf/open/space/ChangeTurf: a space turf in /area/space (not /area/space/nearstation) that
    // becomes anything but space moves into the nearstation area, unless CHANGETURF_NO_AREA_CHANGE.
    [Fact] // M5 slice 4: space.dm:200 and set_turf_to_area (areas.dm:87) translated
    public void Building_on_a_space_turf_moves_it_from_the_space_area_into_the_nearstation_area()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            var space = (area)GLOB.areas_by_type[typeof(area_space)];
            var nearstation = (area)GLOB.areas_by_type[typeof(area_space_nearstation)];
            turf bare = locate(1, 1, 1);
            Assert.Same(space, bare.loc);
            var floor = (turf)Change(bare, typeof(turf_open_floor_plating));
            Assert.Same(nearstation, floor.loc);
            Assert.Contains(floor, nearstation.contents.Items());
            Assert.DoesNotContain(floor, space.contents.Items());

            // CHANGETURF_NO_AREA_CHANGE ("Prevents turfs like space from autoadjusting their area")
            var kept = (turf)Change(locate(8, 1, 1), typeof(turf_open_floor_plating), null, CHANGETURF_NO_AREA_CHANGE);
            Assert.Same(space, kept.loc);

            // a space turf that is not in /area/space is left alone, and so is a change into space
            var in_hall = (turf)Change(locate(6, 4, 1), typeof(turf_open_floor_plating));
            Assert.IsType<area_station_hallway_primary_central>(in_hall.loc);
            var to_space = (turf)Change(locate(2, 1, 1), typeof(turf_open_space));
            Assert.Same(space, to_space.loc);
        }
    }

    // ---- ScrapeAway (baseturfs.dm:2-20)

    [Fact]
    public void ScrapeAway_walks_the_stack_down_and_the_bottom_never_goes_away()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // an iron floor over plating over the level's space: stack list(bottom, plating)
            var plating = (turf)Scrape(locate(2, 4, 1));
            Assert.IsType<turf_open_floor_plating>(plating);
            Assert.Same(plating, locate(2, 4, 1));
            // `new_baseturfs.len -= min(amount, len - 1)`, then a one-item list becomes its item (lines 14-16)
            Assert.Equal(Bottom, plating.baseturfs);
            // plating has a single-type baseturfs: ChangeTurf(baseturfs, baseturfs) (line 22): the bottom marker is space here
            var space = (turf)Scrape(plating);
            Assert.IsType<turf_open_space>(space);
            // space: baseturfs is the marker, ChangeTurf(bottom -> space, bottom): the same type and stack, a no-op
            Assert.Same(space, Scrape(space));
            Assert.Equal(Bottom, space.baseturfs);
        }
    }

    [Fact]
    public void ScrapeAway_by_two_and_by_zero_and_the_wall_stack()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            Assert.Null(Scrape(locate(2, 4, 1), 0)); // line 3: `if(!amount) return`
            Assert.IsType<turf_open_floor_iron>(locate(2, 4, 1));
            // amount 2 on list(bottom, plating): turf_type = list[max(1, 2-2+1)] = bottom, resolved to space by ChangeTurf
            Assert.IsType<turf_open_space>(Scrape(locate(2, 4, 1), 2));
            // a wall scrapes to plating first (walls.dm:13 baseturfs = plating)
            Assert.IsType<turf_open_floor_plating>(Scrape(locate(5, 4, 1)));
            Assert.IsType<turf_open_floor_plating>(Scrape(locate(4, 4, 1)));
        }
    }

    [Fact]
    public void ScrapeAway_skips_a_baseturf_skipover_and_crashes_when_the_bottom_is_one()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // list(bottom, plating, skipover): amount 1 hits the skipover (line 8), amount becomes 2 and lands on plating
            var stacked = (turf)Change(locate(6, 3, 1), typeof(turf_open_floor_iron), Dm.list(Bottom, Plating, typeof(turf_baseturf_skipover)));
            Assert.IsType<turf_open_floor_plating>(Scrape(stacked));
            // list(skipover, plating): scraping two walks past the bottom skipover: CRASH (line 11)
            var broken = (turf)Change(locate(7, 3, 1), typeof(turf_open_floor_iron), Dm.list(typeof(turf_baseturf_skipover), Plating));
            var crash = Assert.ThrowsAny<Exception>(() => Scrape(broken, 2));
            Assert.Contains("bottommost baseturf of a turf is a skipover", crash.Message);
        }
    }

    // ---- TURF-IDENTITY (object-model.md 7.1)

    [Fact]
    public void A_caller_that_keeps_using_the_old_turf_reference_after_the_new_reaches_the_new_turf()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            // attempt_lattice_replacement (change_turf.dm:262-283): src is used after ScrapeAway replaced it; the new lattice
            // goes onto the turf the scrape left. (4,3) is a space turf with a lattice_underneath in tg's default.
            turf iron = locate(6, 3, 1);
            Assert.True(Dm.truthy(iron.lattice_underneath));
            Waiting.NoSleep(iron.attempt_lattice_replacement()); // amount 2: iron -> plating -> space, then the lattice over the space
            turf left = locate(6, 3, 1);
            Assert.IsType<turf_open_space>(left);
            var lattice = Assert.IsType<obj_structure_lattice>(Assert.Single(left.contents.Items()));
            Assert.Same(left, lattice.loc);
        }
    }

    [Fact]
    public void Every_movable_of_a_loaded_map_stands_on_the_turf_the_cell_holds_and_no_area_list_holds_a_replaced_turf()
    {
        foreach (string name in new[] { "turfs", "areas", "preload" })
        {
            World world = T2Maps.Boot(name);
            using (world.Enter())
            {
                for (int y = 1; y <= Dm.world.maxy; y++)
                {
                    for (int x = 1; x <= Dm.world.maxx; x++)
                    {
                        turf t = locate(x, y, 1);
                        Assert.False(QDELETED(t), $"{name}: the turf at ({x},{y}) is deleted");
                        Assert.Equal((float)x, t.x);
                        Assert.Equal((float)y, t.y);
                        foreach (atom_movable m in t.contents.Items())
                            Assert.Same(t, m.loc);
                        Assert.Contains(t, ((area)t.loc).contents.Items());
                    }
                }
                foreach (area a in GLOB.areas.Items().Cast<area>())
                {
                    foreach (turf listed in ((DmList)a.turfs_by_zlevel[1f]).Items().Cast<turf>())
                        Assert.Same(listed, locate((int)listed.x, (int)listed.y, 1));
                }
            }
        }
    }

    [Fact] // the M5 obligation (object-model.md 7.1): met by dm_replace_listed_turf (change_turf.cs)
    public void An_area_turf_list_holds_the_new_turf_after_a_runtime_ChangeTurf()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf iron = locate(2, 4, 1);
            var hall = (area)iron.loc;
            var wall = (turf)Change(iron, typeof(turf_closed_wall));
            var listed = ((DmList)hall.turfs_by_zlevel[1f]).Items().Cast<turf>().ToList();
            Assert.Contains(wall, listed);
            Assert.DoesNotContain(iron, listed);
        }
    }
}
