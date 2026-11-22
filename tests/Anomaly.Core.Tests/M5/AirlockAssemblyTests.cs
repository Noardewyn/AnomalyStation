using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M4aAccess.P5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 6: tg's airlock construction and deconstruction (game/objects/structures/door_assembly.dm, airlock_electronics.dm,
/// machinery/doors/airlock.dm) in a booted toy room, by a joined player's clicks: the assembly's steps in order (wrench, cable,
/// electronics, screwdriver) with their names and construction overlays, and each step taken back; the built airlock's access
/// from the electronics' window acts; an unpowered airlock taken apart into its assembly and electronics; the loose assembly
/// welded apart into 4 iron; the plasteel reinforcement layers; a broken airlock's open panel and cut wires.
/// </summary>
public class AirlockAssemblyTests
{
    private const int Second = 20;
    private const int X = 5, Y = 5;

    private static mob_living_carbon_human Builder(AudioRoom r)
    {
        mob_living_carbon_human bob = r.Join(X - 1, Y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return bob;
    }

    private static List<T> On<T>(AudioRoom r, int x = X, int y = Y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static List<T> Near<T>(AudioRoom r) => On<T>(r, X, Y).Concat(On<T>(r, X - 1, Y)).ToList();

    private static float Total<T>(AudioRoom r) where T : obj_item_stack => r.In(() => Near<T>(r).Sum(s => Dm.num(s.get_amount())));

    /// <summary>Puts an existing item in the builder's hand, the hands emptied first.</summary>
    private static void Give(AudioRoom r, mob_living_carbon_human who, obj_item item) => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old && old != item)
                qdel(old);
        }
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
    });

    private static obj_structure_door_assembly Assembly(AudioRoom r)
        => r.In(() => Dm.@new<obj_structure_door_assembly>(locate(X, Y, 1)));

    private static List<string> OverlayStates(AudioRoom r, atom a)
        => r.In(() => Dm.each(a.managed_overlays is DmList l ? l : Dm.list(a.managed_overlays)).Select(Describe).Where(s => s != null).ToList());

    private static string Describe(object o) => o switch
    {
        string s => s,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        null => null,
        _ => o.GetType().Name + ":" + o,
    };

    private static obj_machinery_door_airlock Build(AudioRoom r, mob_living_carbon_human bob, obj_item_electronics_airlock electronics)
    {
        var assembly = Assembly(r);
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 5);
        WallTests.Click(r, bob, assembly, 5);
        Give(r, bob, electronics);
        WallTests.Click(r, bob, assembly, 5);
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        return Assert.Single(On<obj_machinery_door_airlock>(r));
    }

    [Fact]
    public void TheAssemblyIsBuiltStepByStepAndEveryStepComesBack()
    {
        var r = new AudioRoom(10, 10, seed: 5601);
        mob_living_carbon_human bob = Builder(r);
        var assembly = Assembly(r);
        r.In(() =>
        {
            Assert.Equal("Airlock assembly", assembly.name);
            Assert.False(Dm.truthy(assembly.anchored));
            Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_WIRES, Dm.num(assembly.state));
        });
        Assert.Contains("fill_construction", OverlayStates(r, assembly));
        Assert.Contains("panel_c1", OverlayStates(r, assembly));

        // cable on a loose assembly does nothing (door_assembly.dm:87)
        var cable = WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 5);
        WallTests.Click(r, bob, assembly, 5);
        Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_WIRES, r.In(() => Dm.num(assembly.state)));

        // wrench: anchored in 4 s, "secured"
        WallTests.Hold<obj_item_wrench>(r, bob);
        r.Sounds.Heard.Clear();
        WallTests.Click(r, bob, assembly, 1);
        Assert.False(r.In(() => Dm.truthy(assembly.anchored)));
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(assembly.anchored));
            Assert.Equal("secured Airlock assembly", assembly.name);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.StartsWith("sound/items/tools/ratchet", StringComparison.Ordinal));

        // cable: wired, one cable used
        cable = WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 5);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() =>
        {
            Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, Dm.num(assembly.state));
            Assert.Equal("wired Airlock assembly", assembly.name);
            Assert.Equal(4f, Dm.num(cable.get_amount()));
        });
        Assert.Contains("panel_c2", OverlayStates(r, assembly));

        // electronics: inside the assembly, "near finished"
        var electronics = r.In(() => Dm.@new<obj_item_electronics_airlock>(get_turf(bob)));
        Give(r, bob, electronics);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() =>
        {
            Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER, Dm.num(assembly.state));
            Assert.Equal("near finished Airlock assembly", assembly.name);
            Assert.Same(assembly, electronics.loc);
            Assert.Same(electronics, assembly.electronics);
        });
        Assert.Contains("panel_c3", OverlayStates(r, assembly));

        // glass: window assembly, the glass overlay instead of the fill
        var glass = WallTests.Hold<obj_item_stack_sheet_glass>(r, bob, 5);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() =>
        {
            Assert.True(Dm.truthy(assembly.glass));
            Assert.Equal("near finished window Airlock assembly", assembly.name);
            Assert.Equal(4f, Dm.num(glass.get_amount()));
        });
        Assert.Contains("glass_construction", OverlayStates(r, assembly));

        // back: the welder takes the glass out, the crowbar the electronics, the wirecutters the cable, the wrench the bolts
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() => Assert.False(Dm.truthy(assembly.glass)));
        Assert.Equal(1f, Total<obj_item_stack_sheet_glass>(r));

        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() =>
        {
            Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, Dm.num(assembly.state));
            Assert.Null(assembly.electronics);
            Assert.Same(get_turf(assembly), electronics.loc);
        });

        WallTests.Hold<obj_item_wirecutters>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() => Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_WIRES, Dm.num(assembly.state)));
        Assert.Equal(1f, Total<obj_item_stack_cable_coil>(r));

        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        r.In(() =>
        {
            Assert.False(Dm.truthy(assembly.anchored));
            Assert.Equal("Airlock assembly", assembly.name);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheElectronicsWindowSetsTheBuiltAirlocksAccess()
    {
        var r = new AudioRoom(10, 10, seed: 5602);
        mob_living_carbon_human bob = Builder(r);
        var electronics = r.In(() => Dm.@new<obj_item_electronics_airlock>(get_turf(bob)));
        r.In(() =>
        {
            // the window's acts (ui_act -> do_action; the window itself is the gameplay test's): without an open window
            // tg's ui_act refuses (external.dm:103)
            Assert.Equal(TRUE, Dm.num(Waiting.NoSleep(electronics.ui_act("set", Dm.list(Dm.kv("access", ACCESS_AUX_BASE))))));
            Assert.Empty(Dm.each(electronics.accesses));
            electronics.do_action("set", Dm.list(Dm.kv("access", ACCESS_AUX_BASE)));
            electronics.do_action("set", Dm.list(Dm.kv("access", ACCESS_ENGINEERING)));
            electronics.do_action("set", Dm.list(Dm.kv("access", ACCESS_ENGINEERING))); // a second click takes it back
            electronics.do_action("passedName", Dm.list(Dm.kv("passedName", "Aux Door")));
            electronics.do_action("direc_set", Dm.list(Dm.kv("unres_direction", "4")));
            var data = (DmList)Waiting.NoSleep(electronics.ui_data(bob));
            Assert.Equal(new object[] { ACCESS_AUX_BASE }, Dm.each((DmList)data["accesses"]).ToArray());
            Assert.Equal(0f, Dm.num(data["oneAccess"]));
            Assert.Equal(4f, Dm.num(data["unres_direction"]));
            Assert.Equal("Aux Door", data["passedName"]);
        });

        var door = Build(r, bob, electronics);
        r.In(() =>
        {
            Assert.IsType<obj_machinery_door_airlock>(door);
            Assert.Equal("Aux Door", door.name);
            Assert.Equal(new object[] { ACCESS_AUX_BASE }, Dm.each((DmList)door.req_access).ToArray());
            Assert.Equal(4f, door.unres_sides);
            Assert.True(Dm.truthy(door.unres_latch));
            Assert.Same(door, electronics.loc);
            Assert.True(Dm.truthy(door.density), "the finished airlock closes (door_assembly.dm:380)");
            Assert.Empty(On<obj_structure_door_assembly>(r));
        });

        // without the access the airlock refuses, with a card it lets through
        r.In(() =>
        {
            foreach (object held in Dm.each(bob.held_items.Copy()))
                qdel(held as datum);
        });
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(door.allowed__proc(bob)))));
        r.In(() => IdCards.Hold(bob, ACCESS_AUX_BASE));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(door.allowed__proc(bob)))));

        // one access: req_one_access instead
        var second = r.In(() => Dm.@new<obj_item_electronics_airlock>(get_turf(bob)));
        r.In(() =>
        {
            second.do_action("one_access", null);
            second.do_action("set", Dm.list(Dm.kv("access", ACCESS_MAINT_TUNNELS)));
            second.do_action("set", Dm.list(Dm.kv("access", ACCESS_ENGINEERING)));
            qdel(door);
        });
        r.Step(1);
        var door2 = Build(r, bob, second);
        r.In(() =>
        {
            Assert.Equal(new object[] { ACCESS_MAINT_TUNNELS, ACCESS_ENGINEERING }, Dm.each((DmList)door2.req_one_access).ToArray());
            Assert.False(Dm.truthy(length(door2.req_access)));
            Assert.Equal("Airlock", door2.name);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnpoweredWeldedAirlockComesApartIntoItsAssemblyAndElectronics()
    {
        var r = new AudioRoom(10, 10, seed: 5603);
        mob_living_carbon_human bob = Builder(r);
        var electronics = r.In(() => Dm.@new<obj_item_electronics_airlock>(get_turf(bob)));
        r.In(() => electronics.do_action("set", Dm.list(Dm.kv("access", ACCESS_AUX_BASE))));
        var door = Build(r, bob, electronics);
        r.In(() => IdCards.Hold(bob, ACCESS_AUX_BASE));

        // the panel (screwdriver) and a weld; a powered airlock keeps its electronics (airlock.dm:1252)
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, door, 2);
        r.In(() => Assert.True(Dm.truthy(door.panel_open)));
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        r.Command(() => ClientSeam.Click(bob.client, door, "right=1;button=right;icon-x=16;icon-y=16")); // welds shut (door.dm try_to_weld_secondary)
        r.Step(6 * Second);
        r.In(() => Assert.True(Dm.truthy(door.welded)));
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, door, 6);
        Assert.Single(On<obj_machinery_door_airlock>(r));

        // the area's environment power off, as an APC's breaker does (areas.dm:519)
        r.In(() =>
        {
            area a = (area)get_area(door);
            a.requires_power = TRUE;
            a.power_environ = FALSE; // doors draw from the environment channel (door.dm power_channel)
            a.power_change();
            Assert.False(Dm.truthy(door.hasPower()));
        });
        WallTests.Click(r, bob, door, 6);
        Assert.Empty(On<obj_machinery_door_airlock>(r));
        var assembly = Assert.Single(On<obj_structure_door_assembly>(r));
        r.In(() =>
        {
            Assert.True(Dm.truthy(assembly.anchored));
            Assert.Equal(AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS, Dm.num(assembly.state));
            Assert.Equal("Airlock", assembly.created_name);
            Assert.Equal("wired Airlock assembly", assembly.name);
            Assert.Same(get_turf(assembly), electronics.loc);
            Assert.Equal(new object[] { ACCESS_AUX_BASE }, Dm.each(electronics.accesses).ToArray());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALooseAssemblyIsWeldedApartIntoFourIron()
    {
        var r = new AudioRoom(10, 10, seed: 5604);
        mob_living_carbon_human bob = Builder(r);
        var assembly = Assembly(r);

        // anchored without glass, the welder skips to the attack (door_assembly.dm:194)
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        Assert.Single(On<obj_structure_door_assembly>(r));

        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, assembly, 5);
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, assembly, 1);
        Assert.Single(On<obj_structure_door_assembly>(r));
        r.Step(4 * Second);
        Assert.Empty(On<obj_structure_door_assembly>(r));
        Assert.Equal(4f, Total<obj_item_stack_sheet_iron>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PlasteelReinforcesTheAirlockAndItsLayersComeOffInTgsOrder()
    {
        var r = new AudioRoom(10, 10, seed: 5605);
        mob_living_carbon_human bob = Builder(r);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(X, Y, 1)));
        r.Step(Second);
        float max = r.In(() => door.max_integrity);
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, door, 2);

        var plasteel = WallTests.Hold<obj_item_stack_sheet_plasteel>(r, bob, 5);
        WallTests.Click(r, bob, door, 3);
        r.In(() =>
        {
            Assert.Equal(6f, door.security_level);
            Assert.Equal(max * 1.5f, door.max_integrity, 3);
            Assert.Equal(30f, door.damage_deflection);
            Assert.Equal(3f, Dm.num(plasteel.get_amount()));
        });

        WallTests.Hold<obj_item_wirecutters>(r, bob);
        WallTests.Click(r, bob, door, 2);
        Assert.Equal(5f, r.In(() => door.security_level));
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, door, 5);
        Assert.Equal(4f, r.In(() => door.security_level));
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, door, 5);
        Assert.Equal(3f, r.In(() => door.security_level));
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, door, 5);
        Assert.Equal(2f, r.In(() => door.security_level));
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, door, 5);
        r.In(() =>
        {
            Assert.Equal(0f, door.security_level);
            Assert.Equal(max, door.max_integrity, 3);
            Assert.Equal(21f, door.damage_deflection);
        });
        Assert.Equal(2f, Total<obj_item_stack_sheet_plasteel>(r));

        // iron: one welded layer giving 2 iron back
        WallTests.Hold<obj_item_stack_sheet_iron>(r, bob, 5);
        WallTests.Click(r, bob, door, 3);
        Assert.Equal(1f, r.In(() => door.security_level));
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, door, 5);
        Assert.Equal(0f, r.In(() => door.security_level));
        Assert.Equal(2f, Total<obj_item_stack_sheet_iron>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenAirlockOpensItsPanelCutsItsWiresAndSparks()
    {
        var r = new AudioRoom(10, 10, seed: 5606);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(X, Y, 1)));
        r.Step(Second);
        Assert.DoesNotContain("sparks_damaged", OverlayStates(r, door));
        // below 75% the powered, closed airlock shows tg's damaged sparks (airlock.dm:614, take_damage :1645)
        r.In(() => Waiting.NoSleep(door.take_damage(0.3f * door.max_integrity, BRUTE, 0, 0)));
        Assert.Contains("sparks_damaged", OverlayStates(r, door));
        r.In(() =>
        {
            Assert.False(Dm.truthy(door.panel_open));
            Waiting.NoSleep(door.take_damage(Dm.num(door.atom_integrity) - door.integrity_failure * door.max_integrity + 1, BRUTE, 0, 0));
        });
        r.Step(1);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Dm.bitand(door.machine_stat, BROKEN)));
            Assert.True(Dm.truthy(door.panel_open), "on_break opens the panel (airlock.dm:1611)");
            Assert.True(Dm.truthy(door.wires.is_all_cut()), "on_break cuts every wire (airlock.dm:1612)");
            Assert.True(Dm.truthy(door.locked), "the cut bolt wire drops the bolts (wires/airlock.dm:179)");
            Assert.False(Dm.truthy(door.hasPower()));
        });
        // unpowered, the broken sparks overlay goes out with the power (airlock.dm:610)
        Assert.DoesNotContain("sparks_broken", OverlayStates(r, door));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWindoorComesOutIntoItsAssemblyGoesBackAndBreaksIntoItsDebris()
    {
        var r = new AudioRoom(10, 10, seed: 5607);
        var windoor = r.In(() => Dm.@new<obj_machinery_door_window>(locate(X, Y, 1)));
        mob_living_carbon_human bob = r.Join(X, Y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        r.In(() => Waiting.Detach(windoor.open()));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(windoor.density)));

        // the screwdriver opens the panel of the open windoor, the crowbar takes the electronics out into an assembly (windowdoor.dm:407)
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        WallTests.Click(r, bob, windoor, 2);
        Assert.True(r.In(() => Dm.truthy(windoor.panel_open)));
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, windoor, 6);
        Assert.Empty(On<obj_machinery_door_window>(r));
        var assembly = Assert.Single(On<obj_structure_windoor_assembly>(r));
        var electronics = Assert.Single(On<obj_item_electronics_airlock>(r));
        r.In(() =>
        {
            Assert.True(Dm.truthy(assembly.anchored));
            Assert.True(Dm.truthy(assembly.cables_added));
            Assert.Equal("l", assembly.facing);
            Assert.Equal("l_windoor_assembly02", assembly.icon_state);
        });

        // the electronics back in, the crowbar pries the windoor into the frame (windoor_assembly.dm:310, :347)
        Give(r, bob, electronics);
        WallTests.Click(r, bob, assembly, 5);
        Assert.Same(assembly, r.In(() => electronics.loc));
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, assembly, 6);
        Assert.Empty(On<obj_structure_windoor_assembly>(r));
        var rebuilt = Assert.Single(On<obj_machinery_door_window>(r));
        r.In(() =>
        {
            Assert.Equal("left", rebuilt.base_state);
            Assert.Same(rebuilt, electronics.loc);
        });

        // destroyed: tg's shatter, two shards, two rods and a cable (windowdoor.dm:335)
        r.In(() => Waiting.NoSleep(rebuilt.take_damage(1000, BRUTE, 0, 0)));
        r.Step(Second);
        Assert.Empty(On<obj_machinery_door_window>(r));
        Assert.Equal(2, On<obj_item_shard>(r).Count);
        Assert.Equal(2f, r.In(() => On<obj_item_stack_rods>(r).Sum(s => Dm.num(s.get_amount()))));
        Assert.Equal(1f, r.In(() => On<obj_item_stack_cable_coil>(r).Sum(s => Dm.num(s.get_amount()))));
        r.AssertNoRuntimes();
    }
}
