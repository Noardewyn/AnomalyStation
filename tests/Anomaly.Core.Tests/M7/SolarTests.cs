using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice G1 (D163): Meta's solars (modules/power/solar.dm, tracker.dm, controllers/subsystem/sun.dm, __HELPERS/turfs.dm:563) in a
/// booted toy room with a player: a cable run at y 4 (x 2-9) with a tracker at x 2, three panels at x 3-5 and a control computer at
/// x 6. A panel's output as tg computes it from the sun and its own angle, the occlusion by an opaque turf, the sun's own move and
/// signal, the tracker turning the panels through the computer, the computer's modes and its window's acts, and the assembly's
/// construction and deconstruction with glass.
/// </summary>
public class SolarTests
{
    private const int Second = 20;
    private const int Y = 4;
    private const float Rate = 2500; // SOLAR_GEN_RATE (solar.dm:1)
    private const float Cycle = Rate * 2; // power_to_energy of a full panel over SSmachines' 2 seconds

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed = 7301, Action<World> extras = null, bool tracker = true, bool control = true)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 1; x <= 10; x++)
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
            Dm.@new<turf_open_floor_plating>(locate(4, Y + 1, 1));
            for (int x = 2; x <= 10; x++)
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
            if (tracker)
                Dm.@new<obj_machinery_power_tracker>(locate(2, Y, 1));
            for (int x = 3; x <= 5; x++)
                Dm.@new<obj_machinery_power_solar>(locate(x, Y, 1));
            if (control)
                Dm.@new<obj_machinery_power_solar_control>(locate(6, Y, 1));
            extras?.Invoke(w);
        });
        mob_living_carbon_human bob = r.Join(4, Y + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.In(() =>
        {
            for (int x = 3; x <= 5; x++)
            {
                obj_machinery_power_solar p = Dm.locate<obj_machinery_power_solar>(locate(x, Y, 1));
                Assert.Equal(obj_machinery_power_solar.anomaly_start_azimuth(p.dm_id), p.azimuth_current); // DV197: its own start bearing
                p.azimuth_current = p.azimuth_target = 0; // tg's start, which these tests measure from
            }
        });
        r.Step(Second);
        // the sun stands due west, where the row of the array has an open view to the map's edge, and does not move unless a test fires it
        r.In(() =>
        {
            SSsun.azimuth_mod = 1;
            SSsun.next_fire = Dm.world.time + 1000000;
        });
        Sun(r, 270);
        return (r, bob);
    }

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static obj_machinery_power_solar Panel(AudioRoom r, int x) => r.In(() => Dm.locate<obj_machinery_power_solar>(locate(x, Y, 1)));

    private static obj_machinery_power_solar_control Control(AudioRoom r) => r.In(() => Dm.locate<obj_machinery_power_solar_control>(locate(6, Y, 1)));

    private static obj_machinery_power_tracker Tracker(AudioRoom r) => r.In(() => Dm.locate<obj_machinery_power_tracker>(locate(2, Y, 1)));

    private static datum_powernet Net(AudioRoom r) => r.In(() => Dm.locate<obj_structure_cable>(locate(5, Y, 1)).powernet);

    // the sun is put somewhere and tells the machines, as an admin's edit of its azimuth does (sun.dm:28)
    private static void Sun(AudioRoom r, float azimuth) => r.In(() =>
    {
        SSsun.azimuth = azimuth;
        SSsun.complete_movement();
    });

    private static datum_tgui Window(AudioRoom r, mob who, obj_machinery_power_solar_control control) => r.In(() => Dm.@new<datum_tgui>(who, control, "SolarControl", "Solar"));

    private static object Act(AudioRoom r, obj_machinery_power_solar_control control, datum_tgui ui, string action, params (string, object)[] parameters)
        => r.In(() => Waiting.NoSleep(control.ui_act(action, Dm.list(parameters.Select(p => Dm.kv(p.Item1, p.Item2)).ToArray()), ui, null)));

    private static float Num(object value) => Dm.num(value);

    [Fact]
    public void TheSunStartsAtARandomAzimuthAndRateAndMovesOncePerFireTellingTheMachines()
    {
        var (r, _) = Room();
        r.In(() =>
        {
            // Initialize (sun.dm:9): rand(0, 359), 50% to 200% of the base rotation, either way round
            Assert.InRange(SSsun.azimuth, 0, 359);
            Assert.InRange(Math.Abs(SSsun.azimuth_mod), 0.5f, 2f);
            Assert.Equal(6f, SSsun.base_rotation);
            Assert.Equal(600f, SSsun.wait);
            // fire (sun.dm:16): azimuth += azimuth_mod * base_rotation, wrapped to [0, 360)
            SSsun.azimuth = 350;
            SSsun.azimuth_mod = 2;
            Waiting.NoSleep(SSsun.fire());
            Assert.Equal(2f, SSsun.azimuth); // 350 + 12 - 360
            SSsun.azimuth_mod = -1.5f;
            Waiting.NoSleep(SSsun.fire());
            Assert.Equal(353f, SSsun.azimuth); // 2 - 9 + 360
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APanelsOutputFollowsLambertsCosineLawFromTheSunAndItsOwnAngle()
    {
        var (r, _) = Room();
        // nothing is connected yet: the panels stand at azimuth 0, the sun is moved by hand
        foreach ((float sun, float frac) in new[] { (0f, 1f), (60f, 0.5f), (45f, 0.71f), (89f, 0.02f), (90f, 0f), (180f, 0f), (300f, 0.5f) })
        {
            Sun(r, sun);
            MachinesTick(r, 2);
            r.In(() =>
            {
                Assert.Equal(frac, Panel(r, 4).sunfrac, 2); // update_solar_exposure (solar.dm:218): round(cos(angle difference), 0.01) clamped to 0..1
                // process (solar.dm:233): add_avail(power_to_energy(2500 * sunfrac * power_tier)) of each of the three panels
                Assert.Equal(3 * Cycle * frac, Net(r).avail, 0.5);
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABetterGlassMakesAPanelWorthMore()
    {
        var (r, _) = Room();
        Sun(r, 0);
        r.In(() =>
        {
            Panel(r, 3).power_tier = 2;
            Panel(r, 4).power_tier = 3;
            Panel(r, 5).power_tier = 4;
        });
        MachinesTick(r, 3);
        r.In(() => Assert.Equal((2 + 3 + 4) * Cycle, Net(r).avail)); // solar.dm:244: SOLAR_GEN_RATE * sunfrac * power_tier
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenPanelOrOneUnderAnOpaqueTurfMakesNoPowerAndTheTurfsTraitBlocksTheSun()
    {
        var (r, _) = Room();
        Sun(r, 0);
        MachinesTick(r, 3);
        r.In(() => Assert.Equal(3 * Cycle, Net(r).avail));
        // the trait of a turf (space vines give it, vine_structure.dm:221): the panel does not even process (solar.dm:236)
        r.In(() => ADD_TRAIT(Panel(r, 3).loc, TRAIT_TURF_SUN_BLOCKED, "test"));
        MachinesTick(r, 3);
        r.In(() => Assert.Equal(2 * Cycle, Net(r).avail));
        r.In(() => Assert.True(Dm.truthy(((turf)Panel(r, 3).loc).is_sunlight_blocked())));
        // a broken panel stops, and its look says so (solar.dm:153: the -b icon states; atom_break turns it by chance)
        r.In(() => Waiting.NoSleep(Panel(r, 4).atom_break()));
        MachinesTick(r, 3);
        r.In(() =>
        {
            Assert.Equal(Cycle, Net(r).avail);
            Assert.True(Dm.truthy(Dm.bitand(Panel(r, 4).machine_stat, BROKEN)));
            Assert.InRange(Panel(r, 4).azimuth_current, 160, 200);
            Assert.Equal("solar_panel_glass-b", Panel(r, 4).panel.icon_state);
            Assert.Equal("solar_panel_glass-b", Panel(r, 4).panel_edge.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpaqueTurfInTheDirectionOfTheSunBlocksItWithinTwentyTilesAndOneBehindDoesNot()
    {
        // an iron wall east of the panels, at x 8 above the cable run (Y 3): the sun in the east (azimuth 90) is blocked for a panel at the same y
        var (r, _) = Room(extras: w => Dm.@new<turf_closed_wall>(locate(8, Y, 1)));
        r.In(() =>
        {
            turf at5 = (turf)Panel(r, 5).loc;
            Sun(r, 90);
            Assert.True(Dm.truthy(at5.is_sunlight_blocked())); // 3 tiles east: sin(90) = 1, cos(90) rounds to 0 (turfs.dm:563)
            Assert.False(Dm.truthy(at5.is_sunlight_blocked(2f))); // but not within 2 tiles (the occlusion distance, turfs.dm:571)
            Assert.True(Dm.truthy(at5.is_sunlight_blocked(3f)));
            Sun(r, 270);
            Assert.False(Dm.truthy(at5.is_sunlight_blocked())); // the wall is behind it; the map edge ends the walk (turfs.dm:574)
        });
        // update_turn (solar.dm:208) reads the occlusion when the panel turns, and then it makes nothing
        Sun(r, 90);
        r.In(() => Dm.each<obj_machinery_power_solar>(Dm.list(Panel(r, 3), Panel(r, 4), Panel(r, 5))).ToList().ForEach(p => p.queue_turn(90)));
        MachinesTick(r, 3);
        r.In(() =>
        {
            Assert.All(new[] { Panel(r, 3), Panel(r, 4), Panel(r, 5) }, p =>
            {
                Assert.Equal(90f, p.azimuth_current);
                Assert.True(Dm.truthy(p.obscured), "the wall at x 8 is in the way of all three");
                Assert.Equal(0f, p.sunfrac);
            });
            Assert.Equal(0f, Net(r).avail);
        });
        // the wall does not matter when the sun is elsewhere: the panels turn to it again and the exposure comes back
        Sun(r, 270);
        r.In(() => Dm.each<obj_machinery_power_solar>(Dm.list(Panel(r, 3), Panel(r, 4), Panel(r, 5))).ToList().ForEach(p => p.queue_turn(270)));
        MachinesTick(r, 3);
        r.In(() =>
        {
            Assert.False(Dm.truthy(Panel(r, 4).obscured));
            Assert.Equal(1f, Panel(r, 4).sunfrac);
            Assert.Equal(3 * Cycle, Net(r).avail);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePanelsTwoOverlaysTurnByTgsTransformAndTheComputersScreenFollowsItsState()
    {
        var (r, bob) = Room();
        obj_machinery_power_solar_control control = Control(r);
        r.In(() =>
        {
            // the panel's two vis_contents overlays (solar.dm:42-44, :69): the glass edge and the glass, floating 11 and 13 pixels up
            obj_machinery_power_solar panel = Panel(r, 4);
            Assert.Equal("solar_panel_glass_edge", panel.panel_edge.icon_state);
            Assert.Equal("solar_panel_glass", panel.panel.icon_state);
            Assert.Equal(11f, panel.panel_edge.pixel_z);
            Assert.Equal(13f, panel.panel.pixel_z);
            Assert.Contains(panel.panel, Dm.each<atom>(panel.vis_contents));
            Assert.Contains(panel.panel_edge, Dm.each<atom>(panel.vis_contents));
            // get_panel_transform (solar.dm:174): turned, sheared toward east and west, squashed by the cosine; north is skinny, south fat
            matrix north = panel.get_panel_transform(0);
            matrix south = panel.get_panel_transform(180);
            Assert.Equal(0.15f, north.e, 3);
            Assert.Equal(1.0f, Math.Abs(south.e), 3);
            Assert.Equal(1f, north.a, 3);
            Assert.Equal(0f, north.b, 3);
            matrix east = panel.get_panel_transform(90);
            Assert.NotEqual(0f, east.d, 3); // the shear
        });
        // the control's overlays (solar.dm:502): the keyboard and the screen while it has power and is whole
        Assert.Contains("power_key", Overlays(r, control));
        Assert.Contains("solar", Overlays(r, control));
        // visually_turn (solar.dm:204) leaves the panel's transform at its end value, as animate() does
        r.In(() =>
        {
            Panel(r, 4).visually_turn(90);
            matrix end = (matrix)Panel(r, 4).panel.transform;
            matrix want = Panel(r, 4).get_panel_transform(90);
            Assert.Equal(want.a, end.a, 4);
            Assert.Equal(want.d, end.d, 4);
            Assert.Equal(want.e, end.e, 4);
        });
        r.AssertNoRuntimes();
    }

    private static List<string> Overlays(AudioRoom r, atom a)
        => r.In(() => Dm.each(a.managed_overlays is DmList l ? l : Dm.list(a.managed_overlays))
            .Select(o => o switch { string t => t, mutable_appearance m => (string)m.icon_state, DmAppearance d => (string)d.icon_state, _ => null }).Where(t => t != null).ToList());

    [Fact]
    public void TheComputerFindsThePanelsAndTheTrackerOnItsNetworkOnlyWhenAskedToScan()
    {
        var (r, bob) = Room();
        obj_machinery_power_solar_control control = Control(r);
        r.In(() =>
        {
            // Initialize (solar.dm:444): no scan; the computer's rate and history interval are the sun's
            Assert.Equal(0f, control.connected_panels.len);
            Assert.Null(control.connected_tracker);
            Assert.Equal(6f, control.azimuth_rate);
            Assert.Equal(600f, control.record_interval);
            Assert.Equal(2f, control.history.len);
        });
        datum_tgui ui = Window(r, bob, control);
        Assert.True(Dm.truthy(Act(r, control, ui, "refresh"))); // search_for_connected (solar.dm:464)
        r.In(() =>
        {
            Assert.Equal(3f, control.connected_panels.len);
            Assert.Same(Tracker(r), control.connected_tracker);
            Assert.Same(control, Tracker(r).control);
            Assert.Equal(3 * Rate, Num(control.total_capacity)); // set_control (solar.dm:81)
            Assert.All(new[] { Panel(r, 3), Panel(r, 4), Panel(r, 5) }, p => Assert.Same(control, p.control));
            // ui_data (solar.dm:520)
            DmList data = (DmList)Waiting.NoSleep(control.ui_data(bob));
            Assert.Equal(3f, Num(data["connected_panels"]));
            Assert.Equal(1f, Num(data["connected_tracker"]));
            Assert.Equal(3 * Rate, Num(data["capacity"]));
            Assert.Equal(12f, Num(data["max_rotation_rate"]));
            Assert.Equal(0f, Num(data["tracking_state"]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheComputerOffersManualTimedAndAutoTrackingAndItsPanelsTurnWhereItSays()
    {
        var (r, bob) = Room();
        obj_machinery_power_solar_control control = Control(r);
        datum_tgui ui = Window(r, bob, control);
        Act(r, control, ui, "refresh");

        // "azimuth" (solar.dm:532): a value, or an adjust of the target; the panels turn on their next process; 360 wraps, -90 is 270
        Assert.True(Dm.truthy(Act(r, control, ui, "azimuth", ("value", 270))));
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.Equal(270f, control.azimuth_target);
            Assert.All(new[] { Panel(r, 3), Panel(r, 4), Panel(r, 5) }, p => Assert.Equal(270f, p.azimuth_current));
            Assert.Equal(1f, Panel(r, 4).sunfrac);
            Assert.Equal(3 * Cycle, Net(r).avail);
            Assert.Equal(3 * Rate, control.lastgen); // process (solar.dm:612): the watts the panels added up in the last cycle
        });
        Assert.True(Dm.truthy(Act(r, control, ui, "azimuth", ("adjust", 100))));
        r.In(() => Assert.Equal(10f, control.azimuth_target)); // 370 - 360
        Assert.True(Dm.truthy(Act(r, control, ui, "azimuth", ("adjust", -20))));
        r.In(() => Assert.Equal(350f, control.azimuth_target)); // -10 + 360
        Assert.False(Dm.truthy(Act(r, control, ui, "azimuth"))); // neither: FALSE (solar.dm:540)
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.Equal(350f, Panel(r, 5).azimuth_current);
            Assert.Equal(0.17f, Panel(r, 5).sunfrac, 2); // the sun at 270 is 80 degrees off: cos(80) rounds to 0.17
            Assert.Equal(3 * Cycle * 0.17f, Net(r).avail, 0.5);
        });
        // 90 degrees off and more is nothing
        Act(r, control, ui, "azimuth", ("value", 90));
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.Equal(0f, Panel(r, 4).sunfrac);
            Assert.Equal(0f, Net(r).avail);
        });

        // "azimuth_rate" (solar.dm:546): clamped to twice the base rotation, to 0.01
        Assert.True(Dm.truthy(Act(r, control, ui, "azimuth_rate", ("value", 99))));
        r.In(() => Assert.Equal(12f, control.azimuth_rate));
        Assert.True(Dm.truthy(Act(r, control, ui, "azimuth_rate", ("adjust", -24.567))));
        r.In(() => Assert.Equal(-12f, control.azimuth_rate));
        Assert.True(Dm.truthy(Act(r, control, ui, "azimuth_rate", ("value", 1.2345))));
        r.In(() => Assert.Equal(1.23f, control.azimuth_rate, 3));

        // "tracking" (solar.dm:556): timed mode adds the rate to the target at every move of the sun
        r.In(() => { control.azimuth_target = 100; control.azimuth_rate = 6; });
        Assert.True(Dm.truthy(Act(r, control, ui, "tracking", ("mode", SOLAR_TRACK_TIMED))));
        r.In(() =>
        {
            Assert.Equal(1f, control.track);
            Waiting.NoSleep(SSsun.fire()); // COMSIG_SUN_MOVED -> timed_track (solar.dm:620)
            Assert.Equal(106f, control.azimuth_target);
            Assert.Equal(106f, Panel(r, 4).azimuth_target);
            Waiting.NoSleep(SSsun.fire());
            Assert.Equal(112f, control.azimuth_target);
        });
        // off: the sun's move changes nothing
        Act(r, control, ui, "tracking", ("mode", SOLAR_TRACK_OFF));
        r.In(() =>
        {
            Assert.Equal(0f, control.track);
            Waiting.NoSleep(SSsun.fire());
            Assert.Equal(112f, control.azimuth_target);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTrackerTurnsTheComputersPanelsAfterTheSunAndAutoNeedsATracker()
    {
        var (r, bob) = Room();
        obj_machinery_power_solar_control control = Control(r);
        datum_tgui ui = Window(r, bob, control);
        Act(r, control, ui, "refresh");

        // auto (solar.dm:556): the tracker reports the sun's azimuth to the computer at once (tracker.dm:111) ...
        Sun(r, 265);
        Assert.True(Dm.truthy(Act(r, control, ui, "tracking", ("mode", SOLAR_TRACK_AUTO))));
        r.In(() =>
        {
            Assert.Equal(2f, control.track);
            Assert.Equal(265f, control.azimuth_target);
            Assert.Equal(265f, Num(Tracker(r).azimuth_current));
            // the dish's two overlays turned with it (tracker.dm:84: skinny on the floor plane)
            matrix dish = (matrix)Tracker(r).tracker_dish.transform, want = Tracker(r).get_tracker_transform(265);
            Assert.Equal(want.a, dish.a, 4);
            Assert.Equal(want.e, dish.e, 4);
            Assert.Equal(want.d, dish.d, 4);
        });
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.All(new[] { Panel(r, 3), Panel(r, 4), Panel(r, 5) }, p => Assert.Equal(265f, p.azimuth_current));
            Assert.Equal(3 * Cycle, Net(r).avail); // the panels face the sun
        });
        // ... and at every move of the sun
        r.In(() =>
        {
            SSsun.azimuth = 268;
            Waiting.NoSleep(SSsun.fire());
            Assert.Equal(274f, control.azimuth_target);
        });
        MachinesTick(r, 2);
        r.In(() =>
        {
            Assert.Equal(274f, Panel(r, 3).azimuth_current);
            Assert.Equal(3 * Cycle, Net(r).avail);
        });
        // losing the tracker turns auto off (tracker.dm:69)
        r.In(() => qdel(Tracker(r)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(0f, control.track);
            Assert.Null(control.connected_tracker);
        });
        // and auto without a tracker falls back to off (solar.dm:556)
        Assert.True(Dm.truthy(Act(r, control, ui, "tracking", ("mode", SOLAR_TRACK_AUTO))));
        r.In(() => Assert.Equal(0f, control.track));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AControllerKeepsAHistoryOfItsSupplyAndCapacityEveryMinute()
    {
        var (r, bob) = Room();
        obj_machinery_power_solar_control control = Control(r);
        datum_tgui ui = Window(r, bob, control);
        Act(r, control, ui, "refresh");
        Sun(r, 0);
        Act(r, control, ui, "azimuth", ("value", 0));
        // process (solar.dm:612): lastgen is the watts of the last cycle; record (solar.dm:484) appends it once per record_interval
        r.Step(130 * Second);
        r.In(() =>
        {
            DmList supply = (DmList)control.history["supply"], capacity = (DmList)control.history["capacity"];
            Assert.InRange(supply.len, 2, 3);
            Assert.Equal(supply.len, capacity.len);
            Assert.Equal(3 * Rate, Num(supply[(float)supply.len]));
            Assert.Equal(3 * Rate, Num(capacity[(float)capacity.len]));
            Assert.InRange(control.record_size, 1 + 360 / (6 * 2), 1 + 360 / (6 * 0.5f) + 1); // a full sun cycle of records at the sun's rate (solar.dm:486)
        });
        r.AssertNoRuntimes();
    }

    // What the player holds: the old items go, the new one is put in his active hand (WallTests.Hold, for a type known only at run time).
    private static T Give<T>(AudioRoom r, mob_living_carbon_human who, Type type = null, float? amount = null) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        Type made = type ?? typeof(T);
        T item = (T)(amount == null ? Dm.@new(made, get_turf(who)) : Dm.@new(made, get_turf(who), amount.Value, FALSE));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    private static obj_item_solar_assembly Assembly(AudioRoom r, mob_living_carbon_human who, int x, bool anchored = true)
    {
        r.In(() => Dm.@new<turf_open_floor_plating>(locate(x, Y + 1, 1)));
        obj_item_solar_assembly assembly = r.In(() =>
        {
            var a = Dm.@new<obj_item_solar_assembly>(locate(x, Y, 1));
            if (anchored)
                Waiting.NoSleep(a.set_anchored(TRUE));
            return a;
        });
        r.In(() => Waiting.NoSleep(who.forceMove(locate(x, Y + 1, 1))));
        r.Step(Second);
        return assembly;
    }

    [Fact]
    public void AnAssemblyIsWrenchedDownGetsGlassAndMakesAPanelWorthItsGlassesTier()
    {
        var (r, bob) = Room(tracker: false, control: false);
        // an assembly at (7, 4) on the cable: loose, it lies a few pixels off; wrenched down it is straight and anchored (solar.dm:380)
        obj_item_solar_assembly assembly = Assembly(r, bob, 7, anchored: false);
        r.In(() =>
        {
            Assert.Equal(0f, assembly.anchored);
            Assert.Equal("sp_base", assembly.icon_state);
            Assert.InRange(assembly.pixel_x, -6, 6);
        });
        // glass on the loose assembly is refused
        var loose = Give<obj_item_stack_sheet_glass>(r, bob, amount: 5);
        WallTests.Click(r, bob, assembly, 0.5f);
        r.In(() =>
        {
            Assert.Null(Dm.locate<obj_machinery_power_solar>(locate(7, Y, 1)));
            Assert.Equal(5f, loose.amount);
        });
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, assembly, 0.5f);
        r.In(() =>
        {
            Assert.True(Dm.truthy(assembly.anchored));
            Assert.Equal(0f, assembly.pixel_x);
            Assert.Equal(0f, assembly.pixel_y);
        });

        // glass on the wrenched one makes the panel and takes two sheets
        var sheets = Give<obj_item_stack_sheet_glass>(r, bob, amount: 5);
        WallTests.Click(r, bob, assembly, 0.5f);
        r.Step(Second);
        r.In(() =>
        {
            obj_machinery_power_solar made = Dm.locate<obj_machinery_power_solar>(locate(7, Y, 1));
            Assert.NotNull(made);
            Assert.Equal(1f, made.power_tier);
            Assert.Equal(typeof(datum_material_glass), made.material_type);
            Assert.Equal(3f, sheets.amount);
            Assert.Equal("solar_panel_glass", made.panel.icon_state);
            Assert.Equal("solar_panel_glass_edge", made.panel_edge.icon_state);
            Assert.Same(made, assembly.loc); // Make (solar.dm:96): the assembly sits inside the panel
            Assert.NotNull(made.powernet); // connected to the cable under it
            Assert.True(Dm.truthy(made.should_have_node()));
        });
        // the wrench unwrenches only an assembly: a panel is not one
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GlassOfEveryKindMakesItsTierAndReinforcedGlassIsRefused()
    {
        var (r, bob) = Room(tracker: false, control: false);
        // reinforced glass is refused with tg's words and nothing is built
        obj_item_solar_assembly refused = Assembly(r, bob, 7);
        var rglass = Give<obj_item_stack_sheet>(r, bob, typeof(obj_item_stack_sheet_rglass), 5);
        WallTests.Click(r, bob, refused, 0.5f);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(Dm.locate<obj_machinery_power_solar>(locate(7, Y, 1)));
            Assert.Equal(5f, rglass.amount);
        });
        (Type glass, Type material, float tier, string name)[] kinds =
        {
            (typeof(obj_item_stack_sheet_titaniumglass), typeof(datum_material_alloy_titaniumglass), 2f, "titanium glass"),
            (typeof(obj_item_stack_sheet_plasmaglass), typeof(datum_material_alloy_plasmaglass), 3f, "plasmaglass"),
            (typeof(obj_item_stack_sheet_plastitaniumglass), typeof(datum_material_alloy_plastitaniumglass), 4f, "plastitanium glass"),
        };
        int x = 8;
        foreach (var (glass, material, tier, name) in kinds)
        {
            int px = x++;
            obj_item_solar_assembly assembly = Assembly(r, bob, px);
            var sheets = Give<obj_item_stack_sheet>(r, bob, glass, 2);
            WallTests.Click(r, bob, assembly, 0.5f);
            r.Step(Second);
            r.In(() =>
            {
                obj_machinery_power_solar made = Dm.locate<obj_machinery_power_solar>(locate(px, Y, 1));
                Assert.NotNull(made);
                Assert.Equal(tier, made.power_tier);
                Assert.Equal(material, made.material_type);
                Assert.Equal("solar_panel_" + name, made.panel.icon_state);
                Assert.Equal("solar_panel_" + name + "_edge", made.panel_edge.icon_state);
                Assert.True(QDELETED(sheets), "two sheets are all it takes");
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATrackerIsBuiltFromAnAssemblyWithItsElectronicsAndGlassAndTheCrowbarTakesBothApart()
    {
        var (r, bob) = Room(tracker: false, control: false);
        obj_item_solar_assembly assembly = Assembly(r, bob, 7);
        // the tracker electronics go in (solar.dm:299): the assembly becomes a tracker base
        var electronics = Give<obj_item_electronics_tracker>(r, bob);
        WallTests.Click(r, bob, assembly, 0.5f);
        r.In(() =>
        {
            Assert.True(Dm.truthy(assembly.tracker));
            Assert.Equal("tracker_base", assembly.icon_state);
            Assert.True(QDELETED(electronics));
        });
        // reinforced glass is refused (the tracker takes standard glass only), standard glass completes it
        var rglass = Give<obj_item_stack_sheet>(r, bob, typeof(obj_item_stack_sheet_rglass), 5);
        WallTests.Click(r, bob, assembly, 0.5f);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(Dm.locate<obj_machinery_power_tracker>(locate(7, Y, 1)));
            Assert.Equal(5f, rglass.amount);
        });
        var glass = Give<obj_item_stack_sheet_glass>(r, bob, amount: 5);
        WallTests.Click(r, bob, assembly, 0.5f);
        r.Step(Second);
        obj_machinery_power_tracker tracker = r.In(() => Dm.locate<obj_machinery_power_tracker>(locate(7, Y, 1)));
        Assert.NotNull(tracker);
        r.In(() =>
        {
            Assert.Equal(3f, glass.amount);
            Assert.Equal("tracker_edge", tracker.tracker_dish_edge.icon_state);
            Assert.Equal("tracker", tracker.tracker_dish.icon_state);
            Assert.NotNull(tracker.powernet);
            Assert.Same(tracker, assembly.loc);
        });

        // a crowbar takes the glass off: the assembly (still a tracker base) and two sheets of glass fall out (tracker.dm:130, :143)
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, tracker, 0.5f);
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(tracker));
            turf here = locate(7, Y, 1);
            Assert.Same(here, assembly.loc);
            Assert.Equal(2f, Dm.each<obj_item_stack_sheet_glass>(here).Sum(g => g.amount)); // two sheets, in however many stacks
        });
        // and a crowbar on the assembly takes the electronics out again (solar.dm:403)
        WallTests.Click(r, bob, assembly, 0.5f);
        r.In(() =>
        {
            Assert.Equal(0f, assembly.tracker ?? 0);
            Assert.Equal("sp_base", assembly.icon_state);
            Assert.NotEmpty(Dm.each<obj_item_electronics_tracker>(locate(7, Y, 1)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrowbarTakesTheGlassOffAPanelAndBreakingItLeavesShards()
    {
        var (r, bob) = Room();
        Sun(r, 0);
        WallTests.Hold<obj_item_crowbar>(r, bob);
        obj_machinery_power_solar panel = Panel(r, 4);
        obj_item_solar_assembly assembly = r.In(() => Dm.locate<obj_item_solar_assembly>(panel));
        Assert.NotNull(assembly);
        WallTests.Click(r, bob, panel, 0.5f);
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(panel));
            turf here = locate(4, Y, 1);
            Assert.Same(here, assembly.loc);
            Assert.Equal(2f, Dm.each<obj_item_stack_sheet_glass>(here).Sum(g => g.amount)); // on_deconstruction (solar.dm:132)
            Assert.Null(Dm.locate<obj_machinery_power_solar>(here));
        });
        MachinesTick(r, 3);
        r.In(() => Assert.Equal(2 * Cycle, Net(r).avail)); // two panels left

        // a panel hit to bits drops two shards and no sheets
        obj_machinery_power_solar other = Panel(r, 5);
        r.In(() => Waiting.NoSleep(other.deconstruct(FALSE)));
        r.Step(2);
        r.In(() =>
        {
            turf there = locate(5, Y, 1);
            Assert.Equal(2f, Dm.each<obj_item_shard>(there).Count());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACutCableLetsThePanelsAndTheTrackerGoFromTheComputer()
    {
        var (r, bob) = Room();
        obj_machinery_power_solar_control control = Control(r);
        datum_tgui ui = Window(r, bob, control);
        Act(r, control, ui, "refresh");
        r.In(() => Assert.Equal(3f, control.connected_panels.len));
        Act(r, control, ui, "tracking", ("mode", SOLAR_TRACK_AUTO));
        // the cable under the last panel is cut: the network splits, the computer is on the far side from the panels and the tracker, which
        // leave it at their next process (solar.dm:236, :612)
        r.In(() => Waiting.NoSleep(Dm.locate<obj_structure_cable>(locate(5, Y, 1)).deconstruct(TRUE)));
        MachinesTick(r, 3);
        r.In(() =>
        {
            Assert.Equal(0f, control.connected_panels.len);
            Assert.Null(control.connected_tracker);
            Assert.Equal(0f, control.track); // the tracker's unset_control turns auto off (tracker.dm:69)
            Assert.Null(Panel(r, 3).control);
        });
        r.AssertNoRuntimes();
    }
}
