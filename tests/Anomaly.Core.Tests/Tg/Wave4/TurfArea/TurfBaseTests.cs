using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// The rest of /turf (turf.dm), /turf/open (open/_open.dm), /turf/closed (closed/_closed.dm) and the indestructible types:
/// Destroy, the z-pass and distance procs, density and explosion blocks, is_blocked_turf, wash, and the type defaults, from
/// the DM.
/// </summary>
public class TurfBaseTests
{
    private static List<string> Lines(TurfAreaWorld t) => TurfAreaTrace.Of(t.World).Lines;

    private static datum_test_turfarea_listener Listener(string label, object returns = null)
    {
        var l = Dm.@new<datum_test_turfarea_listener>();
        l.label = label;
        if (returns != null)
            l.returns = returns;
        return l;
    }

    // ---- Destroy (turf.dm:206-239) ----

    [Fact]
    public void DestroyWithoutChangingTurfIsAStackTraceAndStillResetsTheTurf()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            floor.Initialize(TRUE);
            floor.blueprint_data = Dm.list(Dm.@new<obj_test_turfarea_sitter>());
            floor.requires_activation = TRUE;
            var seen = Dm.@new<obj_test_turfarea_sitter>();
            Dm.append(floor.vis_contents, seen);

            object hint = floor.Destroy();

            Assert.True(Dm.eq(hint, QDEL_HINT_IWILLGC));   // turfs are never garbage collected
            Assert.Single(t.Runtimes);
            Assert.Contains("Incorrect turf deletion", t.Runtimes[0].Message);
            Assert.False(Dm.truthy(Dm.bitand(floor.flags_1, INITIALIZED_1)));   // turf.dm:230
            Assert.False(Dm.truthy(floor.requires_activation));
            Assert.Equal(0, floor.blueprint_data.len);                           // LAZYCLEARLIST: cut in place
            Assert.Equal(0, floor.vis_contents.len);                             // turf.dm:234-235
        }
    }

    [Fact]
    public void DestroyDuringAChangeOfTurfIsSilentAndResetsTheFlag()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            floor.changing_turf = TRUE;
            floor.Destroy();
            Assert.True(Dm.eq(floor.changing_turf, FALSE));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DestroyTellsTheTurfsAboveAndBelowOnlyOnLevelsThatAreNotTheLowestOfTheirStack()
    {
        var t = new TurfAreaWorld(1, 1, 3);
        using (t.World.Enter())
        {
            SSmapping.max_plane_offset = 2;
            SSmapping.z_level_to_plane_offset = Dm.list(0, 1, 2);
            SSmapping.z_level_to_lowest_plane_offset = Dm.list(0, 0, 0);   // one stack: every level's lowest is 0 ...
            SSmapping.multiz_levels = Dm.list(Dm.list(TRUE, FALSE), Dm.list(TRUE, TRUE), Dm.list(FALSE, TRUE));
            turf z1 = locate(1, 1, 1), z2 = locate(1, 1, 2), z3 = locate(1, 1, 3);
            var watch = Listener("multiz_del");
            watch.RegisterSignal(z1, COMSIG_TURF_MULTIZ_DEL, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(z3, COMSIG_TURF_MULTIZ_DEL, nameof(datum_test_turfarea_listener.on_any));
            z2.changing_turf = TRUE;

            z2.Destroy();
            Assert.Empty(Lines(t));   // `if(GET_LOWEST_STACK_OFFSET(z))` is 0 here (turf.dm:212)

            SSmapping.z_level_to_lowest_plane_offset = Dm.list(0, 1, 0);   // ... now level 2 is above the lowest of its stack
            z2.changing_turf = TRUE;
            z2.Destroy();

            Assert.Equal(new[]
            {
                "multiz_del turf(1,1,3)(turf(1,1,2)," + Dm.str(DOWN) + ",null,null)",
                "multiz_del turf(1,1,1)(turf(1,1,2)," + Dm.str(UP) + ",null,null)",
            }, Lines(t));
        }
    }

    [Fact]
    public void ForcedDestroyWipesTheTurfToTheWorldTurfAndDeletesWhatWasOnIt()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var inside = Dm.@new<obj_test_turfarea_sitter>(wall);
            var other = Dm.@new<obj_test_turfarea_sitter>(wall);
            wall.changing_turf = TRUE;

            object hint = wall.Destroy(TRUE);

            Assert.True(Dm.eq(hint, QDEL_HINT_IWILLGC));
            // `new world.turf(src)` (turf.dm:223): the world's turf type takes the cell, in the same area
            var wiped = locate(2, 1, 1);
            Assert.IsType<turf_open_indestructible>(wiped);
            Assert.NotSame(wall, wiped);
            Assert.True(QDELETED(inside));
            Assert.True(QDELETED(other));
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- type defaults ----

    [Fact]
    public void ABareTurfHasTgsDefaults()
    {
        // turf.dm:4-114 and LINDA_system.dm:15 (the layer of a bare /turf is BYOND's own, an open question of rules 10.2)
        object V(string name) => Dm.initial(typeof(turf), name);
        Assert.True(Dm.eq(V("datum_flags"), DF_STATIC_OBJECT));
        Assert.True(Dm.eq(V("vis_flags"), VIS_INHERIT_ID));
        Assert.True(Dm.eq(V("luminosity"), 1));
        Assert.True(Dm.eq(V("light_height"), LIGHTING_HEIGHT_FLOOR));
        Assert.True(Dm.eq(V("temperature"), T20C));
        Assert.True(Dm.eq(V("lattice_underneath"), TRUE));
        Assert.True(Dm.eq(V("init_air"), TRUE));
        Assert.True(Dm.eq(V("blocks_air"), FALSE));
        Assert.True(Dm.eq(V("inherent_explosive_resistance"), -1));
        Assert.True(Dm.eq(V("pathing_pass_method"), TURF_PATHING_PASS_DENSITY));
        Assert.True(Dm.eq(V("can_atmos_pass"), ATMOS_PASS_NO));
        Assert.True(Dm.eq(V("underfloor_accessibility"), UNDERFLOOR_HIDDEN));
        Assert.True(Dm.eq(V("rust_resistance"), RUST_RESISTANCE_BASIC));
        Assert.True(Dm.eq(V("thermal_conductivity"), 0.05f));
        Assert.True(Dm.eq(V("heat_capacity"), INFINITY));
        Assert.Equal(OPENTURF_DEFAULT_ATMOS, V("initial_gas_mix"));
        Assert.Same(typeof(turf_baseturf_bottom), V("baseturfs"));
        Assert.Same(typeof(turf), V("abstract_type"));
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open), "can_atmos_pass"), ATMOS_PASS_PROC));   // /turf/open (LINDA_system.dm:18)
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open), "plane"), FLOOR_PLANE));
    }

    [Fact]
    public void OpenTurfsLieOnTheFloorPlaneAndClosedOnesAreSolidOpaqueAirtightWalls()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            Assert.True(Dm.eq(open.layer, LOW_FLOOR_LAYER));
            Assert.True(Dm.eq(open.plane, FLOOR_PLANE));
            Assert.Same(typeof(turf_open), open.abstract_type);

            Assert.True(Dm.eq(wall.layer, CLOSED_TURF_LAYER));
            Assert.True(Dm.eq(wall.plane, WALL_PLANE));
            Assert.Same(typeof(turf_closed), wall.abstract_type);
            Assert.True(Dm.eq(wall.turf_flags, Dm.bitor(IS_SOLID, NO_RUST)));
            Assert.True(Dm.truthy(wall.opacity));
            Assert.True(Dm.truthy(wall.density));
            Assert.True(Dm.truthy(wall.blocks_air));
            Assert.True(Dm.eq(wall.init_air, FALSE));
            Assert.True(Dm.eq(wall.pass_flags_self, PASSCLOSEDTURF));
            Assert.True(Dm.eq(wall.rad_insulation, RAD_MEDIUM_INSULATION));
            Assert.True(Dm.eq(wall.explosive_resistance, 50));
            Assert.True(Dm.eq(wall.rust_resistance, RUST_RESISTANCE_ABSOLUTE));
            Assert.Equal("wall", wall.name);
            Assert.Same(typeof(turf_open_indestructible_plating), wall.baseturfs);
        }
    }

    [Fact]
    public void AnIndestructibleTurfIgnoresMelting()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var floor = (turf_open_indestructible)locate(1, 1, 1);
            wall.to_be_destroyed = 1;
            floor.to_be_destroyed = 1;
            // /turf/closed/indestructible/Melt (indestructible.dm:16) and /turf/open/indestructible/Melt (_open.dm:297)
            Assert.Same(wall, Waiting.NoSleep(wall.Melt()));
            Assert.Same(floor, Waiting.NoSleep(floor.Melt()));
            Assert.True(Dm.eq(wall.to_be_destroyed, FALSE));
            Assert.True(Dm.eq(floor.to_be_destroyed, FALSE));
        }
    }

    // What /turf/open/indestructible and its subtypes are in the DM (_open.dm:285-495): each type's own var lines, inherited
    // down the path. The kitchen, rockyground and stone floors are not declared (their defaults read M3 floor types).
    private static readonly (Type type, Type parent, (string var, object value)[] vars)[] FloorTable =
    {
        (typeof(turf_open_indestructible), null, new (string, object)[]
        {
            ("name", "floor"), ("desc", "The floor you walk on. It looks near-impervious to damage."), ("icon_state", "floor"),
            ("footstep", FOOTSTEP_FLOOR), ("barefootstep", FOOTSTEP_HARD_BAREFOOT), ("clawfootstep", FOOTSTEP_HARD_CLAW),
            ("heavyfootstep", FOOTSTEP_GENERIC_HEAVY), ("tiled_turf", TRUE),
        }),
        (typeof(turf_open_indestructible_large), typeof(turf_open_indestructible), new (string, object)[] { ("icon_state", "floor_large") }),
        (typeof(turf_open_indestructible_white), typeof(turf_open_indestructible), new (string, object)[] { ("icon_state", "white") }),
        (typeof(turf_open_indestructible_white_smooth_large), typeof(turf_open_indestructible_white), new (string, object)[] { ("icon_state", "white_large") }),
        (typeof(turf_open_indestructible_white_textured), typeof(turf_open_indestructible_white), new (string, object)[] { ("icon_state", "textured_white") }),
        (typeof(turf_open_indestructible_dark), typeof(turf_open_indestructible), new (string, object)[] { ("icon_state", "darkfull") }),
        (typeof(turf_open_indestructible_dark_textured), typeof(turf_open_indestructible_dark), new (string, object)[] { ("icon_state", "textured_dark") }),
        (typeof(turf_open_indestructible_dark_smooth_large), typeof(turf_open_indestructible_dark), new (string, object)[] { ("icon_state", "dark_large") }),
        (typeof(turf_open_indestructible_light), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("icon_state", "light_on-1"), ("light_range", 3), ("light_color", LIGHT_COLOR_CYAN), ("light_on", TRUE),
        }),
        (typeof(turf_open_indestructible_permalube), typeof(turf_open_indestructible), new (string, object)[] { ("icon_state", "darkfull") }),
        (typeof(turf_open_indestructible_honk), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "bananium floor"), ("icon_state", "bananium"), ("footstep", null), ("barefootstep", null),
            ("clawfootstep", null), ("heavyfootstep", null),
        }),
        (typeof(turf_open_indestructible_necropolis), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "necropolis floor"), ("desc", "It's regarding you suspiciously."), ("icon_state", "necro1"),
            ("baseturfs", typeof(turf_open_indestructible_necropolis)), ("initial_gas_mix", LAVALAND_DEFAULT_ATMOS),
            ("footstep", FOOTSTEP_LAVA), ("barefootstep", FOOTSTEP_LAVA), ("clawfootstep", FOOTSTEP_LAVA),
            ("heavyfootstep", FOOTSTEP_LAVA), ("tiled_turf", FALSE),
        }),
        (typeof(turf_open_indestructible_necropolis_air), typeof(turf_open_indestructible_necropolis), new (string, object)[] { ("initial_gas_mix", OPENTURF_DEFAULT_ATMOS) }),
        (typeof(turf_open_indestructible_boss), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "necropolis floor"), ("icon_state", "boss"), ("baseturfs", typeof(turf_open_indestructible_boss)),
            ("planetary_atmos", TRUE), ("initial_gas_mix", LAVALAND_DEFAULT_ATMOS),
        }),
        (typeof(turf_open_indestructible_boss_air), typeof(turf_open_indestructible_boss), new (string, object)[] { ("initial_gas_mix", OPENTURF_DEFAULT_ATMOS) }),
        (typeof(turf_open_indestructible_hierophant), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "palestone floor"), ("desc", "A tiled floor made out of an odd pale stone."), ("icon_state", "hierophant_floor-255"),
            ("base_icon_state", "hierophant_floor"), ("planetary_atmos", TRUE), ("initial_gas_mix", LAVALAND_DEFAULT_ATMOS),
            ("baseturfs", typeof(turf_open_indestructible_hierophant)), ("smoothing_flags", SMOOTH_BITMASK),
        }),
        (typeof(turf_open_indestructible_hierophant_two), typeof(turf_open_indestructible_hierophant), new (string, object)[]
        {
            ("name", "runic palestone floor"), ("desc", "A tiled floor made out of an odd pale stone, inscribed with odd runes."),
            ("icon_state", "hierophant_floor_alt-255"), ("base_icon_state", "hierophant_floor_alt"),
        }),
        (typeof(turf_open_indestructible_paper), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "notebook floor"), ("desc", "A floor made of invulnerable notebook paper."), ("icon_state", "paperfloor"),
            ("footstep", null), ("barefootstep", null), ("clawfootstep", null), ("heavyfootstep", null), ("tiled_turf", FALSE),
        }),
        (typeof(turf_open_indestructible_binary), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "tear in the fabric of reality"), ("can_atmos_pass", ATMOS_PASS_NO), ("baseturfs", typeof(turf_open_indestructible_binary)),
            ("icon_state", "binary"), ("footstep", null), ("barefootstep", null), ("clawfootstep", null), ("heavyfootstep", null),
        }),
        (typeof(turf_open_indestructible_airblock), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("icon_state", "bluespace"), ("blocks_air", TRUE), ("init_air", FALSE), ("baseturfs", typeof(turf_open_indestructible_airblock)),
        }),
        (typeof(turf_open_indestructible_meat), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("icon_state", "meat"), ("footstep", FOOTSTEP_MEAT), ("barefootstep", FOOTSTEP_MEAT), ("clawfootstep", FOOTSTEP_MEAT),
            ("heavyfootstep", FOOTSTEP_MEAT), ("initial_gas_mix", OPENTURF_DEFAULT_ATMOS), ("baseturfs", typeof(turf_open_indestructible_meat)),
        }),
        (typeof(turf_open_indestructible_meat_airless), typeof(turf_open_indestructible_meat), new (string, object)[] { ("initial_gas_mix", AIRLESS_ATMOS) }),
        (typeof(turf_open_indestructible_vault), typeof(turf_open_indestructible), new (string, object)[] { ("icon_state", "rockvault") }),
        (typeof(turf_open_indestructible_plating), typeof(turf_open_indestructible), new (string, object)[]
        {
            ("name", "plating"), ("icon_state", "plating"),
            ("desc", "The attachment points are all bent to uselessness, looks nigh-impervious to damage."),
            ("overfloor_placed", FALSE), ("underfloor_accessibility", UNDERFLOOR_INTERACTABLE), ("footstep", FOOTSTEP_PLATING),
        }),
        (typeof(turf_open_indestructible_plating_airless), typeof(turf_open_indestructible_plating), new (string, object)[] { ("initial_gas_mix", AIRLESS_ATMOS) }),
    };

    public static IEnumerable<object[]> FloorTypes() => FloorTable.Select(row => new object[] { row.type.Name });

    [Theory]
    [MemberData(nameof(FloorTypes))]
    public void EveryIndestructibleFloorHasTheDefaultsOfItsDMTypeBlock(string className)
    {
        var chain = new List<(string var, object value)>();
        var row = FloorTable.Single(r => r.type.Name == className);
        // inherited first, then the type's own lines (DM override order)
        var path = new Stack<(Type type, Type parent, (string var, object value)[] vars)>();
        var cursor = row;
        while (true)
        {
            path.Push(cursor);
            if (cursor.parent == null)
                break;
            cursor = FloorTable.Single(r => r.type == cursor.parent);
        }
        var expected = new Dictionary<string, object>();
        foreach (var level in path)
        {
            foreach (var (name, value) in level.vars)
                expected[name] = value;
        }

        foreach (var (name, value) in expected)
        {
            object actual = Dm.initial(row.type, name);
            if (value == null)
                Assert.True(actual == null, className + "." + name + " should be null, was " + Dm.str(actual));
            else if (value is Type type)
                Assert.True(ReferenceEquals(type, actual), className + "." + name + " should be " + type.Name);
            else
                Assert.True(Dm.eq(actual, value), className + "." + name + " should be " + Dm.str(value) + ", was " + Dm.str(actual));
        }
    }

    [Fact]
    public void BossHierophantAndNecropolisFloorsKeepTheirOwnIcons()
    {
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open_indestructible_boss), "icon"), Dm.resource("icons/turf/boss_floors.dmi")));
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open_indestructible_hierophant), "icon"), Dm.resource("icons/turf/floors/hierophant_floor.dmi")));
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open_indestructible_hierophant), "emissive_icon"), Dm.resource("icons/turf/floors/hierophant_floor_e.dmi")));
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open_indestructible_hierophant_two), "emissive_icon"), Dm.resource("icons/turf/floors/hierophant_floor_alt_e.dmi")));
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open_indestructible_necropolis), "icon"), Dm.resource("icons/turf/floors.dmi")));
        Assert.True(Dm.eq(Dm.initial(typeof(turf_open_indestructible_honk), "sound"), Dm.resource("sound/effects/footstep/clownstep1.ogg")));
    }

    [Fact]
    public void NecropolisFloorsRollForAVariantIconAfterTheBaseInitializeAndConsumeTheSeededRandomStream()
    {
        // _open.dm:393-395: `. = ..()`, then `if(prob(12)) icon_state = "necro[rand(2,3)]"`
        var t = new TurfAreaWorld(20, 20, 1, turfType: typeof(turf_open_indestructible_necropolis));
        using (t.World.Enter())
        {
            var variants = new Dictionary<string, int>();
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 20, 20, 1)))
            {
                T.Initialize(TRUE);
                variants[T.icon_state] = variants.GetValueOrDefault(T.icon_state) + 1;
            }
            Assert.Equal(new[] { "necro1", "necro2", "necro3" }, variants.Keys.OrderBy(k => k));
            int rolled = variants["necro2"] + variants["necro3"];
            Assert.InRange(rolled, 15, 90);        // 12% of 400 turfs is 48
            Assert.True(variants["necro2"] > 0 && variants["necro3"] > 0);
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- the z-pass procs (turf.dm:333-347, _open.dm:149-172) ----

    [Fact]
    public void TheBaseTurfPassesNothingAcrossLevelsAndAnOpenTurfOnlyDownInAndUpOut()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            foreach (object dir in new object[] { UP, DOWN, NORTH })
            {
                Assert.True(Dm.eq(wall.zPassIn(dir), FALSE));
                Assert.True(Dm.eq(wall.zPassOut(dir), FALSE));
                Assert.True(Dm.eq(wall.zAirIn(dir, null), FALSE));
                Assert.True(Dm.eq(wall.zAirOut(dir, null), FALSE));
            }

            var open = (turf_open)locate(1, 1, 1);
            Assert.True(Dm.eq(open.zPassIn(DOWN), TRUE));
            Assert.True(Dm.eq(open.zPassIn(UP), FALSE));
            Assert.True(Dm.eq(open.zPassOut(UP), TRUE));
            Assert.True(Dm.eq(open.zPassOut(DOWN), FALSE));
            Assert.True(Dm.eq(open.zAirIn(DOWN, null), TRUE));
            Assert.True(Dm.eq(open.zAirIn(UP, null), FALSE));
            Assert.True(Dm.eq(open.zAirOut(UP, null), TRUE));
            Assert.True(Dm.eq(open.zAirOut(DOWN, null), FALSE));
        }
    }

    [Fact]
    public void AnObjThatBlocksZMovementClosesAnOpenTurfsPassage()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            var plain = Dm.@new<obj_test_turfarea_sitter>(open);
            Assert.True(Dm.eq(open.zPassIn(DOWN), TRUE));
            plain.obj_flags = Dm.bitor(plain.obj_flags, BLOCK_Z_IN_DOWN);
            Assert.True(Dm.eq(open.zPassIn(DOWN), FALSE));
            Assert.True(Dm.eq(open.zPassOut(UP), TRUE));       // a different flag
            plain.obj_flags = BLOCK_Z_OUT_UP;
            Assert.True(Dm.eq(open.zPassIn(DOWN), TRUE));
            Assert.True(Dm.eq(open.zPassOut(UP), FALSE));
        }
    }

    // ---- density and explosion block (turf.dm:746-757, :803-808) ----

    [Fact]
    public void SetDensityKeepsTheTurfsExplosiveResistanceInStepWithItsBlock()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor_blocker));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            Assert.True(Dm.eq(floor.explosive_resistance, 5));
            Assert.False(Dm.truthy(floor.density));

            floor.set_density(TRUE);                            // block is initial(explosive_resistance) = 5
            Assert.True(Dm.truthy(floor.density));
            Assert.True(Dm.eq(floor.explosive_resistance, 10));

            floor.set_density(TRUE);                            // no change: nothing added again
            Assert.True(Dm.eq(floor.explosive_resistance, 10));

            floor.set_density(FALSE);
            Assert.True(Dm.eq(floor.explosive_resistance, 5));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnInherentExplosiveResistanceOverridesTheInitialOneInTheBlock()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var floor = (turf_open)locate(1, 1, 1);
            Assert.True(Dm.eq(wall.get_explosive_block(), 50));    // initial(explosive_resistance)
            Assert.True(Dm.eq(floor.get_explosive_block(), 0));    // nothing: `explosive_resistance` is 0
            wall.set_explosion_block(80);                          // turf.dm:802-806: the net is old - block + new
            Assert.True(Dm.eq(wall.inherent_explosive_resistance, 80));
            Assert.True(Dm.eq(wall.explosive_resistance, 80));
            Assert.True(Dm.eq(wall.get_explosive_block(), 80));
            wall.set_density(FALSE);                               // a dense turf that stops being dense gives its block back
            Assert.True(Dm.eq(wall.explosive_resistance, 0));
        }
    }

    // ---- is_blocked_turf (turf.dm:293-318) ----

    private static obj_test_turfarea_occupant Dense(string name, turf at, float? density = TRUE)
    {
        var o = Dm.@new<obj_test_turfarea_occupant>(at);
        o.name = name;
        o.density = density ?? 0;
        return o;
    }

    [Fact]
    public void ADenseTurfIsBlockedAndAFloorIsBlockedByADenseOccupant()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var floor = locate(1, 1, 1);
            Assert.True(Dm.eq(Waiting.NoSleep(wall.is_blocked_turf()), TRUE));
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf()), FALSE));
            Dense("flat", floor, 0);
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf()), FALSE));
            Dense("heavy", floor);
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf()), TRUE));
        }
    }

    [Fact]
    public void IsBlockedTurfSkipsTheSourceAndIgnoredAtomsAndTypesAndCanExcludeMobs()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var heavy = Dense("heavy", floor);
            var mob = Dm.@new<mob_test_turfarea_mob>(floor);
            mob.density = TRUE;

            // exclude_mobs: the dense mob does not count, the dense obj does
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(TRUE)), TRUE));
            qdel(heavy);
            heavy.loc = null;
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(TRUE)), FALSE));
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE)), TRUE));

            // source_atom is skipped ("We don't want to block ourselves")
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE, mob)), FALSE));

            // ignore_atoms by object, and by type when type_list is set
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE, null, Dm.list(mob))), FALSE));
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE, null, Dm.list(typeof(mob_test_turfarea_mob)), TRUE)), FALSE));
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE, null, Dm.list(typeof(mob_test_turfarea_mob)), FALSE)), TRUE));   // types are only read with type_list
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void IsBlockedTurfAsksCanPassForADenseOccupantWhenThereIsASourceAtom()
    {
        var t = new TurfAreaWorld(1, 1, 2);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var heavy = Dense("heavy", floor);
            var source = Dm.@new<obj_test_turfarea_sitter>(locate(1, 1, 2));
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE, source)), TRUE));
            // the source can pass the heavy thing: turf.dm:310-311 continues
            heavy.pass_flags_self = PASSTABLE;
            source.pass_flags = PASSTABLE;
            Assert.True(Dm.eq(Waiting.NoSleep(floor.is_blocked_turf(FALSE, source)), FALSE));
        }
    }

    [Fact]
    public void IsBlockedTurfIgnoreClimbableIgnoresClimbablesAndBorderObjects()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            Assert.True(Dm.eq(wall.is_blocked_turf_ignore_climbable(), TRUE));

            turf floor = locate(1, 1, 1);
            var table = Dense("table", floor);
            ADD_TRAIT(table, TRAIT_CLIMBABLE, "test");
            var window = Dense("window", floor);
            window.flags_1 = ON_BORDER_1;
            Assert.True(Dm.eq(floor.is_blocked_turf_ignore_climbable(), FALSE));
            var crate = Dense("crate", floor);
            Assert.True(Dm.eq(floor.is_blocked_turf_ignore_climbable(), TRUE));
            crate.density = FALSE;
            Assert.True(Dm.eq(floor.is_blocked_turf_ignore_climbable(), FALSE));
        }
    }

    // ---- distances, wash and the small roots ----

    [Fact]
    public void DistanceIsByteDsDistanceAndTheCardinalVariantIsTheManhattanDistanceOnTheMap()
    {
        var t = new TurfAreaWorld(5, 5, 2);
        using (t.World.Enter())
        {
            turf a = locate(1, 1, 1), b = locate(4, 3, 1), c = locate(4, 3, 2);
            Assert.True(Dm.eq(a.Distance(b), get_dist(a, b)));
            Assert.True(Dm.eq(a.Distance_cardinal(b), 5));         // |1-4| + |1-3|
            Assert.True(Dm.eq(a.Distance_cardinal(c), 5));         // the z level is not part of it
            Assert.True(Dm.eq(a.Distance_cardinal(null), FALSE));  // `if(!src || !T) return FALSE`
            Assert.True(Dm.eq(a.Distance_cardinal(a), 0));
        }
    }

    [Fact]
    public void TheSmallRootProcsAnswerAsTheDMDoes()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var floor = (turf_open)locate(1, 1, 1);
            Assert.True(Dm.eq(floor.can_have_cabling(), TRUE));
            Assert.True(Dm.eq(floor.can_lay_cable(), FALSE));          // underfloor_accessibility is HIDDEN
            floor.underfloor_accessibility = UNDERFLOOR_INTERACTABLE;
            Assert.True(Dm.eq(floor.can_lay_cable(), TRUE));
            Assert.True(Dm.eq(floor.is_explosion_shielded(1), FALSE));
            Assert.True(Dm.eq(floor.can_propagate_explosion(null, 1), TRUE));
            Assert.True(Dm.eq(floor.AllowDrop(), TRUE));
            Assert.Null(wall.burn_tile());
            Assert.Null(wall.break_tile());
            Assert.Null(wall.is_transition_turf());
            Assert.True(Dm.eq(wall.GetHeatCapacity(), INFINITY));
            Assert.True(Dm.eq(wall.GetTemperature(), T20C));
            wall.TakeTemperature(30);
            Assert.True(Dm.eq(wall.temperature, T20C + 30));
            Assert.True(Dm.eq(wall.can_cross_safely(null), TRUE));
            ADD_TRAIT(wall, TRAIT_AI_AVOID_TURF, "test");
            Assert.True(Dm.eq(wall.can_cross_safely(null), FALSE));
        }
    }

    [Fact]
    public void BreakAndBurnTileDoNothingWithoutADamageIconOrWhenAlreadyDone()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            Assert.True(Dm.eq(open.break_tile(), FALSE));       // isnull(damaged_dmi)
            Assert.True(Dm.eq(open.burn_tile(), FALSE));
            Assert.True(Dm.eq(open.broken, FALSE));
            open.damaged_dmi = Dm.resource("icons/turf/damaged.dmi");
            open.broken = TRUE;
            open.burnt = TRUE;
            Assert.True(Dm.eq(open.break_tile(), FALSE));       // already broken
            Assert.True(Dm.eq(open.burn_tile(), FALSE));
        }
    }

    [Fact]
    public void ClearFootprintsEmptiesTheFootprintStateAndRedrawsTheTurf()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            open.footprint_entrance_dirs = NORTH;
            open.footprint_exit_dirs = EAST;
            open.footprint_shoe_types = Dm.list(typeof(obj_test_turfarea_sitter));
            open.footprint_species_types = Dm.list("human");
            var watch = Listener("update_icon");
            watch.RegisterSignal(open, COMSIG_ATOM_UPDATE_ICON, nameof(datum_test_turfarea_listener.on_any));

            open.clear_footprints();

            Assert.True(Dm.eq(open.footprint_entrance_dirs, NONE));
            Assert.True(Dm.eq(open.footprint_exit_dirs, NONE));
            Assert.Null(open.footprint_shoe_types);
            Assert.Null(open.footprint_species_types);
            Assert.Single(Lines(t), l => l.StartsWith("update_icon"));   // update_appearance()
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UpdateVisualsOfAnOpenTurfWithoutAirDropsItsAtmosOverlays()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            var overlay = Dm.@new<obj_test_turfarea_sitter>();
            open.atmos_overlay_types = Dm.list(overlay);
            Dm.append(open.vis_contents, overlay);
            open.update_visuals();   // LINDA_turf_tile.dm:173-182: no air, so the overlays go
            Assert.Null(open.atmos_overlay_types);
            Assert.Equal(0, open.vis_contents.len);
        }
    }

    [Fact]
    public void AnOpenTurfGetsItsInitialGasMixAsRealAir()
    {
        // M2 P2 (scope.md 2e "atmosphere"): create_gas_mixture parses the turf's initial_gas_mix through
        // SSair.parse_gas_string (LINDA_turf_tile.dm:81, air.dm:779) into a /datum/gas_mixture/turf.
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            open.Initialize(TRUE);
            var air = Assert.IsType<datum_gas_mixture_turf>(open.air);
            Assert.Equal(OPENTURF_DEFAULT_ATMOS, open.initial_gas_mix); // "o2=22;n2=82;TEMP=293.15"
            Assert.Equal(22f, Dm.num(air.moles[typeof(datum_gas_oxygen)]));
            Assert.Equal(82f, Dm.num(air.moles[typeof(datum_gas_nitrogen)]));
            Assert.Equal(293.15f, air.temperature, 3);
            Assert.Same(air, Waiting.NoSleep(open.return_air()));
            var wall = Dm.@new<turf_closed_indestructible>(locate(1, 1, 1));
            wall.Initialize(TRUE);
            Assert.True(Dm.truthy(wall.blocks_air));       // blocks_air turfs never ask for air
        }
    }

    [Fact]
    public void OpenTurfDestroyQueuesItsAtmosNeighboursAndLeavesAMissingHotspotAlone()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var open = (turf_open)locate(1, 1, 1);
            var neighbour = (turf_open)locate(2, 1, 1);
            open.changing_turf = TRUE;
            open.atmos_adjacent_turfs = Dm.list(neighbour);
            neighbour.air ??= Dm.@new<datum_gas_mixture>();

            open.Destroy();

            // LINDA_turf_tile.dm:68-74: `SSair.add_to_active(near_turf)` per neighbour (translated in M4b slice 1)
            Assert.True(Dm.@in(neighbour, SSair.active_turfs));
            Assert.True(Dm.truthy(neighbour.excited));
            Assert.Null(open.active_hotspot);
            Assert.Empty(t.Runtimes);
        }
    }
}
