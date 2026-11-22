using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package J6 (D298): the mining and cargo gear in a booted toy room: the kinetic accelerator fires, recharges in tg's 1.6 s and does a
/// quarter of its damage in air and all of it in thin air, modkits and a knife go on it, shovels dig, the ore bag picks up ore as its wearer
/// walks, the scanners ping rock, gibtonite is struck and defused, the climbing hook checks the level above, beacons form a network, the
/// universal scanner tags prices, the quartermaster's locker gear works, and every type spawns live with no runtimes.
/// </summary>
public class MiningCargoGearTests
{
    // The hook with a hole above: tg's target_blocked reads /turf/open/openspace, inert until a stacked map is ported.
    [DmPath("/obj/item/climbing_hook/test_hole")]
    private sealed class HoleHook : obj_item_climbing_hook
    {
        public override object target_blocked(turf target = null, turf trans_vertical = null) => FALSE;
    }

    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_gun_energy_recharge_kinetic_accelerator), typeof(obj_item_gun_energy_recharge_kinetic_accelerator_bdm), typeof(obj_item_gun_energy_recharge_kinetic_accelerator_cyborg), typeof(obj_item_gun_energy_recharge_kinetic_accelerator_minebot),
        typeof(obj_item_ammo_casing_energy_kinetic), typeof(obj_item_borg_upgrade_modkit), typeof(obj_item_borg_upgrade_modkit_range), typeof(obj_item_borg_upgrade_modkit_damage), typeof(obj_item_borg_upgrade_modkit_cooldown), typeof(obj_item_borg_upgrade_modkit_cooldown_minebot),
        typeof(obj_item_borg_upgrade_modkit_cooldown_aoe_turfs), typeof(obj_item_borg_upgrade_modkit_cooldown_aoe_mobs), typeof(obj_item_borg_upgrade_modkit_cooldown_aoe_mobs_andturfs), typeof(obj_item_borg_upgrade_modkit_cooldown_repeater), typeof(obj_item_borg_upgrade_modkit_minebot_passthrough), typeof(obj_item_borg_upgrade_modkit_human_passthrough),
        typeof(obj_item_borg_upgrade_modkit_lifesteal), typeof(obj_item_borg_upgrade_modkit_resonator_blasts), typeof(obj_item_borg_upgrade_modkit_bounty), typeof(obj_item_borg_upgrade_modkit_indoors), typeof(obj_item_borg_upgrade_modkit_trigger_guard), typeof(obj_item_borg_upgrade_modkit_chassis_mod), typeof(obj_item_borg_upgrade_modkit_chassis_mod_orange), typeof(obj_item_borg_upgrade_modkit_tracer), typeof(obj_item_borg_upgrade_modkit_tracer_adjustable),
        typeof(obj_item_pickaxe_drill), typeof(obj_item_pickaxe_drill_diamonddrill), typeof(obj_item_pickaxe_drill_jackhammer), typeof(obj_item_shovel), typeof(obj_item_shovel_spade), typeof(obj_item_shovel_serrated), typeof(obj_item_shovel_serrated_dull), typeof(obj_item_shovel_giant_wrench), typeof(obj_item_trench_tool),
        typeof(obj_item_storage_bag_ore), typeof(obj_item_storage_bag_ore_holding), typeof(obj_item_mining_scanner), typeof(obj_item_t_scanner_adv_mining_scanner), typeof(obj_item_t_scanner_adv_mining_scanner_lesser), typeof(obj_item_resonator), typeof(obj_item_resonator_upgraded), typeof(obj_item_climbing_hook), typeof(obj_item_climbing_hook_emergency), typeof(obj_item_climbing_hook_syndicate), typeof(obj_item_climbing_hook_infinite),
        typeof(obj_item_stack_ore_uranium), typeof(obj_item_stack_ore_iron), typeof(obj_item_stack_ore_glass), typeof(obj_item_stack_ore_glass_basalt), typeof(obj_item_stack_ore_plasma), typeof(obj_item_stack_ore_silver), typeof(obj_item_stack_ore_gold), typeof(obj_item_stack_ore_diamond), typeof(obj_item_stack_ore_bananium), typeof(obj_item_stack_ore_titanium), typeof(obj_item_stack_ore_slag), typeof(obj_item_stack_ore_bluespace_crystal), typeof(obj_item_gibtonite),
        typeof(obj_item_stack_candela_beacon), typeof(obj_item_stack_candela_beacon_ten), typeof(obj_item_stack_candela_beacon_thirty), typeof(obj_item_universal_scanner), typeof(obj_item_barcode), typeof(obj_item_boxcutter), typeof(obj_item_boxcutter_extended), typeof(obj_item_card_mining_point_card), typeof(obj_item_radio_weather_monitor),
        typeof(obj_item_storage_box_survival_mining), typeof(obj_item_storage_box_emptysandbags), typeof(obj_item_emptysandbag), typeof(obj_item_stack_sheet_mineral_sandbags), typeof(obj_item_storage_bag_garment_quartermaster), typeof(obj_item_storage_lockbox_medal_cargo), typeof(obj_item_megaphone_cargo), typeof(obj_item_door_remote_quartermaster), typeof(obj_item_storage_photo_album_qm),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() =>
        {
            if (who.get_active_held_item() is obj_item held)
                Waiting.NoSleep(who.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(thing))));
        });
        return thing;
    }

    private static turf Rock(AudioRoom r, int x, int y, Type type = null) =>
        r.In(() => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(type ?? typeof(turf_closed_mineral_iron))));

    // The charge after picking the gun up: empty until tg's 1.6 s reload.
    private static void Charged(AudioRoom r, obj_item_gun_energy_recharge_kinetic_accelerator gun)
    {
        Assert.False(r.In(() => Dm.truthy(gun.can_shoot())));
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.truthy(gun.can_shoot())));
    }

    private static void Fire(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target)
    {
        object fired = r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16")));
        Assert.True(Dm.truthy(fired), "the accelerator did not fire");
        r.Step(Second);
    }

    [Fact]
    public void TheAcceleratorFiresRechargesInTgsTimeAndFollowsThePressureRule()
    {
        var (r, _, bob) = Room(9601);
        var gun = Hold<obj_item_gun_energy_recharge_kinetic_accelerator>(r, bob);
        Charged(r, gun);

        // in the room's air: a quarter of 40
        mob_living_carbon_human indoor = r.Dummy(5, 4);
        r.Step(2);
        Fire(r, bob, gun, indoor);
        Assert.Equal(10f, r.In(() => Dm.num(indoor.get_brute_loss())), 1);
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.EndsWith("kinetic_accel.ogg", StringComparison.Ordinal));
        r.In(() => qdel(indoor));
        r.Step(2);

        // the next shot only after the recharge
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(gun.can_shoot())));

        // thin air under the target: all 40
        mob_living_carbon_human outdoor = r.Dummy(5, 6);
        r.In(() => (Waiting.NoSleep(locate(5, 6, 1).return_air()) as datum_gas_mixture).remove_ratio(1));
        Fire(r, bob, gun, outdoor);
        Assert.InRange(r.In(() => Dm.num(outdoor.get_brute_loss())), 40f, 44.01f); // and tg's 4 of a vacuum if the target's Life runs before the look

        // put away and dropped: it empties a tenth of a second later
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.truthy(gun.can_shoot())));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step(4);
        Assert.False(r.In(() => Dm.truthy(gun.can_shoot())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBoltDrillsRockAndDropsItsOre()
    {
        var (r, _, bob) = Room(9602);
        var gun = Hold<obj_item_gun_energy_recharge_kinetic_accelerator>(r, bob);
        Charged(r, gun);
        turf rock = Rock(r, 5, 4);
        Fire(r, bob, gun, rock);
        Assert.False(r.In(() => locate(5, 4, 1) is turf_closed_mineral));
        Assert.True(r.In(() => Dm.locate<obj_item_stack_ore_iron>(locate(5, 4, 1)) != null));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ModkitsFitTheCapacityChangeTheBoltAndComeOutWithACrowbar()
    {
        var (r, feed, bob) = Room(9603);
        var gun = Hold<obj_item_gun_energy_recharge_kinetic_accelerator>(r, bob);
        Charged(r, gun);
        var damage = r.In(() => Dm.@new<obj_item_borg_upgrade_modkit_damage>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(damage.install(gun, bob)))));
        Assert.Equal(70f, r.In(() => gun.get_remaining_mod_capacity()));
        Assert.True(feed.Any("You install the modkit."));

        // the cooldown kit changes the recharge, tg's 16 - 3.2
        var cooldown = r.In(() => Dm.@new<obj_item_borg_upgrade_modkit_cooldown>(bob.loc));
        r.In(() => Waiting.NoSleep(cooldown.install(gun, bob)));
        Assert.Equal(12.8f, r.In(() => gun.recharge_time), 2);

        // bigger than the room left
        var second = r.In(() => Dm.@new<obj_item_borg_upgrade_modkit_damage>(bob.loc));
        r.In(() => Waiting.NoSleep(second.install(gun, bob))); // 90 used
        var refused = r.In(() => Dm.@new<obj_item_borg_upgrade_modkit_damage>(bob.loc));
        r.In(() => Assert.False(Dm.truthy(Waiting.NoSleep(refused.install(gun, bob)))));
        Assert.True(feed.Any("You don't have room"));

        // the bolt: 40 + 20 damage, a quarter in air
        mob_living_carbon_human target = r.Dummy(5, 4);
        r.Step(2 * Second);
        Fire(r, bob, gun, target);
        Assert.Equal(15f, r.In(() => Dm.num(target.get_brute_loss())), 1);

        // a crowbar takes them all out, the recharge back to 16
        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(bob.loc));
        r.In(() => Waiting.NoSleep(gun.crowbar_act(bob, crowbar)));
        r.Step(2);
        Assert.Equal(0, r.In(() => Dm.length(gun.modkits)));
        Assert.Equal(16f, r.In(() => gun.recharge_time), 2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSurvivalKnifeIsABayonetAndCutsAsAKnife()
    {
        var (r, feed, bob) = Room(9604);
        var gun = Hold<obj_item_gun_energy_recharge_kinetic_accelerator>(r, bob);
        var knife = r.In(() => Dm.@new<obj_item_knife_combat_survival>(bob.loc));
        r.Step(2);
        Assert.Equal(TOOL_KNIFE, r.In(() => knife.tool_behaviour));
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(knife)));
        r.In(() => Waiting.NoSleep(gun.attackby(knife, bob)));
        r.Step(2);
        Assert.Same(gun, r.In(() => knife.loc));
        var bayonet = r.In(() => gun.GetComponent(typeof(datum_component_bayonet_attachable)) as datum_component_bayonet_attachable);
        Assert.Same(knife, r.In(() => bayonet.bayonet));
        DmList seen = r.In(() => Waiting.NoSleep(gun.examine(bob)) as DmList);
        Assert.Contains("mod capacity", r.In(() => string.Join("\n", Dm.each<object>(seen).Select(l => l?.ToString()))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShovelDigsSandAndARightClickDigsAGrave()
    {
        var (r, feed, bob) = Room(9605);
        var shovel = Hold<obj_item_shovel>(r, bob);
        turf dirt = r.In(() => (turf)Waiting.NoSleep(locate(4, 4, 1).ChangeTurf(typeof(turf_open_misc_asteroid))));
        r.Step(2);
        Click(r, bob, dirt);
        r.Step(4 * Second);
        Assert.True(r.In(() => Dm.truthy((locate(4, 4, 1) as turf_open_misc_asteroid).dug)), "the sand is not dug");
        Assert.True(r.In(() => Dm.locate<obj_item_stack_ore_glass>(locate(4, 4, 1)) != null), "no sand came out");

        turf other = r.In(() => (turf)Waiting.NoSleep(locate(3, 5, 1).ChangeTurf(typeof(turf_open_misc_asteroid))));
        r.Step(2);
        Click(r, bob, other, "right=1");
        r.Step(11 * Second);
        var grave = r.In(() => Dm.locate<obj_structure_closet_crate_grave_fresh>(locate(3, 5, 1)));
        Assert.NotNull(grave);

        // the grave opens only by the shovel
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(grave.open(bob)))));
        Assert.True(feed.Any("too hard to dig up"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTrenchToolSwitchesConfigurationAndItsPictureFollows()
    {
        var (r, _, bob) = Room(9606);
        var tool = Hold<obj_item_trench_tool>(r, bob);
        Assert.Equal(TOOL_WRENCH, r.In(() => tool.tool_behaviour));
        r.In(() =>
        {
            tool.tool_behaviour = TOOL_MINING;
            tool.update_appearance(UPDATE_ICON);
        });
        Assert.Equal("trench_tool_pick", r.In(() => tool.icon_state));
        r.In(() => Assert.True(Dm.@in(TOOL_SHOVEL, tool.get_all_tool_behaviours() as DmList)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOreBagPicksUpOreAsItsWearerWalksAndWhatFallsUnderHim()
    {
        var (r, _, bob) = Room(9607);
        var bag = Hold<obj_item_storage_bag_ore>(r, bob);
        r.Step(2);
        r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(4, 4, 1), 5));
        r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(4, 4, 1), 3)); // merges into the first
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 4, 1))));
        r.Step(4);
        Assert.Equal(0, r.In(() => Dm.each<obj_item_stack_ore_iron>(locate(4, 4, 1)).Count()));
        Assert.Equal(8f, r.In(() => Dm.each<obj_item_stack_ore_iron>(bag).Sum(o => Dm.num(o.amount))));

        // ore that lands on his tile goes in
        var thrown = r.In(() => Dm.@new<obj_item_stack_ore_gold>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(thrown.forceMove(locate(4, 4, 1))));
        r.Step(4);
        Assert.Same(bag, r.In(() => thrown.loc));

        // dropped, it stops
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bag)));
        var left = r.In(() => Dm.@new<obj_item_stack_ore_plasma>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 4, 1))));
        r.Step(4);
        Assert.Same(locate2(r, 5, 4), r.In(() => left.loc));
        r.AssertNoRuntimes();
    }

    private static turf locate2(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    [Fact]
    public void TheScannersPingOreInRockAndTheOverlayFades()
    {
        var (r, _, bob) = Room(9608);
        turf iron = Rock(r, 5, 4);
        turf plain = Rock(r, 5, 5, typeof(turf_closed_mineral));
        var scanner = Hold<obj_item_mining_scanner>(r, bob);
        r.In(() => Waiting.NoSleep(scanner.attack_self(bob)));
        r.Step(2);
        var overlay = r.In(() => Dm.locate<obj_effect_temp_visual_mining_overlay>(iron));
        Assert.NotNull(overlay);
        Assert.Null(r.In(() => Dm.locate<obj_effect_temp_visual_mining_overlay>(plain)));
        Assert.True(r.In(() => Dm.length(overlay.overlays)) > 0);

        // a second ping inside the cooldown does nothing; the overlay is gone after its 3.5 s
        r.Step(4 * Second);
        Assert.True(r.In(() => QDELETED(overlay)));

        // the automatic scanner of the miner's kit: range 4, on its own
        var auto = Hold<obj_item_t_scanner_adv_mining_scanner_lesser>(r, bob);
        r.In(() => auto.scan());
        Assert.NotNull(r.In(() => Dm.locate<obj_effect_temp_visual_mining_overlay>(iron)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GibtoniteIsStruckThenDefusedByTheScannerAndDugOut()
    {
        var (r, feed, bob) = Room(9609);
        var deposit = (turf_closed_mineral_gibtonite)Rock(r, 4, 4, typeof(turf_closed_mineral_gibtonite));
        r.In(() => Waiting.NoSleep(deposit.gets_drilled(bob)));
        r.Step(2);
        Assert.Equal((float)GIBTONITE_ACTIVE, r.In(() => deposit.stage));
        Assert.True(feed.Any("There's gibtonite inside!"));
        var scanner = Hold<obj_item_mining_scanner>(r, bob);
        r.In(() => deposit.defuse(bob));
        Assert.Equal((float)GIBTONITE_STABLE, r.In(() => deposit.stage));
        r.Step(10 * Second);
        Assert.True(r.In(() => locate(4, 4, 1) == deposit), "a defused deposit does not explode");
        r.In(() => Waiting.NoSleep(deposit.gets_drilled(bob)));
        r.Step(2);
        Assert.NotNull(r.In(() => Dm.locate<obj_item_gibtonite>(locate(4, 4, 1))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClimbingHookNeedsAnOpenHoleOnAnotherLevel()
    {
        var (r, _, bob) = Room(9610);
        var hook = Hold<obj_item_climbing_hook>(r, bob);
        // the same level is nothing to climb
        r.In(() => Assert.Equal((object)NONE, Waiting.NoSleep(hook.ranged_interact_with_atom(locate(4, 4, 1), bob))));
        // a blocked target: a wall below the hole
        turf wall = r.In(() => (turf)Waiting.NoSleep(locate(4, 5, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() => Assert.True(Dm.truthy(hook.target_blocked(wall, locate(4, 4, 1)))));
        DmList seen = r.In(() => Waiting.NoSleep(hook.examine(bob)) as DmList);
        Assert.Contains("look upwards", r.In(() => string.Join("\n", Dm.each<object>(seen).Select(l => l?.ToString()))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClimbingHookClimbsToAStackedLevelThroughAHole()
    {
        var r = new AudioRoom(13, 9, seed: 9614, levels: 2);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var hook = Hold<HoleHook>(r, bob);
        // a second deck above the room: floor over Bob and beside it (open space stays inert until a stacked map, so the hook sees a hole)
        turf target = r.In(() =>
        {
            SSmapping.multiz_levels.len = 2;
            SSmapping.multiz_levels[1] = Dm.list_n(LARGEST_Z_LEVEL_INDEX);
            SSmapping.multiz_levels[2] = Dm.list_n(LARGEST_Z_LEVEL_INDEX);
            ((DmList)SSmapping.multiz_levels[1])[Z_LEVEL_UP] = TRUE;
            ((DmList)SSmapping.multiz_levels[2])[Z_LEVEL_DOWN] = TRUE;
            Waiting.NoSleep(locate(3, 4, 2).ChangeTurf(typeof(turf_open_floor_plating)));
            return (turf)Waiting.NoSleep(locate(4, 4, 2).ChangeTurf(typeof(turf_open_floor_plating)));
        });
        r.Step(2);
        Assert.Same(r.In(() => locate(3, 4, 2)), r.In(() => GET_TURF_ABOVE(bob.loc)));
        r.In(() => Waiting.Detach(hook.ranged_interact_with_atom(target, bob)));
        r.Step(Second);
        Assert.True(feed.Any("You get to work on properly hooking"));
        Assert.Equal(1f, r.In(() => (float)bob.z)); // tg's 2.5 s
        r.Step(2 * Second);
        Assert.Same(target, r.In(() => bob.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BeaconsFormANetworkDrawBeamsAndComeBackIntoTheStack()
    {
        var (r, feed, bob) = Room(9611);
        var stack = Hold<obj_item_stack_candela_beacon_thirty>(r, bob);
        r.In(() => Waiting.NoSleep(stack.attack_self(bob)));
        r.Step(2);
        Assert.Equal(29f, r.In(() => Dm.num(stack.amount)));
        var first = r.In(() => Dm.locate<obj_structure_candela_beacon>(bob.loc));
        Assert.NotNull(first);
        var firstNode = r.In(() => first.GetComponent(typeof(datum_component_candela_node)) as datum_component_candela_node);
        Assert.NotNull(r.In(() => firstNode.network));
        Assert.Same(r.In(() => firstNode.network), r.In(() => stack.handler.network));

        // a second beacon on sight joins the same network, older nodes draw the beam
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(6, 4, 1))));
        r.Step(2);
        r.In(() => Waiting.NoSleep(stack.attack_self(bob)));
        r.Step(2);
        var second = r.In(() => Dm.locate<obj_structure_candela_beacon>(bob.loc));
        var secondNode = r.In(() => second.GetComponent(typeof(datum_component_candela_node)) as datum_component_candela_node);
        Assert.Same(r.In(() => firstNode.network), r.In(() => secondNode.network));
        Assert.Equal(1, r.In(() => Dm.length(firstNode.beam_visuals) + Dm.length(secondNode.beam_visuals)));
        Assert.Equal(1, r.In(() => Dm.length(GLOB.mining_beacon_networks)));

        // a right click with the stack takes it back
        r.In(() => Waiting.Detach(second.item_interaction_secondary(bob, stack)));
        r.Step(3 * Second);
        Assert.True(r.In(() => QDELETED(second)));
        Assert.Equal(29f, r.In(() => Dm.num(stack.amount)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheUniversalScannerTagsPricesAndTheQuartermastersGearWorks()
    {
        var (r, feed, bob) = Room(9612);
        var scanner = Hold<obj_item_universal_scanner>(r, bob);
        var thing = r.In(() => Dm.@new<obj_item_crowbar>(bob.loc));
        r.Step(2);
        // the export reading is a row naming P4: quiet, no runtime
        r.In(() => Waiting.NoSleep(scanner.interact_with_atom(thing, bob)));
        r.In(() => { scanner.scanning_mode = SCAN_PRICE_TAG; scanner.new_custom_price = 25; });
        r.In(() => Waiting.NoSleep(scanner.interact_with_atom(thing, bob)));
        Assert.Equal(25f, r.In(() => Dm.num(thing.custom_price)));
        Assert.True(feed.Any("You set the price of"));

        // the box cutter opens into a knife
        var cutter = Hold<obj_item_boxcutter>(r, bob);
        r.In(() => Waiting.NoSleep(cutter.attack_self(bob)));
        Assert.Equal(TOOL_KNIFE, r.In(() => cutter.tool_behaviour));

        // the medal box starts locked and holds its ribbon
        var medals = r.In(() => Dm.@new<obj_item_storage_lockbox_medal_cargo>(bob.loc));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(medals.atom_storage.locked)));
        Assert.True(r.In(() => Dm.locate<obj_item_clothing_accessory_medal_ribbon_cargo>(medals) != null));
        r.In(() => Waiting.NoSleep(medals.emag_act(bob)));
        Assert.False(r.In(() => Dm.truthy(medals.atom_storage.locked)));
        Assert.Equal("medalbox+b", r.In(() => { medals.update_appearance(); return medals.icon_state; }));

        // the door remote's modes cycle and its picture follows
        var remote = Hold<obj_item_door_remote_quartermaster>(r, bob);
        Assert.Equal("remote_cargo_open", r.In(() => remote.icon_state));
        r.In(() => Waiting.NoSleep(remote.attack_self(bob)));
        Assert.Equal("remote_cargo_bolt", r.In(() => remote.icon_state));

        // sand fills a sandbag
        var bag = r.In(() => Dm.@new<obj_item_emptysandbag>(bob.loc));
        var sand = r.In(() => Dm.@new<obj_item_stack_ore_glass>(bob.loc, 2));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bag.item_interaction(bob, sand)));
        r.Step(2);
        Assert.Equal(1f, r.In(() => Dm.num(sand.amount)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryTypeSpawnsLiveAndIsDestroyedWithNoRuntimes()
    {
        var (r, _, bob) = Room(9613);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            obj_item item = r.In(() => (obj_item)Dm.@new(type, bob.loc));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(item)))
                failures.Add(type.Name + " is inert");
            r.In(() => Waiting.NoSleep(item.examine(bob)));
            r.In(() => qdel(item));
            r.Step(2);
        }
        foreach (Type type in new[] { typeof(obj_structure_candela_beacon), typeof(obj_structure_barricade_sandbags), typeof(obj_structure_closet_crate_grave_fresh) })
        {
            atom thing = r.In(() => (atom)Dm.@new(type, locate(5, 4, 1)));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(thing)))
                failures.Add(type.Name + " is inert");
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.AssertNoRuntimes();
    }
}
