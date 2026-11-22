using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package C4: vermin, space fauna and farm animals. Each creature spawns and goes away with no runtime; a mouse chews a live
/// cable and dies by the shock; a carp bites a player; a chicken lays and an egg hatches; a cow is milked; a sheep is sheared; a goat
/// retaliates; an animal is butchered with a knife; riding a tamed animal moves the rider; a slime latches on and feeds.
/// </summary>
public class CreaturesTests
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

    public static IEnumerable<object[]> Creatures() => new[]
    {
        typeof(mob_living_basic_mouse), typeof(mob_living_basic_mouse_brown_tom), typeof(mob_living_basic_mouse_rat), typeof(mob_living_basic_cockroach), typeof(mob_living_basic_cockroach_sewer),
        typeof(mob_living_basic_cockroach_bloodroach), typeof(mob_living_basic_cockroach_hauberoach), typeof(mob_living_basic_cockroach_glockroach), typeof(mob_living_basic_cockroach_glockroach_mobroach),
        typeof(mob_living_basic_mothroach), typeof(mob_living_basic_frog), typeof(mob_living_basic_frog_crazy), typeof(mob_living_basic_crab), typeof(mob_living_basic_axolotl), typeof(mob_living_basic_butterfly),
        typeof(mob_living_basic_bat), typeof(mob_living_basic_snail), typeof(mob_living_basic_stoat), typeof(mob_living_basic_stoat_kit), typeof(mob_living_basic_regal_rat),
        typeof(mob_living_basic_chicken), typeof(mob_living_basic_chick), typeof(mob_living_basic_cow), typeof(mob_living_basic_cow_moonicorn), typeof(mob_living_basic_goat), typeof(mob_living_basic_goat_pete),
        typeof(mob_living_basic_pig), typeof(mob_living_basic_sheep), typeof(mob_living_basic_rabbit), typeof(mob_living_basic_rabbit_easter), typeof(mob_living_basic_pony),
        typeof(mob_living_basic_slime), typeof(mob_living_basic_slime_pet), typeof(mob_living_basic_carp), typeof(mob_living_basic_carp_mega), typeof(mob_living_basic_carp_passive), typeof(mob_living_basic_carp_pet_lia),
        typeof(mob_living_basic_carp_advanced), typeof(mob_living_basic_carp_magic), typeof(mob_living_basic_carp_magic_chaos), typeof(mob_living_basic_bear), typeof(mob_living_basic_bear_russian), typeof(mob_living_basic_bear_butter),
        typeof(mob_living_basic_snake), typeof(mob_living_basic_snake_banded),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Creatures))]
    public void A_creature_spawns_thinks_on_its_own_tree_and_goes_away_cleanly(Type path)
    {
        var r = new AudioRoom(13, 9, seed: 8301);
        r.Join(11, 4, "Bob");
        var mob = (mob_living)r.In(() => Dm.@new(path, locate(4, 4, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(mob)));
        Assert.NotNull(mob.ai_controller);
        RunFor(r, 100);
        if (!r.In(() => QDELETED(mob)))
            r.In(() => qdel(mob));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mouse_chews_a_live_cable_and_is_toast_while_Tom_fears_no_cable()
    {
        var r = new AudioRoom(13, 9, seed: 8302);
        r.Join(11, 4, "Bob");
        var cable = r.In(() => Dm.@new<obj_structure_cable>(locate(4, 4, 1)));
        var mouse = r.In(() => Dm.@new<mob_living_basic_mouse>(locate(4, 4, 1)));
        // no zap: chewing a dead cable leaves the mouse alive and the cable gone
        r.In(() => Waiting.NoSleep(mouse.try_bite_cable(cable)));
        r.Step();
        Assert.True(r.In(() => QDELETED(cable)));
        Assert.False(r.In(() => QDELETED(mouse)));

        // a powered cable shocks: the mouse dies and leaves a blackened corpse item
        var live = r.In(() => Dm.@new<obj_structure_cable>(locate(5, 4, 1)));
        r.In(() => { mouse.cable_zap_prob = 100; live.powernet = Dm.@new<datum_powernet>(); live.powernet.avail = 1000; });
        var toast = r.In(() => { Waiting.NoSleep(mouse.try_bite_cable(live)); return Dm.locate<obj_item_food_deadmouse>(locate(4, 4, 1)); });
        Assert.NotNull(toast);
        Assert.Equal("They're toast.", toast.desc);
        Assert.Equal("#3A3A3A", r.In(() => toast.color));
        r.Step();
        Assert.True(r.In(() => QDELETED(mouse)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_carp_hunts_and_bites_a_player()
    {
        var r = new AudioRoom(13, 9, seed: 8303);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var carp = r.In(() => Dm.@new<mob_living_basic_carp>(locate(5, 4, 1)));
        float start = r.In(() => Dm.num(bob.health));
        Assert.True(RunUntil(r, 300, () => Dm.num(bob.health) < start), "the carp reached Bob and bit him");
        Assert.False(r.In(() => QDELETED(carp)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_chicken_lays_when_fed_and_a_fertile_egg_hatches_into_a_chick()
    {
        var r = new AudioRoom(13, 9, seed: 8304);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var hen = r.In(() => Dm.@new<mob_living_basic_chicken>(locate(4, 4, 1)));
        var layer = r.In(() => hen.GetComponent(typeof(datum_component_egg_layer)) as datum_component_egg_layer);
        Assert.NotNull(layer);
        var wheat = r.In(() => Dm.@new<obj_item_food_grown_wheat>(locate(4, 4, 1)));
        r.In(() => layer.feed_food(hen, wheat, bob));
        r.Step();
        Assert.True(r.In(() => QDELETED(wheat)));
        Assert.True(r.In(() => Dm.num(layer.eggs_left) > 0));
        Assert.True(RunUntil(r, 8000, () => Dm.each<obj_item_food_egg_organic>(world).Any()), "the hen laid");

        var egg = r.In(() => Dm.@new<obj_item_food_egg_fertile>(locate(7, 7, 1)));
        var fertile = r.In(() => egg.GetComponent(typeof(datum_component_fertile_egg)) as datum_component_fertile_egg);
        r.In(() => fertile.current_growth = 199f);
        Assert.True(RunUntil(r, 100, () => QDELETED(egg)), "the egg hatched");
        Assert.True(r.In(() => Dm.each<mob_living_basic_chick>(world).Any()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cow_is_milked_into_a_cup_and_can_be_tipped()
    {
        var r = new AudioRoom(13, 9, seed: 8305);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var cow = r.In(() => Dm.@new<mob_living_basic_cow>(locate(4, 4, 1)));
        var cup = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(5, 4, 1)));
        var udder = r.In(() => cow.GetComponent(typeof(datum_component_udder)) as datum_component_udder);
        r.In(() => udder.on_attackby(cow, cup, bob));
        RunFor(r, 2);
        Assert.True(r.In(() => Dm.truthy((cup.reagents as datum_reagents).has_reagent(typeof(datum_reagent_consumable_milk)))));

        var tip = r.In(() => cow.GetComponent(typeof(datum_component_tippable)) as datum_component_tippable);
        r.In(() => Waiting.NoSleep(tip.do_tip(cow, bob)));
        Assert.True(r.In(() => HAS_TRAIT(cow, TRAIT_MOB_TIPPED)));
        r.In(() => Waiting.NoSleep(tip.do_untip(cow, bob)));
        Assert.False(r.In(() => HAS_TRAIT(cow, TRAIT_MOB_TIPPED)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_sheep_is_sheared_with_a_razor_and_shows_tgs_harvested_state()
    {
        var r = new AudioRoom(13, 9, seed: 8306);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var sheep = r.In(() => Dm.@new<mob_living_basic_sheep>(locate(4, 4, 1)));
        var harvest = r.In(() => sheep.GetComponent(typeof(datum_component_mob_harvest)) as datum_component_mob_harvest);
        Assert.Equal("sheep", r.In(() => sheep.icon_state));
        r.In(() => { harvest.amount_ready = 1; harvest.item_harvest_time = 0; });
        r.In(() => Waiting.Detach(harvest.harvest_item(bob)));
        Assert.True(RunUntil(r, 40, () => Dm.each<obj_item_stack_sheet_cotton_wool>(world).Any()), "the wool came off");
        Assert.Equal(0f, r.In(() => harvest.amount_ready));
        Assert.Equal("sheep_harvested", r.In(() => sheep.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goat_retaliates_against_who_hits_it()
    {
        var r = new AudioRoom(13, 9, seed: 8307);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var goat = r.In(() => Dm.@new<mob_living_basic_goat_pete>(locate(4, 4, 1)));
        float start = r.In(() => Dm.num(bob.health));
        // tg's goat is capricious (capricious_pick_target): 0.5% a try to fly into a rage at someone in earshot; here it always does
        r.In(() => (goat.ai_controller as datum_ai_controller).set_blackboard_key(BB_RANDOM_AGGRO_CHANCE, 100f));
        r.In(() => (goat.ai_controller as datum_ai_controller).set_blackboard_key(BB_RANDOM_DEAGGRO_CHANCE, 0.01f));
        // tg's melee leaf rechecks the faction with the retaliate leaf's temporary flag already reset (a quirk of the snapshot):
        // a goat only strikes someone outside its neutral faction, so Bob is not neutral here.
        r.In(() => bob.set_faction(Dm.list(FACTION_HOSTILE)));
        r.In(() => goat._SendSignal(COMSIG_ATOM_WAS_ATTACKED, Dm.list(goat, bob, NONE)));
        Assert.True(RunUntil(r, 300, () => Dm.num(bob.health) < start), "Pete charged back");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_animal_is_butchered_with_a_knife_into_its_meat()
    {
        var r = new AudioRoom(13, 9, seed: 8308);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var knife = r.In(() => Dm.@new<obj_item_knife_butcher>(locate(5, 4, 1)));
        var pig = r.In(() => Dm.@new<mob_living_basic_pig>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(pig.death()));
        Assert.Equal("pig_dead", r.In(() => pig.icon_state));
        var butcher = r.In(() => knife.GetComponent(typeof(datum_component_butchering)) as datum_component_butchering);
        Assert.NotNull(butcher);
        r.In(() => { bob.combat_mode = TRUE; });
        Assert.Equal(COMPONENT_CANCEL_ATTACK_CHAIN, r.In(() => butcher.on_item_attack(knife, pig, bob)));
        Assert.True(RunUntil(r, 200, () => Dm.each<obj_item_food_meat_slab_pig>(world).Count() >= 6), "six slabs of pork");
        Assert.True(RunUntil(r, 20, () => QDELETED(pig)), "the body is gibbed");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Riding_a_tamed_pig_moves_the_rider_with_it()
    {
        var r = new AudioRoom(13, 9, seed: 8309);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var pig = r.In(() => Dm.@new<mob_living_basic_pig>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(pig.tamed(bob, null)));
        Assert.True(r.In(() => Dm.truthy(pig.can_buckle)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(pig.buckle_mob(bob, force: TRUE)))));
        Assert.Same(pig, r.In(() => bob.buckled));
        Assert.NotNull(r.In(() => pig.GetComponent(typeof(datum_component_riding_creature_pig))));
        Assert.True(r.In(() => HAS_TRAIT(pig, TRAIT_AI_PAUSED)), "the rider drives, not the AI");
        var before = r.In(() => pig.x);
        r.In(() => pig.relaydrive(bob, EAST));
        RunFor(r, 2);
        Assert.Equal(before + 1, r.In(() => pig.x));
        Assert.Equal(r.In(() => pig.x), r.In(() => bob.x));
        r.In(() => Waiting.NoSleep(pig.unbuckle_mob(bob)));
        r.Step();
        r.AssertNoRuntimes();
        Assert.Null(r.In(() => pig.GetComponent(typeof(datum_component_riding_creature_pig))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_slime_latches_on_feeds_and_lets_go_when_wrestled_off()
    {
        var r = new AudioRoom(13, 9, seed: 8310);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(locate(4, 4, 1)));
        Assert.Equal("grey-baby", r.In(() => slime.icon_state));
        Assert.True(r.In(() => Dm.truthy(slime.can_feed_on(bob))));
        r.In(() => Waiting.NoSleep(slime.start_feeding(bob)));
        Assert.Same(bob, r.In(() => slime.buckled));
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_slime_leech)))));
        float start = r.In(() => Dm.num(bob.get_brute_loss()));
        float nutrition = r.In(() => Dm.num(slime.nutrition));
        Assert.True(RunUntil(r, 100, () => Dm.num(bob.get_brute_loss()) > start), "the leech drains");
        Assert.True(r.In(() => Dm.num(slime.nutrition)) > nutrition - 5);
        r.In(() => slime.stop_feeding());
        r.Step();
        Assert.Null(r.In(() => slime.buckled));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_grown_adult_slime_splits_into_four()
    {
        var r = new AudioRoom(13, 9, seed: 8311);
        r.Join(11, 4, "Bob");
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(locate(4, 4, 1), typeof(datum_slime_type_grey), SLIME_LIFE_STAGE_ADULT));
        Assert.Equal("grey-adult", r.In(() => slime.icon_state));
        r.In(() => { slime.amount_grown = SLIME_EVOLUTION_THRESHOLD; });
        r.In(() => Waiting.NoSleep(slime.reproduce()));
        Assert.Equal(4, r.In(() => Dm.each<mob_living_basic_slime>(world).Count()));
        Assert.Equal(SLIME_LIFE_STAGE_BABY, r.In(() => slime.life_stage));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Minor_mapping_only_drops_vermin_on_breathable_exposed_wires()
    {
        var r = new AudioRoom(13, 9, seed: 8312);
        var plating = r.In(() => locate(4, 4, 1) as turf);
        var ss = r.In(() => Dm.@new<datum_controller_subsystem_minor_mapping>());
        Assert.False(r.In(() => Dm.truthy(ss.valid_mouse_turf(null))));
        Assert.True(r.In(() => Dm.truthy(ss.valid_mouse_turf(plating as turf_open))));
        r.AssertNoRuntimes();
    }
}
