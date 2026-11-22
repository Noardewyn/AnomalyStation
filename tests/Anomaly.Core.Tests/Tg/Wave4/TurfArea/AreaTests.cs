using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// /area (game/area/areas.dm), the area half of the package: registration in New, Initialize and LateInitialize, Destroy,
/// power, fire and parallax state, Entered and Exited (waitfor FALSE, no ..()), the small procs, /area/space and
/// /area/misc/testroom (areas/misc.dm), and /datum/alarm_handler (datums/alarm.dm), from the DM.
/// </summary>
public class AreaTests
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

    // ---- New (areas.dm:177-185) ----

    [Fact]
    public void NewRegistersAUniqueAreaByTypeAndInGLOBAreasAndGivesItItsEnergyListAndAlarmHandler()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = (area)locate(1, 1, 1).loc;   // the world.area instance, created by the engine
            var powered = Dm.@new<area_test_turfarea_powered>();

            Assert.Same(powered, GLOB.areas_by_type[typeof(area_test_turfarea_powered)]);
            Assert.Equal(new area[] { room, powered }, TurfAreaLists.Of<area>(GLOB.areas));
            Assert.Equal(AREA_USAGE_LEN, powered.energy_usage.len);
            for (int i = 1; i <= AREA_USAGE_LEN; i++)
                Assert.Null(powered.energy_usage[(float)i]);   // `new /list(AREA_USAGE_LEN)`: nulls, not zeros
            Assert.IsType<datum_alarm_handler>(powered.alarm_manager);
            Assert.Same(powered, powered.alarm_manager.source_atom);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAreaWithoutUNIQUE_AREAIsListedButNotRegisteredByType()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var common = Dm.@new<area_test_turfarea_common>();
            Assert.Null(GLOB.areas_by_type[typeof(area_test_turfarea_common)]);
            Assert.True(Dm.@in(common, GLOB.areas));
        }
    }

    [Fact]
    public void ASecondUniqueAreaOfATypeTakesOverTheRegistrationAndDestroyingTheFirstKeepsIt()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var first = Dm.@new<area_test_turfarea_powered>();
            var second = Dm.@new<area_test_turfarea_powered>();
            Assert.Same(second, GLOB.areas_by_type[typeof(area_test_turfarea_powered)]);   // `GLOB.areas_by_type[type] = src`

            qdel(first);   // Destroy only clears the entry when it names the area itself (areas.dm:405-406)
            Assert.Same(second, GLOB.areas_by_type[typeof(area_test_turfarea_powered)]);
            qdel(second);
            Assert.Null(GLOB.areas_by_type[typeof(area_test_turfarea_powered)]);
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- Initialize (areas.dm:195-219) ----

    [Fact]
    public void InitializeOfAnAreaThatNeedsPowerKeepsItsPowerVarsAndDarkensIt()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var powered = Dm.@new<area_test_turfarea_powered>();
            powered.power_light = FALSE;
            powered.power_equip = FALSE;
            powered.power_environ = FALSE;

            object hint = powered.Initialize(FALSE);

            Assert.True(Dm.eq(hint, INITIALIZE_HINT_LATELOAD));
            Assert.Equal("", powered.icon_state);
            Assert.True(Dm.eq(powered.luminosity, 0));    // requires_power: luminosity = 0 (areas.dm:202)
            Assert.True(Dm.eq(powered.power_light, FALSE));
            Assert.True(Dm.eq(powered.power_equip, FALSE));
            Assert.True(Dm.eq(powered.power_environ, FALSE));
            Assert.True(Dm.truthy(Dm.bitand(powered.flags_1, INITIALIZED_1)));   // through ..() into /atom/Initialize
            Assert.True(Dm.eq(powered.blend_mode, BLEND_DEFAULT));                  // static_lighting: no multiply blend
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeOfAPowerlessAreaPowersEveryChannelAndDarkensItOnlyWithStaticLighting()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var lit = Dm.@new<area_test_turfarea_lit>();           // requires_power FALSE, static_lighting TRUE
            lit.power_light = lit.power_equip = lit.power_environ = FALSE;
            lit.Initialize(FALSE);
            Assert.True(Dm.eq(lit.power_light, TRUE));
            Assert.True(Dm.eq(lit.power_equip, TRUE));
            Assert.True(Dm.eq(lit.power_environ, TRUE));
            Assert.True(Dm.eq(lit.luminosity, 0));                  // areas.dm:210-211

            var room = Dm.@new<area_misc_testroom>();               // static_lighting FALSE (misc.dm:41)
            room.power_light = FALSE;
            room.Initialize(FALSE);
            Assert.True(Dm.eq(room.power_light, TRUE));
            Assert.True(Dm.eq(room.luminosity, 1));                 // no static lighting: the engine's default stays
            Assert.True(Dm.eq(room.blend_mode, BLEND_MULTIPLY));    // areas.dm:216-217
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeFillsAmbientSoundsFromTheAmbienceIndexOnlyWhenTheAreaHasNone()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var sounds = Dm.list("a.ogg", "b.ogg");
            GLOB.ambience_assoc["test_turfarea_ambience"] = sounds;
            var noisy = Dm.@new<area_test_turfarea_noisy>();
            Assert.Null(noisy.ambientsounds);
            noisy.Initialize(FALSE);
            Assert.Same(sounds, noisy.ambientsounds);   // areas.dm:196-197

            var own = Dm.@new<area_test_turfarea_noisy>();
            var mine = Dm.list("mine.ogg");
            own.ambientsounds = mine;
            own.Initialize(FALSE);
            Assert.Same(mine, own.ambientsounds);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeCallsAtomInitializeSoTwiceIsReportedAsAWarning()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var powered = Dm.@new<area_test_turfarea_powered>();
            powered.Initialize(FALSE);
            Assert.Empty(t.Runtimes);
            powered.Initialize(FALSE);
            Assert.Single(t.Runtimes);
            Assert.Contains("initialized multiple times", t.Runtimes[0].Message);
        }
    }

    [Fact]
    public void InitializeRegistersTheAreaInSSmappingsAreasInZAtItsLevelAndCountsItsOpenTurfs()
    {
        var t = new TurfAreaWorld(3, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            space.turfs_by_zlevel.len = 1;
            space.turfs_by_zlevel[1f] = block(1, 1, 1, 3, 1, 1);
            Dm.@new<turf_closed_indestructible>(locate(3, 1, 1));
            var room = Dm.@new<area_misc_testroom>();
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 3, 1, 1)))
                T.change_area(space, room);

            room.Initialize(FALSE);

            Assert.Equal(2, room.areasize);   // two floors and a wall
            Assert.Equal(new[] { room }, TurfAreaLists.Of<area>(SSmapping.areas_in_z["1"]));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeOfAnAreaWithNoTurfsRegistersNothing()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var empty = Dm.@new<area_misc_testroom>();
            empty.Initialize(FALSE);
            Assert.Equal(0, SSmapping.areas_in_z.len);
            Assert.Equal(0, empty.areasize);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void SSatomsLateInitializesAnAreaThroughTheLateloadHintOncePowerChangeAndBeautyIncluded()
    {
        var t = new TurfAreaWorld(4, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            space.turfs_by_zlevel.len = 1;
            space.turfs_by_zlevel[1f] = block(1, 1, 1, 4, 1, 1);
            var room = Dm.@new<area_misc_testroom>();
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 3, 1, 1)))
                T.change_area(space, room);
            room.totalbeauty = 30;
            var power = Listener("power_change");
            power.RegisterSignal(room, COMSIG_AREA_POWER_CHANGE, nameof(datum_test_turfarea_listener.on_any));
            var beauty = Listener("beauty_updated");
            beauty.RegisterSignal(room, COMSIG_AREA_BEAUTY_UPDATED, nameof(datum_test_turfarea_listener.on_any));

            TurfAreaWorld.InitializeSSatoms();

            Assert.Equal(new[] { "power_change area:area_misc_testroom(null,null,null,null)" }, Lines(t).Where(l => l.StartsWith("power_change")));
            Assert.Equal(3, room.areasize);
            Assert.True(Dm.eq(room.beauty, 10));    // areas.dm:491: totalbeauty / areasize
            Assert.Single(Lines(t), l => l.StartsWith("beauty_updated"));
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- update_beauty (areas.dm:610-620) ----

    [Fact]
    public void UpdateBeautyAveragesOverTheAreaSizeAndZeroesTooBigOrEmptyAreas()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_misc_testroom>();
            room.totalbeauty = 12;
            room.beauty = 7;
            Assert.True(Dm.eq(room.update_beauty(), FALSE));   // no areasize
            Assert.True(Dm.eq(room.beauty, 0));

            room.areasize = 150;   // `areasize >= beauty_threshold`: too big
            room.beauty = 7;
            Assert.True(Dm.eq(room.update_beauty(), FALSE));
            Assert.True(Dm.eq(room.beauty, 0));

            room.areasize = 149;
            Assert.Null(room.update_beauty());                  // the success path has no return value
            Assert.True(Dm.eq(room.beauty, 12f / 149f));
        }
    }

    // ---- Destroy (areas.dm:397-420) ----

    [Fact]
    public void DestroyUnregistersTheAreaAndNullsItsListsAndDeletesItsAlarmHandler()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var handler = room.alarm_manager;
            GLOB.sortedAreas = Dm.list(room);
            GLOB.custom_areas = Dm.list(Dm.kv(room, TRUE));
            var other = Dm.@new<area_test_turfarea_common>();
            Dm.append(GLOB.sortedAreas, other);

            qdel(room);

            Assert.True(QDELETED(room));
            Assert.Null(GLOB.areas_by_type[typeof(area_test_turfarea_powered)]);
            Assert.False(Dm.@in(room, GLOB.areas));
            Assert.Equal(new[] { other }, TurfAreaLists.Of<area>(GLOB.sortedAreas));
            Assert.False(Dm.@in(room, GLOB.custom_areas));
            Assert.Null(room.alarm_manager);
            Assert.True(QDELETED(handler));
            Assert.Null(room.turfs_by_zlevel);
            Assert.Null(room.turfs_to_uncontain_by_zlevel);
            Assert.Null(room.firealarms);
            Assert.Null(room.air_vents);
            Assert.Null(room.air_scrubbers);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DestroyDoesNotTouchSortedAreasOrCustomAreasThatWereNeverMade()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            GLOB.sortedAreas = null;   // "not initialized until get_sorted_areas() is called" (areas.dm:407-408)
            GLOB.custom_areas = null;
            qdel(room);
            Assert.Null(GLOB.sortedAreas);
            Assert.Null(GLOB.custom_areas);
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- power ----

    [Fact]
    public void PoweredAnswersPerChannelAndRequiresPowerAndAlwaysUnpoweredOverrideIt()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            room.power_equip = TRUE;
            room.power_light = FALSE;
            room.power_environ = TRUE;
            Assert.True(Dm.eq(room.powered(AREA_USAGE_EQUIP), TRUE));
            Assert.True(Dm.eq(room.powered(AREA_USAGE_LIGHT), FALSE));
            Assert.True(Dm.eq(room.powered(AREA_USAGE_ENVIRON), TRUE));
            Assert.True(Dm.eq(room.powered(AREA_USAGE_STATIC_EQUIP), FALSE));   // not a switch case (areas.dm:466-475)
            room.always_unpowered = TRUE;
            Assert.True(Dm.eq(room.powered(AREA_USAGE_EQUIP), FALSE));
            room.requires_power = FALSE;
            Assert.True(Dm.eq(room.powered(AREA_USAGE_EQUIP), TRUE));           // "!requires_power" is checked first
            Assert.True(Dm.eq(room.powered(null), TRUE));
        }
    }

    [Fact]
    public void SpaceIsNeverPowered()
    {
        var t = new TurfAreaWorld(1, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            space.power_equip = TRUE;
            space.requires_power = FALSE;   // even then: /area/space/powered answers FALSE (areas.dm:480)
            Assert.True(Dm.eq(space.powered(AREA_USAGE_EQUIP), FALSE));
            Assert.True(Dm.eq(space.powered(AREA_USAGE_LIGHT), FALSE));
        }
    }

    [Fact]
    public void ClearUsageZeroesTheFourDynamicChannelsAndLeavesTheStaticOnes()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            for (int i = 1; i <= AREA_USAGE_LEN; i++)
                room.energy_usage[(float)i] = 5;
            room.clear_usage();
            foreach (int chan in new[] { AREA_USAGE_EQUIP, AREA_USAGE_LIGHT, AREA_USAGE_ENVIRON, AREA_USAGE_APC_CHARGE })
                Assert.True(Dm.eq(room.energy_usage[(float)chan], 0), "channel " + chan);
            foreach (int chan in new[] { AREA_USAGE_STATIC_EQUIP, AREA_USAGE_STATIC_LIGHT, AREA_USAGE_STATIC_ENVIRON })
                Assert.True(Dm.eq(room.energy_usage[(float)chan], 5), "channel " + chan);
        }
    }

    [Fact]
    public void UseEnergyAddsToADynamicChannelAndIgnoresTheStaticRange()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            room.use_energy(30, AREA_USAGE_LIGHT);   // null + 30
            room.use_energy(12, AREA_USAGE_LIGHT);
            Assert.True(Dm.eq(room.energy_usage[(float)AREA_USAGE_LIGHT], 42));
            foreach (int chan in new[] { AREA_USAGE_STATIC_EQUIP, AREA_USAGE_STATIC_LIGHT, AREA_USAGE_STATIC_ENVIRON })
            {
                room.use_energy(99, chan);
                Assert.Null(room.energy_usage[(float)chan]);
            }
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- fire and parallax state ----

    [Fact]
    public void SetFireEffectChangesStateAndSignalsOnlyOnAChange()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var watch = Listener("fire");
            watch.RegisterSignal(room, COMSIG_AREA_FIRE_CHANGED, nameof(datum_test_turfarea_listener.on_any));
            var source = Dm.@new<obj_test_turfarea_sitter>();

            room.set_fire_effect(FALSE);   // fire is already FALSE: `new_fire == fire`, return
            Assert.Empty(Lines(t));

            room.set_fire_effect(TRUE, AREA_FAULT_AUTOMATIC, source);
            Assert.True(Dm.eq(room.fire, TRUE));
            Assert.True(Dm.eq(room.fault_status, AREA_FAULT_AUTOMATIC));
            Assert.Same(source, room.fault_location);
            Assert.Equal("fire area:area_test_turfarea_powered(" + Dm.str(TRUE) + ",null,null,null)", Assert.Single(Lines(t)));

            room.set_fire_effect(TRUE, AREA_FAULT_MANUAL, null);   // no change: nothing happens, not even the fault status
            Assert.True(Dm.eq(room.fault_status, AREA_FAULT_AUTOMATIC));
            Assert.Single(Lines(t));

            room.set_fire_effect(FALSE);
            Assert.Null(room.fault_location);       // `fault_location = null` when the fire ends
            Assert.Equal(2, Lines(t).Count);
        }
    }

    [Fact]
    public void SetParallaxMovedirSignalsOnlyOnAChange()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var watch = Listener("parallax");
            watch.RegisterSignal(room, COMSIG_AREA_PARALLAX_DIR_CHANGED, nameof(datum_test_turfarea_listener.on_any));
            room.set_parallax_movedir(NONE);
            Assert.Empty(Lines(t));
            room.set_parallax_movedir(EAST);
            Assert.True(Dm.eq(room.parallax_movedir, EAST));
            Assert.Equal("parallax area:area_test_turfarea_powered(" + Dm.str(EAST) + ",null,null,null)", Assert.Single(Lines(t)));
            room.set_parallax_movedir(EAST);
            Assert.Single(Lines(t));
        }
    }

    // ---- Entered and Exited (areas.dm:581-606) ----

    [Fact]
    public void EnteredSendsTheAreaSignalAndNothingMoreForAMoverThatIsNotAreaSensitive()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var from = Dm.@new<area_test_turfarea_common>();
            var mover = Dm.@new<obj_test_turfarea_sitter>();
            mover.name = "mover";
            var watch = Listener("area_entered");
            watch.RegisterSignal(room, COMSIG_AREA_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            var atom_signals = Listener("atom_entered");
            atom_signals.RegisterSignal(room, COMSIG_ATOM_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            atom_signals.RegisterSignal(mover, COMSIG_ATOM_ENTERING, nameof(datum_test_turfarea_listener.on_any));

            object answer = Waiting.NoSleep(room.Entered(mover, from, null));

            Assert.Null(answer);
            // no ..(): an area's Entered never reaches /atom/Entered, so no COMSIG_ATOM_ENTERED or COMSIG_ATOM_ENTERING
            Assert.Equal(new[] { "area_entered area:area_test_turfarea_powered(mover,area:area_test_turfarea_common,null,null)" }, Lines(t));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void EnteredTellsEveryAreaSensitiveRecipientOfTheArrivalInOrder()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var carrier = Dm.@new<obj_test_turfarea_sitter>();
            carrier.name = "carrier";
            var ear1 = Dm.@new<obj_test_turfarea_sitter>();
            ear1.name = "ear1";
            var ear2 = Dm.@new<obj_test_turfarea_sitter>();
            ear2.name = "ear2";
            carrier.important_recursive_contents = Dm.list(Dm.kv(RECURSIVE_CONTENTS_AREA_SENSITIVE, Dm.list(ear1, ear2)));
            var watch = Listener("enter_area");
            watch.RegisterSignal(ear1, COMSIG_ENTER_AREA, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(ear2, COMSIG_ENTER_AREA, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(carrier, COMSIG_ENTER_AREA, nameof(datum_test_turfarea_listener.on_any));

            Waiting.NoSleep(room.Entered(carrier, null, null));

            // COMSIG_ENTER_AREA goes to each recipient (source: the recipient; argument: the area), not to the carrier
            Assert.Equal(new[]
            {
                "enter_area ear1(area:area_test_turfarea_powered,null,null,null)",
                "enter_area ear2(area:area_test_turfarea_powered,null,null,null)",
            }, Lines(t));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void EnteredUpdatesTheAmbienceOfAnAreaSensitiveMobAfterTheSignalsAndOnlyThen()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var plain = Dm.@new<mob_test_turfarea_mob>();
            plain.name = "plain";
            var hearing = Dm.@new<mob_test_turfarea_mob>();
            hearing.name = "hearing";
            hearing.important_recursive_contents = Dm.list(Dm.kv(RECURSIVE_CONTENTS_AREA_SENSITIVE, Dm.list(hearing)));
            var watch = Listener("enter_area");
            watch.RegisterSignal(hearing, COMSIG_ENTER_AREA, nameof(datum_test_turfarea_listener.on_any));

            Waiting.NoSleep(room.Entered(plain, null, null));
            Assert.Empty(Lines(t));   // areas.dm:585-586: the early return is before the mob branch

            Waiting.NoSleep(room.Entered(hearing, null, null));
            Assert.Equal(new[]
            {
                "enter_area hearing(area:area_test_turfarea_powered,null,null,null)",
                "update_ambience_area hearing area:area_test_turfarea_powered",
            }, Lines(t));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ExitedSendsBothSignalsAndTheRecipientFanOutAndNoAtomExitedSignal()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var gone = Dm.@new<obj_test_turfarea_sitter>();
            gone.name = "gone";
            var ear = Dm.@new<obj_test_turfarea_sitter>();
            ear.name = "ear";
            gone.important_recursive_contents = Dm.list(Dm.kv(RECURSIVE_CONTENTS_AREA_SENSITIVE, Dm.list(ear)));
            var watch = Listener("area_exited");
            watch.RegisterSignal(room, COMSIG_AREA_EXITED, nameof(datum_test_turfarea_listener.on_any));
            var left = Listener("exited_area");
            left.RegisterSignal(gone, COMSIG_MOVABLE_EXITED_AREA, nameof(datum_test_turfarea_listener.on_any));
            var fan = Listener("exit_area");
            fan.RegisterSignal(ear, COMSIG_EXIT_AREA, nameof(datum_test_turfarea_listener.on_any));
            var atom_signals = Listener("atom_exited");
            atom_signals.RegisterSignal(room, COMSIG_ATOM_EXITED, nameof(datum_test_turfarea_listener.on_any));
            atom_signals.RegisterSignal(gone, COMSIG_ATOM_EXITING, nameof(datum_test_turfarea_listener.on_any));

            Assert.Null(Waiting.NoSleep(room.Exited(gone, WEST)));

            Assert.Equal(new[]
            {
                "area_exited area:area_test_turfarea_powered(gone," + Dm.str(WEST) + ",null,null)",
                "exited_area gone(area:area_test_turfarea_powered," + Dm.str(WEST) + ",null,null)",
                "exit_area ear(area:area_test_turfarea_powered,null,null,null)",
            }, Lines(t));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ExitedOfAMoverThatIsNotAreaSensitiveStopsAfterTheTwoSignals()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var gone = Dm.@new<obj_test_turfarea_sitter>();
            gone.name = "gone";
            var watch = Listener("any");
            watch.RegisterSignal(room, COMSIG_AREA_EXITED, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(gone, COMSIG_MOVABLE_EXITED_AREA, nameof(datum_test_turfarea_listener.on_any));
            Waiting.NoSleep(room.Exited(gone, NORTH));
            Assert.Equal(2, Lines(t).Count);
        }
    }

    // ---- the small procs ----

    [Fact]
    public void SetupNamesTheAreaClearsItsPowerAndTerritoryFlagsAndForcesAResort()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            room.always_unpowered = TRUE;
            room.area_flags = Dm.bitor(Dm.bitor(Dm.bitor(VALID_TERRITORY, BLOBS_ALLOWED), CULT_PERMITTED), NOTELEPORT);
            GLOB.sortedAreas = Dm.list(room);

            room.setup("Custom Room");

            Assert.Equal("Custom Room", room.name);
            Assert.True(Dm.eq(room.power_equip, FALSE));
            Assert.True(Dm.eq(room.power_light, FALSE));
            Assert.True(Dm.eq(room.power_environ, FALSE));
            Assert.True(Dm.eq(room.always_unpowered, FALSE));
            Assert.True(Dm.eq(room.area_flags, NOTELEPORT));   // only the three territory bits are removed
            Assert.Null(GLOB.sortedAreas);                       // require_area_resort()
        }
    }

    [Fact]
    public void GetSortedAreasSortsByNameOnceAndCachesTheResult()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var b = Dm.@new<area_test_turfarea_powered>();
            b.name = "b room";
            var a = Dm.@new<area_test_turfarea_common>();
            a.name = "a room";
            GLOB.sortedAreas = null;

            var sorted = (DmList)get_sorted_areas();
            Assert.True(Dm.@in(a, sorted));
            Assert.True(sorted.Find(a) < sorted.Find(b));
            Assert.Same(sorted, get_sorted_areas());   // cached until require_area_resort
            require_area_resort();
            Assert.NotSame(sorted, get_sorted_areas());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AllowDropAndDropLocationOfAnAreaAreRuntimeErrors()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var drop = Assert.Throws<DmCrashException>(() => room.AllowDrop());
            Assert.Contains("Bad op: area/AllowDrop() called", drop.Message);
            var location = Assert.Throws<DmCrashException>(() => room.drop_location());
            Assert.Contains("Bad op: area/drop_location() called", location.Message);
        }
    }

    [Fact]
    public void TheOriginalAreaNameIsAddedInBracketsOnlyWhenTheNameChanged()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_misc_testroom>();
            Assert.Equal("Test Room", room.get_original_area_name());
            room.name = "Gateway";
            Assert.Equal("Gateway (Test Room)", room.get_original_area_name());
        }
    }

    [Fact]
    public void PlaceOnTopReactReturnsTheFlagsAsTheyAre()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_misc_testroom>();
            Assert.True(Dm.eq(room.place_on_top_react(Dm.list(), null, 5), 5));
            Assert.Null(room.place_on_top_react(Dm.list(), null, null));
        }
    }

    // ---- type defaults ----

    [Fact]
    public void TheTestroomIsAPowerlessLitRoomWithStandardGravity()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_misc_testroom>();
            Assert.True(Dm.eq(room.requires_power, FALSE));
            Assert.True(Dm.eq(room.default_gravity, STANDARD_GRAVITY));
            Assert.True(Dm.eq(room.static_lighting, FALSE));
            Assert.True(Dm.eq(room.base_lighting_alpha, 255));
            Assert.Equal("Test Room", room.name);
            Assert.Equal("test_room", room.icon_state);
        }
    }

    [Fact]
    public void SpaceIsOutdoorsUnpoweredAndHasNoIconState()
    {
        var t = new TurfAreaWorld(1, 1, 1, areaType: typeof(area_space));
        using (t.World.Enter())
        {
            var space = (area)locate(1, 1, 1).loc;
            Assert.True(Dm.truthy(space.always_unpowered));
            Assert.True(Dm.truthy(space.outdoors));
            Assert.True(Dm.eq(space.area_flags, NO_GRAVITY));
            Assert.True(Dm.eq(space.default_gravity, ZERO_GRAVITY));   // the base area's default stays
            // /area/space/update_icon_state is not a ..() override (areas.dm:455): the icon state becomes null
            space.update_appearance();
            Assert.Null(space.icon_state);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ABareAreaHasTgsAreaDefaults()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var bare = Dm.@new<area_test_turfarea_powered>();
            Assert.True(Dm.eq(bare.area_flags, Dm.bitor(Dm.bitor(VALID_TERRITORY, BLOBS_ALLOWED), CULT_PERMITTED)));
            Assert.True(Dm.eq(bare.area_flags_mapping, UNIQUE_AREA));
            Assert.True(Dm.eq(bare.layer, AREA_LAYER));
            Assert.True(Dm.eq(bare.plane, AREA_PLANE));
            Assert.True(Dm.eq(bare.mouse_opacity, MOUSE_OPACITY_TRANSPARENT));
            Assert.True(Dm.eq(bare.invisibility, INVISIBILITY_LIGHTING));
            Assert.True(Dm.eq(bare.flags_1, CAN_BE_DIRTY_1));
            Assert.True(Dm.truthy(bare.requires_power));
            Assert.True(Dm.eq(bare.default_gravity, ZERO_GRAVITY));
            Assert.True(Dm.eq(bare.beauty_threshold, 150));
            Assert.Equal("unknown", bare.icon_state);
            Assert.Equal(0, bare.turfs_by_zlevel.len);
            Assert.Equal(0, bare.turfs_to_uncontain_by_zlevel.len);
            // each area gets its own lists (dm_init_vars), not one shared by every area
            var other = Dm.@new<area_test_turfarea_common>();
            Assert.NotSame(bare.turfs_by_zlevel, other.turfs_by_zlevel);
            Assert.NotSame(bare.lights, other.lights);
        }
    }

    // ---- /datum/alarm_handler (datums/alarm.dm:13-33) ----

    [Fact]
    public void AnAlarmHandlerKeepsItsSourceAtomAndDropsItOnDestroy()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var room = Dm.@new<area_test_turfarea_powered>();
            var handler = Dm.@new<datum_alarm_handler>(room);
            Assert.Same(room, handler.source_atom);
            qdel(handler);
            Assert.Null(handler.source_atom);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAlarmHandlerGivenANonAtomReportsAStackTraceAndHasNoSource()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var not_an_atom = Dm.@new<datum_test_turfarea_listener>();
            var handler = Dm.@new<datum_alarm_handler>(not_an_atom);
            Assert.Null(handler.source_atom);
            Assert.Single(t.Runtimes);
            Assert.Contains("a non atom was passed into alarm_handler!", t.Runtimes[0].Message);

            var from_null = Dm.@new<datum_alarm_handler>(null);   // `isdatum(null)` is false: the type part is ""
            Assert.Null(from_null.source_atom);
            Assert.Equal(2, t.Runtimes.Count);
        }
    }
}
