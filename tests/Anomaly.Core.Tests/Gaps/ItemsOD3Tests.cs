using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package OD3 (D333), in a booted toy room with Bob as a player: the rapid part exchange device (an open machine's parts swapped
/// for better ones, a closed one only listed, the bluespace RPED at range with its beam, a machine frame filled with its board and
/// parts, a computer frame wired and glassed, the storage's stack quota and lowest-tier dump, the bluespace RPED emptying a beaker),
/// the rotfrag grenade's rotations, Lavaland's ash flora picked and regrown with its light, and the elephant graveyard's oil well,
/// graves and rocks.
/// </summary>
public class ItemsOD3Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new(typeof(T), who.loc) as T);
        r.Step(2);
        r.In(() =>
        {
            foreach (object held in Dm.each(who.held_items.Copy()))
                if (held is obj_item old && old != item)
                    qdel(old);
            Waiting.NoSleep(who.put_in_hands(item));
        });
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static int Heard(AudioRoom r, string part) => r.Sounds.Heard.Count(s => (s.File ?? "").Contains(part, StringComparison.Ordinal));

    private static float CapacitorTier(AudioRoom r, obj_machinery machine) => r.In(() =>
        Dm.num(Dm.each(machine.component_parts).OfType<datum_stock_part_capacitor>().Single().tier));

    [Fact]
    public void TheRpedSwapsAnOpenMachinesPartsAndOnlyListsAClosedOnes()
    {
        var (r, feed, bob) = Room(9501);
        var recharger = Place<obj_machinery_recharger>(r, 5, 4);
        var rped = Hold<obj_item_storage_part_replacer>(r, bob);
        r.In(() => Dm.@new<obj_item_stock_parts_capacitor_quadratic>(rped));
        Assert.Equal(1f, CapacitorTier(r, recharger));

        // the panel is shut: the RPED only lists the parts
        Click(r, bob, recharger);
        r.Step(4);
        Assert.Equal(1f, CapacitorTier(r, recharger));
        Assert.True(feed.Any("It contains the following parts"), Diag(feed, r));

        // open: the quadratic capacitor goes in, the old one comes out into the RPED, the machine's coefficient follows (RefreshParts)
        r.In(() => recharger.panel_open = TRUE);
        Click(r, bob, recharger);
        r.Step(4);
        Assert.Equal(4f, CapacitorTier(r, recharger));
        r.In(() =>
        {
            Assert.Equal(4f, recharger.recharge_coeff);
            Assert.Empty(Dm.each<obj_item_stock_parts_capacitor_quadratic>(rped.contents));
            Assert.Single(Dm.each<obj_item_stock_parts_capacitor>(rped.contents));
        });
        Assert.True(feed.Any("replaced with"), Diag(feed, r));
        Assert.True(Heard(r, "sound/items/tools/rped.ogg") >= 1, r.Sounds.Dump());

        // nothing better left: no swap and no sound
        int sounds = Heard(r, "sound/items/tools/rped.ogg");
        Click(r, bob, recharger);
        r.Step(4);
        Assert.Equal(4f, CapacitorTier(r, recharger));
        Assert.Equal(sounds, Heard(r, "sound/items/tools/rped.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBluespaceRpedWorksFromAfarWithItsBeam()
    {
        var (r, feed, bob) = Room(9502);
        var recharger = Place<obj_machinery_recharger>(r, 9, 4);
        var brped = Hold<obj_item_storage_part_replacer_bluespace_tier4>(r, bob);
        r.Step(2);
        r.In(() => Assert.Equal(10, Dm.each<obj_item_stock_parts_capacitor_quadratic>(brped.contents).Count()));

        // five tiles away and the panel shut: the bluespace one swaps anyway
        Click(r, bob, recharger);
        r.Step(2);
        Assert.Equal(4f, CapacitorTier(r, recharger));
        r.In(() => Assert.Contains(Dm.each<datum_beam>(), b => b.target == recharger));
        Assert.True(feed.Any("It contains the following parts"), Diag(feed, r)); // it lists them from afar too
        Assert.True(Heard(r, "sound/items/pshoom/pshoom") >= 1, r.Sounds.Dump());
        r.In(() => Assert.Equal(9, Dm.each<obj_item_stock_parts_capacitor_quadratic>(brped.contents).Count()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRpedFillsAMachineFrameAndWiresAndGlassesAComputerFrame()
    {
        var (r, feed, bob) = Room(9503);
        var frame = r.In(() =>
        {
            var f = Dm.@new<obj_structure_frame_machine>(locate(5, 4, 1));
            f.state = FRAME_STATE_WIRED;
            return f;
        });
        var rped = Hold<obj_item_storage_part_replacer>(r, bob);
        r.In(() =>
        {
            Dm.@new<obj_item_circuitboard_machine_recharger>(rped);
            Dm.@new<obj_item_stock_parts_capacitor_adv>(rped);
        });
        Click(r, bob, frame);
        r.Step(4);
        r.In(() =>
        {
            Assert.IsType<obj_item_circuitboard_machine_recharger>(frame.circuit_value);
            Assert.Equal(0f, Dm.num(frame.req_components[typeof(datum_stock_part_capacitor)]));
            Assert.Contains(Dm.each(frame.components), c => c is datum_stock_part_capacitor cap && Dm.num(cap.tier) == 2);
            Assert.Empty(Dm.each<obj_item>(rped.contents));
        });
        Assert.True(feed.Any("circuit installed"), Diag(feed, r));
        Assert.True(Heard(r, "sound/items/tools/rped.ogg") >= 1, r.Sounds.Dump());

        // the computer frame with its board secured: five cable and two glass from the RPED, at once
        var console = r.In(() =>
        {
            var f = Dm.@new<obj_structure_frame_computer>(locate(4, 5, 1));
            var board = Dm.@new<obj_item_circuitboard_computer_powermonitor>(f);
            f.circuit_value = board;
            f.state = FRAME_COMPUTER_STATE_BOARD_SECURED;
            Dm.@new<obj_item_stack_cable_coil>(rped);
            Dm.@new<obj_item_stack_sheet_glass>(rped, 5);
            return f;
        });
        Click(r, bob, console);
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal((float)FRAME_COMPUTER_STATE_GLASSED, Dm.num(console.state));
            Assert.Equal(25f, Dm.num(Dm.each<obj_item_stack_cable_coil>(rped.contents).Single().amount));
            Assert.Equal(3f, Dm.num(Dm.each<obj_item_stack_sheet_glass>(rped.contents).Single().amount));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRpedTakesPartsAndAQuotaOfStacksAndDumpsItsLowestTier()
    {
        var (r, feed, bob) = Room(9504);
        var rped = Place<obj_item_storage_part_replacer>(r, 4, 4);
        r.In(() =>
        {
            var storage = rped.atom_storage;
            // a crowbar is no part
            var crowbar = Dm.@new<obj_item_crowbar>(locate(4, 4, 1));
            Assert.False(Dm.truthy(storage.can_insert(crowbar, bob, messages: FALSE)));
            // fifty glass: the RPED takes thirty, the rest stays
            var glass = Dm.@new<obj_item_stack_sheet_glass>(locate(4, 4, 1), 50);
            Assert.True(Dm.truthy(storage.attempt_insert(glass, bob, messages: FALSE)));
            Assert.Equal(20f, Dm.num(glass.amount));
            Assert.Equal(30f, Dm.num(Dm.each<obj_item_stack_sheet_glass>(rped.contents).Sum(s => Dm.num(s.amount))));
            // full quota: no more
            Assert.False(Dm.truthy(storage.attempt_insert(glass, bob, messages: FALSE)));
            // iron is no machine stack
            var iron = Dm.@new<obj_item_stack_sheet_iron>(locate(4, 4, 1), 10);
            Assert.False(Dm.truthy(storage.attempt_insert(iron, bob, messages: FALSE)));
            // two bins of tiers 1 and 3: emptying drops the tier 1 one only (the lowest rating, the stacks before it)
            Assert.True(Dm.truthy(storage.attempt_insert(Dm.@new<obj_item_stock_parts_matter_bin>(locate(4, 4, 1)), bob, messages: FALSE)));
            Assert.True(Dm.truthy(storage.attempt_insert(Dm.@new<obj_item_stock_parts_matter_bin_super>(locate(4, 4, 1)), bob, messages: FALSE)));
        });
        // tg's sort puts the lowest rating first: the glass (a stack rates 0) goes before the tier 1 bin, then the bin
        r.In(() => rped.atom_storage.mass_empty(rped, bob));
        r.Step(2);
        r.In(() =>
        {
            Assert.Empty(Dm.each<obj_item_stack_sheet_glass>(rped.contents));
            Assert.Equal(2, Dm.each<obj_item_stock_parts_matter_bin>(rped.contents).Count());
        });
        r.In(() => rped.atom_storage.mass_empty(rped, bob));
        r.Step(2);
        r.In(() =>
        {
            Assert.Single(Dm.each<obj_item_stock_parts_matter_bin_super>(rped.contents));
            Assert.DoesNotContain(Dm.each(rped.contents), b => b is obj_item_stock_parts_matter_bin && b is not obj_item_stock_parts_matter_bin_super);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBluespaceRpedEmptiesABeakerIntoBluespaceAndKeepsItDry()
    {
        var (r, feed, bob) = Room(9505);
        var brped = Place<obj_item_storage_part_replacer_bluespace>(r, 4, 4);
        r.In(() =>
        {
            var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(4, 4, 1));
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 20));
            Assert.True(Dm.truthy(brped.atom_storage.attempt_insert(beaker, bob, messages: FALSE)));
            Assert.Equal(0f, Dm.num(((datum_reagents)beaker.reagents).total_volume));
            // inside, nothing can be added
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 10));
            Assert.Equal(0f, Dm.num(((datum_reagents)beaker.reagents).total_volume));
            // out again, it fills as before
            Assert.True(Dm.truthy(brped.atom_storage.attempt_remove(beaker, locate(4, 4, 1))));
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 10));
            Assert.Equal(10f, Dm.num(((datum_reagents)beaker.reagents).total_volume));
        });
        r.AssertNoRuntimes();
    }

    private static async DmTask Detonate(obj_item_grenade grenade, mob_living who, Action<object> done) => done(await grenade.detonate(who));

    [Fact]
    public void TheRotfragGrenadeGrowsItsShrapnelWithEveryTurnInHand()
    {
        var (r, feed, bob) = Room(9506);
        var grenade = Hold<obj_item_grenade_primer>(r, bob);
        r.In(() => Waiting.NoSleep(grenade.arm_grenade(bob, 10 * Second)));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(grenade.active)));
        for (int i = 0; i < 5; i++)
        {
            r.In(() => Waiting.NoSleep(grenade.attack_self(bob)));
            r.Step(3);
        }
        r.In(() => Assert.Equal(6f, grenade.rots));
        Assert.True(Heard(r, "sound/misc/box_deploy.ogg") >= 5, r.Sounds.Dump());
        object detonated = null;
        r.In(() => Waiting.Detach(Detonate(grenade, bob, d => detonated = d)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(2f, Dm.num(grenade.shrapnel_radius)); // round(6 / 3)
            Assert.Equal(1f, Dm.num(detonated));
            // qdel(src) after the blast: the pellet cloud holds the deletion off (COMSIG_PREQDELETED) and parks the grenade in nullspace
            // until its shards land, as in tg
            Assert.True(QDELETED(grenade) || grenade.loc == null, "the grenade is still on the map");
        });
    }

    [Fact]
    public void AshMushroomsArePickedByHandAndRegrowAndTheFireBlossomDims()
    {
        var (r, feed, bob) = Room(9507);
        var shrooms = Place<obj_structure_flora_ash>(r, 5, 4);
        string variant = r.In(() => shrooms.icon_state);
        Assert.Matches("^l_mushroom[1-4]$", variant);

        Click(r, bob, shrooms);
        r.Step(10 * Second);
        r.In(() =>
        {
            Assert.Equal(1f, shrooms.harvested);
            Assert.Equal(variant + "p", shrooms.icon_state);
            Assert.NotEmpty(Dm.each<obj_item_food_grown_ash_flora_shavings>(locate(4, 4, 1).contents).Concat(Dm.each<obj_item_food_grown_ash_flora_shavings>(locate(5, 4, 1).contents)));
        });
        r.In(() => shrooms.regrow());
        r.In(() =>
        {
            Assert.Equal(0f, shrooms.harvested);
            Assert.Equal(variant, shrooms.icon_state);
        });

        // the shavings grind to mushroom powder
        var shavings = r.In(() => Dm.@new<obj_item_food_grown_ash_flora_shavings>(locate(6, 6, 1)));
        r.In(() =>
        {
            var results = Waiting.NoSleep(shavings.grind_results()) as DmList;
            Assert.Equal(5f, Dm.num(results[typeof(datum_reagent_toxin_mushroom_powder)]));
        });

        // the fire blossom's light dims while picked (tg's swapped defines) and comes back; the glowgrowth goes dark
        var blossom = Place<obj_structure_flora_ash_fireblossom>(r, 6, 4);
        var glow = Place<obj_structure_flora_ash_glowgrowth>(r, 7, 4);
        r.In(() =>
        {
            Waiting.NoSleep(blossom.after_harvest(bob));
            Assert.Equal(LIGHT_RANGE_FIRE_BLOSSOM_HARVESTED, blossom.light_power);
            Assert.Equal(LIGHT_POWER_FIRE_BLOSSOM_HARVESTED, blossom.light_range);
            blossom.regrow();
            Assert.Equal(LIGHT_FIRE_BLOSSOM, blossom.light_power);
            Assert.Equal(LIGHT_FIRE_BLOSSOM, blossom.light_range);
            Assert.True(Dm.truthy(glow.light_on));
            Waiting.NoSleep(glow.after_harvest(bob));
            Assert.False(Dm.truthy(glow.light_on));
            glow.regrow();
            Assert.True(Dm.truthy(glow.light_on));
        });

        // the cacti prick (a caltrop) and the produce lies scattered
        var cacti = Place<obj_structure_flora_ash_cacti>(r, 8, 4);
        r.In(() => Assert.NotNull(cacti.GetComponent(typeof(datum_component_caltrop))));
        var fruit = r.In(() => Dm.@new<obj_item_food_grown_ash_flora_cactus_fruit>(locate(8, 6, 1)));
        r.In(() =>
        {
            Assert.InRange(Dm.num(fruit.pixel_x), -4f, 4f);
            Assert.InRange(Dm.num(fruit.pixel_y), -4f, 4f);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOilWellOilsItsToucherAndIsFilledInWithAShovel()
    {
        var (r, feed, bob) = Room(9508);
        var well = Place<obj_structure_sink_oil_well>(r, 5, 4);
        r.In(() => Assert.True(Dm.num(((datum_reagents)well.reagents).total_volume) > 0));
        Click(r, bob, well);
        r.Step(2);
        Assert.True(feed.Any("You touch the pool of oil"), Diag(feed, r));

        Hold<obj_item_shovel>(r, bob);
        Click(r, bob, well);
        r.Step(4);
        Assert.True(feed.Any("You fill in the oil well with soil."), Diag(feed, r));
        r.In(() =>
        {
            Assert.True(QDELETED(well));
            Assert.Single(Dm.each<obj_effect_decal_cleanable_blood_oil>(locate(5, 4, 1).contents));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGraveyardsGravesRocksAndBonesBootWithTheirContents()
    {
        var (r, feed, bob) = Room(9509);
        var grave = Place<obj_structure_closet_crate_grave_filled>(r, 5, 4);
        var lead = Place<obj_structure_closet_crate_grave_filled_lead_researcher>(r, 6, 4);
        var rib = Place<obj_structure_statue_bone_rib>(r, 7, 4);
        // a closet fills when it is first opened (tg's lazy contents): a dig spills the grave onto its tile
        r.In(() =>
        {
            Assert.Empty(Dm.each<obj_effect_decal_remains_human>(grave.contents));
            Waiting.Detach(grave.dump_contents());
            Waiting.Detach(lead.dump_contents());
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Single(Dm.each<obj_effect_decal_remains_human>(locate(5, 4, 1).contents)); // human remains may lie in a filled grave
            Assert.Single(Dm.each<obj_effect_decal_remains_human>(locate(6, 4, 1).contents));
            Assert.Single(Dm.each<obj_effect_decal_cleanable_blood_gibs_old>(locate(6, 4, 1).contents));
            Assert.Single(Dm.each<obj_item_book_granter_crafting_recipe_boneyard_notes>(locate(6, 4, 1).contents));
            Assert.False(InertSeam.IsInert(rib));
        });

        // the wasteland: its rock drops bone or ores, the cracked earth never breaks
        var rock = r.In(() => (turf_closed_mineral_strong_wasteland)Waiting.NoSleep(locate(9, 6, 1).ChangeTurf(typeof(turf_closed_mineral_strong_wasteland))));
        r.In(() => rock.drop_ores());
        r.In(() => Assert.True(Dm.each<obj_item_stack_sheet_bone>(rock.contents).Any() || Dm.each<obj_item_stack_ore_iron>(rock.contents).Any()));
        // an untrained miner cannot break the strong rock (Bob is no master miner)
        r.In(() => Waiting.NoSleep(rock.gets_drilled(bob)));
        r.Step(2);
        r.In(() => Assert.IsType<turf_closed_mineral_strong_wasteland>(locate(9, 6, 1)));
        r.In(() => Assert.False(Dm.truthy(rock.ex_act(EXPLODE_DEVASTATE))));
        var earth = r.In(() => (turf_open_misc_asteroid_basalt_wasteland)Waiting.NoSleep(locate(10, 6, 1).ChangeTurf(typeof(turf_open_misc_asteroid_basalt_wasteland))));
        r.In(() =>
        {
            Assert.StartsWith("wasteland", earth.icon_state);
            earth.break_tile();
            Assert.IsType<turf_open_misc_asteroid_basalt_wasteland>(locate(10, 6, 1));
        });
        r.AssertNoRuntimes();
    }
}
