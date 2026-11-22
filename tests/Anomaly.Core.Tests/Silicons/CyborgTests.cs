using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI2 (D326): the cyborgs. A cyborg picks a model and gets its modules (the engineering model's cyborg RCD builds from its
/// cell); a recharging station charges the cyborg inside it; the robotics console and the lockdown wire lock it down; a hat thrown at it
/// lands on its head; its laser draws the shot from its cell; the GPS sees the other GPS units; a marker beacon is placed and taken back;
/// the service cyborg's chisel carves a statue of a target out of a block.
/// </summary>
public class CyborgTests
{
    private const int Second = 20;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.In(() =>
        {
            Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant)));
            // the toy room has no APC: free power
            (get_area(locate(5, 5, 1)) as area).requires_power = FALSE;
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static mob_living_silicon_robot Borg(AudioRoom r, int x, int y, Type model = null)
    {
        var borg = r.In(() => Dm.@new<mob_living_silicon_robot>(locate(x, y, 1)));
        r.Step();
        if (model != null)
            r.In(() => Waiting.NoSleep(borg.model.transform_to(model, forced: TRUE, transform: FALSE)));
        r.Step(Second);
        return borg;
    }

    private static obj_item_stock_parts_power_store Cell(AudioRoom r, mob_living_silicon_robot borg) => r.In(() => (obj_item_stock_parts_power_store)borg.cell);

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(mob_living_silicon_robot), typeof(obj_item_robot_model_engineering), typeof(obj_item_robot_model_medical), typeof(obj_item_robot_model_janitor),
            typeof(obj_item_robot_model_security), typeof(obj_item_robot_model_service), typeof(obj_item_robot_model_miner), typeof(obj_item_robot_model_peacekeeper),
            typeof(obj_item_robot_model_clown), typeof(obj_machinery_recharge_station), typeof(obj_machinery_computer_robotics), typeof(datum_wires_robot),
            typeof(datum_component_riding_creature_cyborg), typeof(datum_job_cyborg), typeof(datum_storage_cyborg_internal_storage), typeof(datum_storage_interface_silicon),
            typeof(obj_item_gps_cyborg), typeof(datum_component_gps_item), typeof(obj_item_stack_marker_beacon), typeof(obj_structure_marker_beacon),
            typeof(obj_item_chisel), typeof(obj_structure_carving_block), typeof(obj_structure_statue_custom), typeof(datum_element_beauty), typeof(datum_component_marionette),
            typeof(datum_component_engraved), typeof(datum_element_wall_engraver), typeof(obj_item_rag), typeof(obj_item_rsf_cookiesynth), typeof(obj_item_razor),
            typeof(obj_item_melee_flyswatter), typeof(obj_item_paint_anycolor_cyborg), typeof(obj_item_picket_sign_cyborg), typeof(obj_item_storage_bag_trash),
            typeof(obj_item_storage_bag_tray), typeof(obj_item_construction_rcd_borg), typeof(obj_item_construction_rtd_borg), typeof(obj_item_gun_energy_laser_cyborg),
            typeof(obj_item_gun_energy_disabler_cyborg), typeof(obj_item_assembly_flash_cyborg), typeof(obj_item_soap_nanotrasen_cyborg), typeof(obj_item_toy_crayon_spraycan_borg),
            typeof(obj_item_stack_rods_cyborg), typeof(obj_item_melee_energy_sword_saber_cyborg), typeof(obj_item_reagent_containers_spray_waterflower_cyborg),
            typeof(obj_item_reagent_containers_spray_chemsprayer_janitor), typeof(datum_move_loop_move_rand),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AnEngineeringCyborgGetsItsModulesAndItsRcdBuildsFromItsCell()
    {
        var (r, _, _) = Room(9301);
        var borg = Borg(r, 5, 5, typeof(obj_item_robot_model_engineering));
        Assert.IsType<obj_item_robot_model_engineering>(r.In(() => borg.model));
        var modules = r.In(() => Dm.each<obj_item>(borg.model.modules).ToList());
        Assert.Contains(modules, m => m is obj_item_construction_rcd_borg);
        Assert.Contains(modules, m => m is obj_item_stack_rods_cyborg);
        Assert.Contains(modules, m => m is obj_item_assembly_flash_cyborg);

        var rcd = (obj_item_construction_rcd_borg)modules.First(m => m is obj_item_construction_rcd_borg);
        var cell = Cell(r, borg);
        float charge = r.In(() => Dm.num(cell.charge));
        Assert.True(charge > 0);
        Assert.Equal(charge, r.In(() => Dm.num(rcd.get_matter(borg))));
        // the RCD's matter is the cell's charge: a build of 10 units costs 10 * energyfactor
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(rcd.useResource(10, borg)))));
        Assert.Equal(charge - 10 * r.In(() => rcd.energyfactor), r.In(() => Dm.num(cell.charge)), 1f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARechargingStationChargesTheCyborgInsideIt()
    {
        var (r, _, _) = Room(9302);
        var station = r.In(() => Dm.@new<obj_machinery_recharge_station>(locate(6, 5, 1)));
        var borg = Borg(r, 6, 5);
        var cell = Cell(r, borg);
        r.In(() => cell.charge = Dm.num(cell.maxcharge) / 4);
        r.In(() => Waiting.NoSleep(station.close_machine(borg)));
        r.Step();
        Assert.Same(borg, r.In(() => station.occupant));
        Assert.Equal("borgcharger1", r.In(() => station.icon_state));
        float before = r.In(() => Dm.num(cell.charge));
        r.Step(10 * Second);
        Assert.True(r.In(() => Dm.num(cell.charge)) > before, "the station charges the cyborg's cell");

        r.In(() => Waiting.NoSleep(station.open_machine()));
        r.Step();
        Assert.Equal("borgcharger0", r.In(() => station.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRoboticsConsoleAndTheLockdownWireLockTheCyborgDown()
    {
        var (r, _, _) = Room(9303);
        var console = r.In(() => Dm.@new<obj_machinery_computer_robotics>(locate(7, 5, 1)));
        var borg = Borg(r, 5, 5);
        Assert.False(r.In(() => Dm.truthy(borg.lockcharge)));

        r.In(() => Waiting.NoSleep(console.lock_unlock_borg(borg, "the test room")));
        Assert.True(r.In(() => Dm.truthy(borg.lockcharge)), "the console locks the cyborg down");
        r.In(() => Waiting.NoSleep(console.lock_unlock_borg(borg)));
        Assert.False(r.In(() => Dm.truthy(borg.lockcharge)), "and releases it");

        // the console's window lists the cyborg
        var data = r.In(() => (DmList)Waiting.NoSleep(console.ui_data(null)));
        Assert.Single(r.In(() => Dm.each(data["cyborgs"] as DmList).ToList()));

        // cutting the lockdown wire locks it down too; mending releases it
        r.In(() => Waiting.NoSleep(borg.wires.cut(WIRE_LOCKDOWN)));
        Assert.True(r.In(() => Dm.truthy(borg.lockcharge)));
        r.In(() => Waiting.NoSleep(borg.wires.cut(WIRE_LOCKDOWN)));
        Assert.False(r.In(() => Dm.truthy(borg.lockcharge)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHatThrownAtACyborgLandsOnItsHead()
    {
        var (r, feed, _) = Room(9304);
        var borg = Borg(r, 5, 5);
        var hat = r.In(() => Dm.@new<obj_item_clothing_head_soft>(locate(5, 5, 1)));
        // a hat thrown at the head (tg's throw_impact reads the throw's target zone)
        r.In(() =>
        {
            var throw_ = Dm.@new<datum_thrownthing>(hat, borg, SOUTH, 5, 1, null, FALSE, MOVE_FORCE_DEFAULT, FALSE, null, BODY_ZONE_HEAD);
            hat.throw_impact(borg, throw_);
        });
        r.Step(Second);
        Assert.Same(hat, r.In(() => borg.hat));
        Assert.True(feed.Any("lands neatly on top of"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCyborgLaserDrawsItsShotFromTheCell()
    {
        var (r, _, _) = Room(9305);
        var borg = Borg(r, 5, 5, typeof(obj_item_robot_model_security));
        var gun = r.In(() =>
        {
            var g = Dm.@new<obj_item_gun_energy_laser_cyborg>(borg);
            return g;
        });
        var cell = Cell(r, borg);
        r.In(() => ((obj_item_stock_parts_power_store)gun.cell).charge = 0);
        float before = r.In(() => Dm.num(cell.charge));
        r.In(() => Waiting.NoSleep(gun.recharge_newshot()));
        Assert.True(r.In(() => Dm.num(cell.charge)) < before, "the shot's energy comes from the cyborg's cell");
        Assert.True(r.In(() => Dm.num(((obj_item_stock_parts_power_store)gun.cell).charge)) > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGpsSeesTheOtherSignals()
    {
        var (r, _, bob) = Room(9306);
        var mine = r.In(() => Dm.@new<obj_item_gps_cyborg>(locate(4, 5, 1)));
        var other = r.In(() => Dm.@new<obj_item_gps_mining>(locate(8, 6, 1)));
        r.Step();
        Assert.Equal("global positioning system (BORG0)", r.In(() => mine.name));
        var comp = r.In(() => (datum_component_gps_item)mine.GetComponent(typeof(datum_component_gps_item)));
        var data = r.In(() => (DmList)Waiting.NoSleep(comp.ui_data(bob)));
        var signals = r.In(() => Dm.each<DmList>(data["signals"] as DmList).ToList());
        Assert.Single(signals);
        Assert.Equal("MINE0", r.In(() => Dm.str(signals[0]["entrytag"])));
        Assert.Equal(4f, r.In(() => Dm.num(signals[0]["dist"])));

        // tracking off: the other GPS no longer lists it
        r.In(() => Waiting.NoSleep(comp.toggletracking(bob)));
        var otherComp = r.In(() => (datum_component_gps_item)other.GetComponent(typeof(datum_component_gps_item)));
        var otherData = r.In(() => (DmList)Waiting.NoSleep(otherComp.ui_data(bob)));
        Assert.Empty(r.In(() => Dm.each<DmList>(otherData["signals"] as DmList).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMarkerBeaconIsPlacedGlowsAndIsPickedUp()
    {
        var (r, _, bob) = Room(9307);
        var stack = r.In(() =>
        {
            var s = Dm.@new<obj_item_stack_marker_beacon_ten>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(s))));
            s.picked_color = "Teal";
            return s;
        });
        r.In(() => Waiting.NoSleep(stack.attack_self(bob)));
        r.Step();
        var beacon = r.In(() => Dm.each<obj_structure_marker_beacon>(bob.loc.contents).Single());
        Assert.Equal(9f, r.In(() => Dm.num(stack.amount)));
        Assert.Equal("markerteal-on", r.In(() => beacon.icon_state));
        Assert.Equal(LIGHT_COLOR_LIGHT_CYAN, r.In(() => Dm.str(beacon.light_color)));

        // hit with the stack, the beacon goes back into it
        r.In(() => Waiting.Detach(beacon.item_interaction(bob, stack, Dm.list())));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(beacon)));
        Assert.Equal(10f, r.In(() => Dm.num(stack.amount)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChiselCarvesAStatueOfItsTarget()
    {
        var (r, _, bob) = Room(9308);
        var block = r.In(() => Dm.@new<obj_structure_carving_block>(locate(4, 5, 1)));
        var duck = r.In(() => Dm.@new<obj_item_bikehorn_rubberducky>(locate(4, 6, 1)));
        var chisel = r.In(() =>
        {
            var c = Dm.@new<obj_item_chisel>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(c))));
            return c;
        });
        // the first hit prepares the block, then the chisel points at the target
        r.In(() => Waiting.NoSleep(chisel.interact_with_atom(block, bob, Dm.list())));
        Assert.Same(block, r.In(() => chisel.prepared_block));
        r.In(() => Waiting.NoSleep(chisel.interact_with_atom(duck, bob, Dm.list())));
        Assert.NotNull(r.In(() => block.current_target));

        // half carved: the target shows through the block's mask
        r.In(() => block.set_completion(0.5f));
        Assert.NotNull(r.In(() => block.target_appearance_with_filters));
        r.In(() => block.create_statue());
        r.Step();
        var statue = r.In(() => Dm.each<obj_structure_statue_custom>(locate(4, 5, 1).contents).Single());
        Assert.Equal("statue of " + r.In(() => duck.name), r.In(() => statue.name));
        Assert.NotNull(r.In(() => statue.content_ma));
        Assert.True(r.In(() => QDELETED(block)));
        r.AssertNoRuntimes();
    }
}

public class CyborgSoakTests
{
    private const int Second = 20;

    // the in-game look's spawns (a robotized player, a preset engineering cyborg, a statue, a marker beacon, a recharging station) left
    // alone for half a minute raise no runtime
    [Fact]
    public void ARobotizedPlayerAndTheSpawnsRunCleanForHalfAMinute()
    {
        var r = new AudioRoom(13, 9, seed: 9399);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Robotize()));
        r.Step(2 * Second);
        r.In(() =>
        {
            Dm.@new<obj_machinery_recharge_station>(locate(6, 4, 1));
            Dm.@new<mob_living_silicon_robot_model_engineering>(locate(7, 5, 1));
            Dm.@new<obj_structure_statue_gold_hos>(locate(7, 3, 1));
            Dm.@new<obj_structure_marker_beacon_teal>(locate(6, 5, 1));
        });
        r.Step(30 * Second);
        r.AssertNoRuntimes();
    }
}
