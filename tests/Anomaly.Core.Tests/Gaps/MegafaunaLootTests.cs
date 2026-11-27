using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The megafauna's loot: the necropolis chests and the crusher's chests with their trophies, the lava staff, the hierophant club,
/// the staff of storms, the resurrection crystal, the ice trail boots, the cleaving saw, the bottle of mayhem, and the megafauna's
/// crusher trophies on a crusher, each doing what tg's does when the crusher's mark is detonated.
/// </summary>
public class MegafaunaLootTests
{
    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    private static bool RunUntil(AudioRoom r, float deciseconds, Func<bool> done)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
        {
            if (r.In(done))
                return true;
            r.Step();
        }
        return r.In(done);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        var item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    // tgui_alert's AlertModal answered through its window
    private static void Answer(AudioRoom r, mob who, string choice)
    {
        r.Step(4);
        string window = null;
        foreach (TguiOutput o in Take(who))
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() == "update" && InterfaceOf(doc.RootElement.GetProperty("payload")) == "AlertModal")
                window = o.WindowId;
        }
        Assert.NotNull(window);
        Act(r, who, window, "choose", ("choice", choice));
    }

    [Theory]
    [InlineData(typeof(obj_structure_closet_crate_necropolis_dragon_crusher), typeof(obj_item_crusher_trophy_tail_spike))]
    [InlineData(typeof(obj_structure_closet_crate_necropolis_bubblegum_crusher), typeof(obj_item_crusher_trophy_demon_claws))]
    [InlineData(typeof(obj_structure_closet_crate_necropolis_colossus_crusher), typeof(obj_item_crusher_trophy_blaster_tubes))]
    public void A_crushers_chest_opens_on_the_megafaunas_relic_and_its_trophy(Type chest, Type trophy)
    {
        var r = new AudioRoom(11, 9, seed: 13321);
        var crate = (obj_structure_closet)r.In(() => Dm.@new(chest, locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(crate.open(null, TRUE)));
        RunFor(r, 10);
        var items = r.In(() => Dm.each<atom>(range(1, locate(5, 4, 1))).Where(a => a is obj_item).ToList());
        Assert.Contains(items, a => trophy.IsInstanceOfType(a));
        Assert.True(items.Count >= 2, "the relic came with the trophy");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_drakes_chest_holds_one_of_its_relics()
    {
        var r = new AudioRoom(11, 9, seed: 13322);
        var crate = r.In(() => Dm.@new<obj_structure_closet_crate_necropolis_dragon>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(crate.open(null, TRUE)));
        RunFor(r, 10);
        var relics = new[] { typeof(obj_item_melee_ghost_sword), typeof(obj_item_lava_staff), typeof(obj_item_book_granter_action_spell_sacredflame), typeof(obj_item_dragons_blood) };
        Assert.True(r.In(() => Dm.each<atom>(range(1, locate(5, 4, 1))).Count(a => relics.Any(t => t.IsInstanceOfType(a)))) == 1, "one relic");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lava_staff_turns_a_floor_into_lava()
    {
        var r = new AudioRoom(11, 9, seed: 13323);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(20);
        var staff = Hold<obj_item_lava_staff>(r, bob);
        turf target = r.In(() => locate(6, 4, 1));
        r.Command(() => Waiting.Detach(staff.ranged_interact_with_atom(target, bob)));
        Assert.True(RunUntil(r, 60, () => islava(locate(6, 4, 1))), "the floor turned to lava");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_hierophant_club_blinks_its_wielder_and_leaves_its_beacon()
    {
        var r = new AudioRoom(13, 9, seed: 13324);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(20);
        var club = Hold<obj_item_hierophant_club>(r, bob);
        // the blink is on from the start; the club's own use turns it off and on
        Assert.Equal(TRUE, r.In(() => club.blink_activated));
        r.In(() => Waiting.NoSleep(club.attack_self(bob)));
        Assert.Equal(FALSE, r.In(() => club.blink_activated));
        r.In(() => Waiting.NoSleep(club.attack_self(bob)));
        Assert.Equal(TRUE, r.In(() => club.blink_activated));
        r.Command(() => Waiting.Detach(club.ranged_interact_with_atom(locate(7, 4, 1), bob)));
        Assert.True(RunUntil(r, 40, () => bob.loc == locate(7, 4, 1)), "the club blinked Bob");
        r.Command(() => Waiting.Detach(club.deploy_beacon(bob)));
        Assert.True(RunUntil(r, 80, () => club.beacon != null), "the beacon was set down");
        Assert.Equal(r.In(() => locate(7, 4, 1)), r.In(() => club.beacon.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_staff_of_storms_calls_a_thunderbolt_down_on_its_target()
    {
        var r = new AudioRoom(13, 9, seed: 13325);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human dummy = r.Dummy(8, 4);
        r.Step(20);
        var staff = Hold<obj_item_storm_staff>(r, bob);
        float before = r.In(() => Dm.num(dummy.health));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(staff.ranged_interact_with_atom(dummy, bob))));
        Assert.True(RunUntil(r, 40, () => Dm.num(dummy.health) < before), "the bolt struck");
        Assert.True(r.In(() => Dm.num(staff.thunder_charges)) < r.In(() => Dm.num(Dm.initial(staff, "thunder_charges"))), "a charge was spent");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_resurrection_crystal_drags_its_owner_out_of_death_into_a_shadow()
    {
        var r = new AudioRoom(13, 9, seed: 13326);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        // the room is lit, so no dark maintenance spot takes the new body: a safe turf on the station's level does,
        r.In(() =>
        {
            SSmapping.z_list = Dm.list();
            SSmapping.z_level_to_plane_offset ??= Dm.list();
            SSmapping.z_level_to_lowest_plane_offset ??= Dm.list();
            SSmapping.gravity_by_z_level ??= Dm.list();
            SSmapping.z_level_to_stack ??= Dm.list();
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))), FALSE, contain_turfs: FALSE);
            // and a safe turf is a station floor with air: the toy room's indestructible floor is none, so the far half is tiled
            for (int x = 7; x <= 11; x++)
                for (int y = 2; y <= 7; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron)));
        });
        r.Step(20);
        var crystal = Hold<obj_item_resurrection_crystal>(r, bob);
        r.In(() => Waiting.NoSleep(crystal.attack_self(bob)));
        Assert.Equal(bob, r.In(() => crystal.loc));
        r.In(() => Waiting.Detach(bob.death(FALSE)));
        Assert.True(RunUntil(r, 40, () => bob.stat != DEAD && QDELETED(crystal)), "Bob came back: " + string.Join("\n", r.Room.RuntimeText));
        Assert.True(r.In(() => bob.dna.species is datum_species_shadow), "as a shadow");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_ice_trail_boots_freeze_the_floor_they_step_on()
    {
        var r = new AudioRoom(11, 9, seed: 13327);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        var boots = r.In(() => Dm.@new<obj_item_clothing_shoes_winterboots_ice_boots_ice_trail>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(bob.shoes, TRUE));
            Waiting.NoSleep(bob.equip_to_slot_or_del(boots, ITEM_SLOT_FEET));
            Waiting.NoSleep(boots.ui_action_click(bob, null));
        });
        Assert.True(r.In(() => HAS_TRAIT(boots, TRAIT_NODROP)), "the boots are cursed on");
        r.Command(() => Waiting.Detach(bob.Move(locate(5, 4, 1), EAST)));
        Assert.True(RunUntil(r, 20, () => Dm.each<turf>(range(1, bob)).Any(t => t is turf_open_misc_ice_icemoon_no_planet_atmos)), "the floor froze");
        RunFor(r, 80);
        Assert.False(r.In(() => Dm.each<turf>(range(2, bob)).Any(t => t is turf_open_misc_ice_icemoon_no_planet_atmos)), "the ice thawed");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_cleaving_saw_opens_and_bleeds_the_fauna_it_cuts()
    {
        var r = new AudioRoom(11, 9, seed: 13328);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        var saw = Hold<obj_item_melee_cleaving_saw>(r, bob);
        var goliath = r.In(() =>
        {
            var g = Dm.@new<mob_living_basic_mining_goliath>(locate(5, 4, 1));
            g.ai_controller = null;
            return g;
        });
        r.Step(2);
        r.In(() => Waiting.NoSleep(saw.melee_attack_chain(bob, goliath)));
        Assert.NotNull(r.In(() => goliath.has_status_effect(typeof(datum_status_effect_stacking_saw_bleed))));
        r.In(() => Waiting.NoSleep(saw.attack_self(bob)));
        Assert.True(r.In(() => HAS_TRAIT(saw, TRAIT_TRANSFORM_ACTIVE)), "the saw opened");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bottle_of_mayhem_sends_everyone_near_into_a_frenzy()
    {
        var r = new AudioRoom(11, 9, seed: 13329);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human dummy = r.Dummy(7, 4);
        r.Step(20);
        var bottle = Hold<obj_item_mayhem>(r, bob);
        Take(bob);
        r.Command(() => Waiting.Detach(bottle.attack_self(bob)));
        Answer(r, bob, "Break it!");
        Assert.True(RunUntil(r, 20, () => QDELETED(bottle)), "the bottle broke");
        Assert.NotNull(r.In(() => dummy.has_status_effect(typeof(datum_status_effect_mayhem))));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_mayhem))));
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    public static IEnumerable<object[]> Trophies() => new[]
    {
        typeof(obj_item_crusher_trophy_miner_eye), typeof(obj_item_crusher_trophy_tail_spike), typeof(obj_item_crusher_trophy_demon_claws),
        typeof(obj_item_crusher_trophy_blaster_tubes), typeof(obj_item_crusher_trophy_vortex_talisman), typeof(obj_item_crusher_trophy_ice_block_talisman),
        typeof(obj_item_crusher_trophy_wendigo_horn), typeof(obj_item_crusher_trophy_flesh_glob),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Trophies))]
    public void A_megafauna_trophy_goes_on_a_crusher_and_works_when_its_mark_is_detonated(Type path)
    {
        var r = new AudioRoom(13, 9, seed: 13330);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        var crusher = Hold<obj_item_kinetic_crusher>(r, bob);
        var trophy = (obj_item_crusher_trophy)r.In(() => Dm.@new(path, bob.loc));
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(trophy.add_to(crusher, bob))));
        Assert.Contains(trophy, r.In(() => Dm.each<obj_item_crusher_trophy>(crusher.trophies).ToList()));
        Assert.False(string.IsNullOrEmpty(r.In(() => trophy.effect_desc())));
        // a still hostile animal (a goliath stands too heavy for the spike to push)
        var goliath = r.In(() =>
        {
            var g = Dm.@new<mob_living_simple_animal_hostile>(locate(6, 4, 1));
            g.toggle_ai(AI_OFF);
            return g;
        });
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(20)));
        float bobHurt = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => Waiting.Detach(trophy.on_mark_detonation(goliath, bob, crusher)));
        RunFor(r, 30);
        if (trophy is obj_item_crusher_trophy_ice_block_talisman)
            Assert.NotNull(r.In(() => goliath.has_status_effect(typeof(datum_status_effect_ice_block_talisman))));
        if (trophy is obj_item_crusher_trophy_tail_spike)
            Assert.True(r.In(() => get_dist(bob, goliath)) > 2, "the spike pushed the animal back");
        if (trophy is obj_item_crusher_trophy_demon_claws)
            Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) < bobHurt, "the claws healed Bob");
        if (trophy is obj_item_crusher_trophy_vortex_talisman)
            Assert.True(r.In(() => Dm.num(goliath.health) < Dm.num(goliath.maxHealth)), "the chaser caught the animal");
        RunFor(r, 60);
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(trophy.remove_from(crusher, bob))));
        r.AssertNoRuntimes();
    }
}
