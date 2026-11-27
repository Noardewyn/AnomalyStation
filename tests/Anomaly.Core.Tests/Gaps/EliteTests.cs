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
/// Lavaland's elites and their pulsing tumor: a touch wakes the tumor and an elite climbs out into an arena walled off around it;
/// an elite that dies leaves a chest with its loot or a tumor shard when the tumor was fed a core, and one that kills its
/// activator stays on, healing on its tumor; each elite's attacks (the goliath broodmother, the herald, the legionnaire, pandora)
/// do what tg's do.
/// </summary>
public class EliteTests
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

    private static int Count<T>(AudioRoom r) where T : class =>
        r.In(() => Dm.each<atom>(range(30, locate(1, 1, 1))).Count(a => a is T));

    public static IEnumerable<object[]> Elites() => new[]
    {
        typeof(mob_living_simple_animal_hostile_asteroid_elite_broodmother), typeof(mob_living_simple_animal_hostile_asteroid_elite_broodmother_child),
        typeof(mob_living_simple_animal_hostile_asteroid_elite_herald), typeof(mob_living_simple_animal_hostile_asteroid_elite_herald_mirror),
        typeof(mob_living_simple_animal_hostile_asteroid_elite_legionnaire), typeof(mob_living_simple_animal_hostile_asteroid_elite_legionnairehead),
        typeof(mob_living_simple_animal_hostile_asteroid_elite_pandora),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Elites))]
    public void An_elite_spawns_fights_and_goes_away_cleanly(Type path)
    {
        var r = new AudioRoom(17, 13, seed: 13460);
        mob_living_carbon_human bob = r.Dummy(13, 6);
        var mob = (mob_living)r.In(() => Dm.@new(path, locate(4, 6, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(mob)), path.Name + " is inert");
        RunFor(r, 150);
        if (!r.In(() => QDELETED(bob)))
            r.In(() => qdel(bob));
        RunFor(r, 60);
        if (!r.In(() => QDELETED(mob)))
            r.In(() => qdel(mob));
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_touch_wakes_the_tumor_an_elite_climbs_out_and_the_arena_closes_around_them()
    {
        var r = new AudioRoom(27, 27, seed: 13461);
        mob_living_carbon_human bob = r.Join(13, 13, "Bob");
        r.Step(20);
        var tumor = r.In(() => Dm.@new<obj_structure_elite_tumor>(locate(14, 14, 1)));
        Assert.NotNull(r.In(() => tumor.GetComponent(typeof(datum_component_gps))));
        r.Command(() => Waiting.Detach(tumor.attack_hand(bob)));
        r.Step(2);
        Assert.Equal(obj_structure_elite_tumor.TUMOR_ACTIVE, r.In(() => tumor.activity));
        Assert.Equal(bob, r.In(() => tumor.activator));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_ELITE_CHALLENGER)), "Bob is the challenger");
        Assert.True(RunUntil(r, 50, () => tumor.mychild != null), "an elite climbed out");
        Assert.Equal("tumor_popped", r.In(() => tumor.icon_state));
        Assert.True(r.In(() => tumor.mychild.loc == tumor.loc), "on the tumor");
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_temp_visual_elite_tumor_wall>(r) > 0), "the arena's walls rose");
        Assert.True(r.In(() => Dm.each<obj_effect_temp_visual_elite_tumor_wall>(range(13, tumor)).All(w => get_dist(w, tumor) == 12)), "twelve tiles out");

        // the fighters cannot pass the wall, a stranger can
        var wall = r.In(() => Dm.each<obj_effect_temp_visual_elite_tumor_wall>(range(13, tumor)).First());
        mob_living_carbon_human stranger = r.Dummy(2, 2);
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(wall.CanAllowThrough(bob, NORTH)))), "the wall holds the challenger");
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(wall.CanAllowThrough(stranger, NORTH)))), "the wall lets a stranger by");

        // a challenger taken out of the arena is put back on the tumor
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(1, 1, 1))));
        Assert.True(RunUntil(r, 60, () => bob.loc == tumor.loc || get_dist(bob, tumor) < 12), "the challenger was pulled back in");

        // a second touch does nothing
        r.In(() => Waiting.NoSleep(tumor.attack_hand(bob)));
        Assert.Equal(obj_structure_elite_tumor.TUMOR_ACTIVE, r.In(() => tumor.activity));
        r.In(() => tumor.mychild.toggle_ai(AI_OFF));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_elite_that_dies_leaves_the_tumor_as_a_chest()
    {
        var r = new AudioRoom(27, 27, seed: 13462);
        mob_living_carbon_human bob = r.Join(13, 13, "Bob");
        r.Step(20);
        var tumor = r.In(() => Dm.@new<obj_structure_elite_tumor>(locate(14, 14, 1)));
        r.Command(() => Waiting.Detach(tumor.attack_hand(bob)));
        Assert.True(RunUntil(r, 50, () => tumor.mychild != null), "an elite climbed out");
        var elite = r.In(() => tumor.mychild);
        r.In(() => elite.toggle_ai(AI_OFF));
        r.In(() => Waiting.Detach(elite.adjust_brute_loss(Dm.num(elite.maxHealth) * 2, forced: TRUE)));
        Assert.True(RunUntil(r, 60, () => QDELETED(tumor)), "the tumor dissipated");
        var chest = r.In(() => Dm.each<obj_structure_closet_crate_necropolis_tendril>(locate(14, 14, 1)).FirstOrDefault());
        Assert.NotNull(chest);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_ELITE_CHALLENGER)), "Bob is no longer a challenger");
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_regenerative_core_boosts_the_tumor_and_its_elite_then_drops_its_loot()
    {
        var r = new AudioRoom(27, 27, seed: 13463);
        r.In(() => r.World.Rng.Reseed(13463, 1));
        mob_living_carbon_human bob = r.Join(13, 13, "Bob");
        r.Step(20);
        var tumor = r.In(() => Dm.@new<obj_structure_elite_tumor>(locate(14, 14, 1)));
        var core = r.In(() =>
        {
            var c = Dm.@new<obj_item_organ_monster_core_regenerative_core>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(c));
            return c;
        });
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(tumor.item_interaction(bob, core))));
        Assert.True(r.In(() => Dm.truthy(tumor.boosted)), "the tumor swelled");
        Assert.Equal("advanced_tumor", r.In(() => tumor.icon_state));
        Assert.True(r.In(() => QDELETED(core)), "the core was eaten");

        // a boosted tumor's elite: as the loot comes out, the boost's shard or the elite's own loot
        var elite = r.In(() =>
        {
            var e = Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_pandora>(tumor.loc);
            tumor.mychild = e;
            tumor.activity = obj_structure_elite_tumor.TUMOR_ACTIVE;
            e.toggle_ai(AI_OFF);
            return e;
        });
        r.In(() => tumor.onEliteLoss());
        r.Step(2);
        var chest = r.In(() => Dm.each<obj_structure_closet_crate_necropolis_tendril>(locate(14, 14, 1)).First());
        Assert.True(r.In(() => Dm.each<atom>(chest.contents).Any(a => a is obj_item_tumor_shard || a is obj_item_clothing_accessory_pandora_hope)), "the chest holds a shard or pandora's hope");
        r.In(() => qdel(elite));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_elite_that_kills_its_activator_stays_on_its_tumor_and_heals_there()
    {
        var r = new AudioRoom(27, 27, seed: 13464);
        mob_living_carbon_human bob = r.Join(13, 13, "Bob");
        r.Step(20);
        var tumor = r.In(() => Dm.@new<obj_structure_elite_tumor>(locate(14, 14, 1)));
        r.Command(() => Waiting.Detach(tumor.attack_hand(bob)));
        Assert.True(RunUntil(r, 50, () => tumor.mychild != null), "an elite climbed out");
        var elite = r.In(() => tumor.mychild);
        r.In(() => elite.toggle_ai(AI_OFF));
        r.In(() => Waiting.Detach(bob.death(FALSE)));
        Assert.True(RunUntil(r, 80, () => tumor.activity == obj_structure_elite_tumor.TUMOR_PASSIVE), "the elite won");
        Assert.Null(r.In(() => tumor.activator));
        Assert.False(r.In(() => HAS_TRAIT(elite, TRAIT_UNCONVERTABLE)), "the winner can be converted again");
        r.In(() => Waiting.NoSleep(elite.adjust_brute_loss(100, forced: TRUE)));
        float hurt = r.In(() => Dm.num(elite.health));
        Assert.True(RunUntil(r, 60, () => Dm.num(elite.health) > hurt), "the elite healed on its tumor");

        // a new challenger calls it back to fight at full strength
        mob_living_carbon_human jim = r.Dummy(13, 14);
        r.In(() => Waiting.NoSleep(elite.forceMove(locate(20, 20, 1))));
        r.Command(() => Waiting.Detach(tumor.attack_hand(jim)));
        Assert.True(RunUntil(r, 50, () => elite.loc == tumor.loc), "the elite was returned to the tumor");
        Assert.Equal(r.In(() => Dm.num(elite.maxHealth)), r.In(() => Dm.num(elite.health)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_tumor_shard_revives_a_fallen_elite_on_its_users_side()
    {
        var r = new AudioRoom(13, 9, seed: 13465);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human eve = r.Join(8, 6, "Eve");
        r.Step(20);
        var elite = r.In(() => Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_herald>(locate(6, 4, 1)));
        r.In(() => elite.toggle_ai(AI_OFF));
        var shard = r.In(() =>
        {
            var s = Dm.@new<obj_item_tumor_shard>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(s));
            return s;
        });
        // alive, or dead with no one in it: the shard does nothing
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(shard.interact_with_atom(elite, bob))));
        r.In(() => Waiting.NoSleep(elite.death(FALSE)));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(shard.interact_with_atom(elite, bob))));
        Assert.False(r.In(() => QDELETED(shard)));

        // a player's elite
        r.In(() => Waiting.NoSleep(eve.mind.transfer_to(elite)));
        float fullHealth = r.In(() => Dm.num(elite.maxHealth));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(shard.interact_with_atom(elite, bob))));
        Assert.True(r.In(() => QDELETED(shard)), "the shard went into the elite");
        Assert.NotEqual(DEAD, r.In(() => elite.stat));
        Assert.True(r.In(() => Dm.@in(REF(bob), elite.allies)), "the elite is Bob's ally now");
        Assert.Equal(fullHealth * 0.4f, r.In(() => Dm.num(elite.maxHealth)), 2);
        Assert.Equal(SENTIENCE_ORGANIC, r.In(() => Dm.num(elite.sentience_type)));
        Assert.False(r.In(() => HAS_TRAIT(elite, TRAIT_UNCONVERTABLE)));
        r.AssertNoRuntimes();
    }

    // ---- the goliath broodmother ----

    [Fact]
    public void The_broodmother_digs_tentacles_breeds_children_rages_and_calls_them()
    {
        var r = new AudioRoom(17, 13, seed: 13466);
        mob_living_carbon_human bob = r.Dummy(11, 6);
        var mother = r.In(() => Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_broodmother>(locate(5, 6, 1)));
        r.In(() => mother.toggle_ai(AI_OFF));
        r.Step(2);
        r.In(() => mother.tentacle_patch(bob));
        Assert.True(r.In(() => Dm.each<atom>(range(2, bob)).Count(a => a is obj_effect_goliath_tentacle_broodmother) >= 5), "a patch of tentacles rose under Bob");
        r.In(() => Waiting.NoSleep(mother.spawn_children(bob)));
        Assert.Equal(2, r.In(() => mother.children_list.len));
        r.In(() => Waiting.NoSleep(mother.rage()));
        Assert.Equal(COLOR_RED, r.In(() => mother.color as string));
        Assert.True(RunUntil(r, 80, () => mother.color as string == COLOR_WHITE), "the rage passed");
        r.In(() => Waiting.NoSleep(mother.call_children()));
        Assert.True(r.In(() => Dm.each<mob>(mother.children_list).All(c => get_dist(c, mother) <= 1)), "the children came to their mother");
        // a child blows up when it dies, and leaves the list
        var child = r.In(() => Dm.each<mob_living_simple_animal_hostile_asteroid_elite_broodmother_child>(mother.children_list).First());
        r.In(() => Waiting.Detach(child.death(FALSE)));
        Assert.True(RunUntil(r, 20, () => mother.children_list.len == 1), "the child left the list");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    // ---- the herald ----

    [Fact]
    public void The_herald_fires_its_volleys_teleports_on_its_shot_and_casts_its_mirror()
    {
        var r = new AudioRoom(17, 13, seed: 13467);
        mob_living_carbon_human bob = r.Dummy(12, 6);
        var herald = r.In(() => Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_herald>(locate(5, 6, 1)));
        r.In(() => herald.toggle_ai(AI_OFF));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => herald.herald_trishot(bob));
        Assert.True(RunUntil(r, 10, () => Count<obj_projectile_herald>(r) > 0), "the trishot flew");
        Assert.True(RunUntil(r, 60, () => Dm.num(bob.health) < before), "a shot hit Bob");
        r.In(() => herald.herald_directionalshot());
        Assert.True(RunUntil(r, 10, () => Count<obj_projectile_herald>(r) >= 4), "the directional volley flew");
        RunFor(r, 40);
        r.In(() => herald.herald_teleshot(bob));
        Assert.True(RunUntil(r, 60, () => get_dist(herald, bob) <= 1 || Count<obj_projectile_herald_teleshot>(r) > 0), "the teleshot flew");
        RunFor(r, 40);
        r.In(() => Waiting.NoSleep(herald.herald_mirror()));
        Assert.True(r.In(() => herald.my_mirror != null), "the mirror appeared");
        var mirror = r.In(() => herald.my_mirror);
        r.In(() => Waiting.Detach(herald.death(FALSE)));
        Assert.True(RunUntil(r, 20, () => QDELETED(mirror)), "the mirror went with the dying herald");
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_heralds_cloak_answers_a_hit_with_its_shots()
    {
        var r = new AudioRoom(13, 9, seed: 13468);
        r.In(() => r.World.Rng.Reseed(13468, 1));
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.Step(20);
        var cloak = r.In(() => Dm.@new<obj_item_clothing_neck_cloak_herald_cloak>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(cloak, ITEM_SLOT_NECK, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))));
        bool shot = false;
        for (int i = 0; i < 30 && !shot; i++)
        {
            r.In(() => Waiting.NoSleep(cloak.hit_reaction(bob, null, "the attack", 0, 10, MELEE_ATTACK, BRUTE)));
            r.Step(1);
            shot = Count<obj_projectile_herald>(r) > 0;
        }
        Assert.True(shot, "the cloak answered with its shots");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    // ---- the legionnaire ----

    [Fact]
    public void The_legionnaire_charges_loses_its_head_lights_its_bonfire_and_spews_smoke()
    {
        var r = new AudioRoom(17, 13, seed: 13469);
        mob_living_carbon_human bob = r.Dummy(12, 6);
        var legionnaire = r.In(() => Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_legionnaire>(locate(5, 6, 1)));
        r.In(() => legionnaire.toggle_ai(AI_OFF));
        r.Step(2);
        r.In(() => legionnaire.legionnaire_charge(bob));
        Assert.True(RunUntil(r, 40, () => get_dist(legionnaire, bob) <= 2 || legionnaire.x > 6), "the legionnaire charged");
        RunFor(r, 30);

        r.In(() => Waiting.NoSleep(legionnaire.head_detach(bob)));
        Assert.True(r.In(() => legionnaire.myhead != null), "the head flew off");
        var head = r.In(() => legionnaire.myhead);
        r.In(() => Waiting.Detach(head.death(FALSE)));
        Assert.True(RunUntil(r, 20, () => legionnaire.myhead == null), "the head died");
        Assert.True(RunUntil(r, 70, () => Dm.truthy(legionnaire.has_head)), "the legionnaire regained its head");

        r.In(() => Waiting.NoSleep(legionnaire.bonfire_teleport()));
        Assert.True(r.In(() => legionnaire.mypile != null), "the bonfire was lit");
        var pile = r.In(() => legionnaire.mypile);
        var pileTurf = r.In(() => pile.loc);
        r.In(() => Waiting.NoSleep(legionnaire.forceMove(locate(10, 3, 1))));
        r.In(() => Waiting.NoSleep(legionnaire.bonfire_teleport()));
        Assert.True(r.In(() => legionnaire.loc == pileTurf && pile.loc == locate(10, 3, 1)), "the legionnaire swapped places with its bonfire");

        r.In(() => Waiting.NoSleep(legionnaire.spew_smoke()));
        Assert.True(RunUntil(r, 20, () => Count<obj_effect_particle_effect_fluid_smoke>(r) > 0), "the smoke spread");
        RunFor(r, 40);
        r.AssertNoRuntimes();
    }

    // ---- pandora ----

    [Fact]
    public void Pandora_fires_its_lines_and_boxes_teleports_and_spreads_its_squares()
    {
        var r = new AudioRoom(17, 13, seed: 13470);
        mob_living_carbon_human bob = r.Dummy(11, 6);
        var pandora = r.In(() => Dm.@new<mob_living_simple_animal_hostile_asteroid_elite_pandora>(locate(5, 6, 1)));
        r.In(() => pandora.toggle_ai(AI_OFF));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => pandora.singular_shot(bob));
        Assert.True(RunUntil(r, 30, () => Count<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(r) > 1), "the line of blasts went out");
        Assert.True(RunUntil(r, 30, () => Dm.num(bob.health) < before), "the blast hurt Bob");
        RunFor(r, 30);
        r.In(() => pandora.magic_box(bob));
        Assert.True(r.In(() => Count<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(r)) >= 16, "the box of blasts rose around Bob");
        RunFor(r, 30);
        r.In(() => pandora.pandora_teleport(locate(9, 9, 1)));
        Assert.True(RunUntil(r, 20, () => pandora.loc == locate(9, 9, 1)), "pandora teleported");
        Assert.True(RunUntil(r, 10, () => Dm.num(pandora.alpha) == 255 && !HAS_TRAIT(pandora, TRAIT_UNDENSE)), "pandora faded in");
        r.In(() => pandora.aoe_squares(bob));
        Assert.True(RunUntil(r, 30, () => Count<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(r) >= 9), "the squares spread out");
        // its attacks come quicker as it is hurt
        r.In(() => Waiting.NoSleep(pandora.adjust_brute_loss(Dm.num(pandora.maxHealth) * 0.8f, forced: TRUE)));
        r.In(() => pandora.Life(SSMOBS_DT()));
        Assert.Equal(1 * SECONDS, r.In(() => Dm.num(pandora.cooldown_time)));
        RunFor(r, 30);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Pandoras_hope_lifts_its_wearers_mood()
    {
        var r = new AudioRoom(11, 9, seed: 13471);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(20);
        var uniform = r.In(() =>
        {
            var u = Dm.@new<obj_item_clothing_under_color_grey>(bob.loc);
            Waiting.NoSleep(bob.equip_to_slot_if_possible(u, ITEM_SLOT_ICLOTHING, disable_warning: TRUE, bypass_equip_delay_self: TRUE));
            return u;
        });
        var hope = r.In(() => Dm.@new<obj_item_clothing_accessory_pandora_hope>(bob.loc));
        r.In(() => Waiting.NoSleep(hope.attach(uniform)));
        r.Step(2);
        Assert.True(r.In(() => bob.mob_mood?.mood_events["hope_lavaland"] != null), "hope lifted Bob's mood");
        r.In(() => hope.detach(uniform));
        r.Step(2);
        Assert.True(r.In(() => bob.mob_mood?.mood_events["hope_lavaland"] == null), "the mood went with it");
        r.AssertNoRuntimes();
    }
}
