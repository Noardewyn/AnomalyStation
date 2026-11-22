using System.Text.RegularExpressions;
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
/// Gaps package E4b (D327): the space vines in a booted toy room whose level is the station's and whose floor is a station hallway.
/// The event takes root on a free hallway floor; the vines grow through their stages and spread; an entangled mob is buckled to
/// them and gets free by hand; a knife cuts them (the last one leaves a kudzu seed pack) and kudzu seeds plant them; the cold
/// stops and the heat burns them; the explosive vines blow up in a chain, the aggressive ones thresh whoever they spread into, the
/// vine eaters eat other vines, the transparent ones let light through; a goat eats them; the flowering kudzu's bud grows and
/// bears a venus human trap for a ghost, and the trap heals on the vines and wilts off them.
/// </summary>
public class SpaceVinesE4bTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, area Room) Room(ulong seed)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        area room = r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var a = (area)Dm.@new(typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, 14, 10, 1)))
                t.change_area((area)t.loc, a);
            GLOB.the_station_areas = Dm.list(a.type);
            a.requires_power = FALSE;
            return a;
        });
        r.Step(Second);
        return (r, feed, bob, room);
    }

    private static turf T(AudioRoom r, int x, int y) => r.In(() => (turf)locate(x, y, 1));

    /// A cluster of one vine on (x, y) with the given mutations, its controller kept from processing (the tests grow it by hand).
    private static (datum_spacevine_controller Master, obj_structure_spacevine Vine) Plant(AudioRoom r, int x, int y, bool processing, params Type[] mutations)
    {
        var master = r.In(() =>
        {
            var c = Dm.@new<datum_spacevine_controller>(locate(x, y, 1), Dm.list(mutations.Cast<object>().ToArray()), null, null);
            if (!processing)
                STOP_PROCESSING(SSobj, c);
            return c;
        });
        var vine = r.In(() => Dm.each<obj_structure_spacevine>(master.vines).Single());
        return (master, vine);
    }

    private static obj_structure_spacevine Grow(AudioRoom r, datum_spacevine_controller master, obj_structure_spacevine parent, int x, int y)
        => r.In(() => (obj_structure_spacevine)Waiting.NoSleep(master.spawn_spacevine_piece(locate(x, y, 1) as turf, parent)));

    private static T Mutation<T>(AudioRoom r) where T : datum_spacevine_mutation
        => r.In(() => Dm.each<T>(GLOB.vine_mutations_list).Single());

    [Fact]
    public void TheVinesAreLiveAndTheEventIsInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(obj_structure_spacevine), typeof(datum_spacevine_controller), typeof(datum_round_event_spacevine),
            typeof(datum_spacevine_mutation_light), typeof(datum_spacevine_mutation_toxicity), typeof(datum_spacevine_mutation_explosive),
            typeof(datum_spacevine_mutation_fire_proof), typeof(datum_spacevine_mutation_cold_proof), typeof(datum_spacevine_mutation_temp_stabilisation),
            typeof(datum_spacevine_mutation_vine_eating), typeof(datum_spacevine_mutation_aggressive_spread), typeof(datum_spacevine_mutation_transparency),
            typeof(datum_spacevine_mutation_gas_eater_oxy_eater), typeof(datum_spacevine_mutation_gas_eater_nitro_eater),
            typeof(datum_spacevine_mutation_gas_eater_carbondioxide_eater), typeof(datum_spacevine_mutation_gas_eater_plasma_eater),
            typeof(datum_spacevine_mutation_thorns), typeof(datum_spacevine_mutation_hardened), typeof(datum_spacevine_mutation_timid),
            typeof(datum_spacevine_mutation_flowering), typeof(datum_spacevine_mutation_slippery), typeof(datum_spacevine_mutation_conductive),
            typeof(datum_spacevine_mutation_radioactive), typeof(datum_component_storm_hating), typeof(obj_structure_alien_resin_flower_bud),
            typeof(obj_effect_ebeam_vine), typeof(obj_effect_countdown_flower_bud), typeof(mob_living_basic_venus_human_trap),
            typeof(datum_action_cooldown_mob_cooldown_projectile_attack_vine_tangle), typeof(obj_effect_mob_spawn_ghost_role_venus_human_trap),
            typeof(datum_antagonist_venus_human_trap), typeof(datum_objective_venus_human_trap), typeof(datum_element_lifesteal),
            typeof(datum_element_on_hit_effect),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _, _) = Room(4401);
        Assert.Contains("Space Vines", r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList()));
        // every mutation but the abstract gas eater, weighted by how far its severity is below the ideal maximum
        Assert.Equal(20, r.In(() => GLOB.vine_mutations_list.len));
        var explosive = Mutation<datum_spacevine_mutation_explosive>(r);
        Assert.Equal(IDEAL_MAX_SEVERITY - SEVERITY_MAJOR, r.In(() => Dm.num(GLOB.vine_mutations_list[explosive])));
    }

    [Fact]
    public void TheEventTakesRootOnAFreeHallwayFloor()
    {
        var (r, _, _, room) = Room(4402);
        var control = r.In(() => Dm.each<datum_round_event_control_spacevine>(SSevents.control).Single());
        var ev = r.In(() => Dm.@new<datum_round_event_spacevine>(FALSE, control));
        r.In(() => { Waiting.NoSleep(ev.start()); });
        var vine = r.In(() => Dm.each<obj_structure_spacevine>(Dm.world).Single(v => !QDELETED(v)));
        Assert.Same(room, r.In(() => get_area(vine)));
        var master = r.In(() => vine.master);
        Assert.NotNull(master);
        // one random starting mutation; a potency of 50 to 100 and a production of 1 to 4, with the event's bonus
        Assert.Equal(1, r.In(() => vine.mutations.len));
        float potency = r.In(() => Dm.num(ev.potency)), production = r.In(() => Dm.num(ev.production));
        Assert.InRange(potency, 50, 100);
        Assert.InRange(production, 1, 4);
        Assert.Equal(potency * MUTATIVENESS_SCALE_FACTOR, r.In(() => master.mutativeness), 3);
        Assert.Equal(Dm.round(potency * MAX_SEVERITY_LINEAR_COEFF + MAX_SEVERITY_CONSTANT_TERM) + MAX_SEVERITY_EVENT_BONUS, r.In(() => master.max_mutation_severity));
        Assert.Equal(SPREAD_CAP_LINEAR_COEFF * (MAX_POSSIBLE_PRODUCTIVITY_VALUE + 1 - production) + SPREAD_CAP_CONSTANT_TERM, r.In(() => master.spread_cap));
        Assert.Equal(3f, r.In(() => master.minimum_spread_rate));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVinesGrowThroughTheirStagesAndSpread()
    {
        var (r, _, _, _) = Room(4403);
        var (master, first) = Plant(r, 8, 6, processing: true);
        Assert.Equal("Light1", r.In(() => first.icon_state));
        Assert.Equal(PASS_LIGHT, r.In(() => (int)Dm.num(first.opacity)));
        for (int sec = 0; sec < 60 && r.In(() => Dm.each<obj_structure_spacevine>(master.vines).All(v => v.growth_stage < 2)); sec++)
            r.Step(Second);
        var vines = r.In(() => Dm.each<obj_structure_spacevine>(master.vines).ToList());
        Assert.True(vines.Count > 3, "the vines spread: " + vines.Count);
        // the medium stage blocks the view, the heavy one too
        var grown = vines.Where(v => r.In(() => v.growth_stage) >= 1).ToList();
        Assert.NotEmpty(grown);
        Assert.All(grown, v => Assert.Matches("^(Med|Hvy)[123]$", r.In(() => v.icon_state)));
        Assert.All(grown, v => Assert.Equal(BLOCK_LIGHT, r.In(() => (int)Dm.num(v.opacity))));
        Assert.Contains(vines, v => r.In(() => v.growth_stage) == 2 && Regex.IsMatch(r.In(() => v.icon_state), "^Hvy[123]$"));
        // one vine a tile: no two on one floor
        Assert.Equal(vines.Count, vines.Select(v => r.In(() => v.loc)).Distinct().Count());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGrownVineEntanglesTheMobOnItAndAHandFreesHim()
    {
        var (r, feed, bob, _) = Room(4404);
        var (_, vine) = Plant(r, 3, 4, processing: false, typeof(datum_spacevine_mutation_light));
        r.In(() => { Waiting.NoSleep(vine.entangle(bob)); });
        Assert.Same(vine, r.In(() => bob.buckled));
        Assert.True(feed.Any("around you!"), "the vines wind around him");
        Assert.Equal(SPACEVINE_MOB_LAYER, r.In(() => bob.layer), 3); // each mutation lays the buckled mob over the vines
        // his own hand on the vine unbuckles him; the mutations' layer is undone with it
        r.In(() => { Waiting.NoSleep(vine.attack_hand(bob)); });
        Assert.Null(r.In(() => bob.buckled));
        Assert.Equal(MOB_LAYER, r.In(() => bob.layer), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AKnifeCutsTheVinesAndTheLastOneLeavesKudzuSeeds()
    {
        var (r, _, bob, _) = Room(4405);
        var (master, vine) = Plant(r, 4, 4, processing: false, typeof(datum_spacevine_mutation_light));
        var knife = r.In(() =>
        {
            var k = Dm.@new<obj_item_knife_kitchen>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(k));
            return k;
        });
        float force = r.In(() => Dm.num(knife.force)), demolition = r.In(() => Dm.num(knife.demolition_mod));
        // a sharp item cuts at four times its force, and the vines invert its demolition modifier (TRAIT_INVERTED_DEMOLITION): the
        // kitchen knife's 10 force and 0.75 make 53 points, more than a vine's 50
        Assert.True(4 * force / demolition >= 50, "one stroke is enough");
        r.In(() => { Waiting.NoSleep(knife.melee_attack_chain(bob, vine)); });
        Assert.True(r.In(() => QDELETED(vine)), "the vine is cut");
        Assert.True(r.In(() => QDELETED(master)), "the cluster's controller goes with its last vine");
        var seeds = r.In(() => Dm.locate<obj_item_seeds_kudzu>(locate(4, 4, 1)));
        Assert.NotNull(seeds);
        Assert.Contains(Mutation<datum_spacevine_mutation_light>(r), r.In(() => Dm.each<datum_spacevine_mutation>(seeds.mutations).ToList()));
        // the controller's stats back: no potency given (mutativeness 1) is a potency of 5, its default spread multiplier a production of 1
        Assert.Equal(5f, r.In(() => Dm.num(seeds.potency)));
        Assert.Equal(1f, r.In(() => Dm.num(seeds.production)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void KudzuSeedsPlantVinesUnderThePlanter()
    {
        var (r, feed, bob, _) = Room(4406);
        var seeds = r.In(() =>
        {
            var s = Dm.@new<obj_item_seeds_kudzu>(bob.loc);
            Dm.append(ref s.mutations, Dm.each<datum_spacevine_mutation_thorns>(GLOB.vine_mutations_list).Single());
            s.set_potency(100);
            s.set_production(1);
            return s;
        });
        r.In(() => { Waiting.NoSleep(seeds.plant(bob)); });
        Assert.True(feed.Any("You plant "));
        Assert.True(r.In(() => QDELETED(seeds)));
        var vine = r.In(() => Dm.locate<obj_structure_spacevine>(bob.loc));
        Assert.NotNull(vine);
        Assert.IsType<datum_spacevine_mutation_thorns>(r.In(() => vine.mutations[1]));
        // potency 100: 20 mutativeness and 25 severity; production 1: a cap of 60 vines spreading at 10 % a second
        Assert.Equal(20f, r.In(() => vine.master.mutativeness), 3);
        Assert.Equal(25f, r.In(() => vine.master.max_mutation_severity));
        Assert.Equal(60f, r.In(() => vine.master.spread_cap));
        Assert.Equal(5f, r.In(() => vine.master.spread_multiplier));
        // a second pack on the same floor is refused
        var more = Place<obj_item_seeds_kudzu>(r, 3, 4);
        r.In(() => { Waiting.NoSleep(more.plant(bob)); });
        Assert.True(feed.Any("There is too much kudzu here to plant"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheColdStopsTheVinesAndTheHeatBurnsThem()
    {
        var (r, _, _, _) = Room(4407);
        var (_, vine) = Plant(r, 6, 6, processing: false);
        Assert.Equal(TRUE, r.In(() => Dm.truthy(vine.should_atmos_process(null, 50)) ? TRUE : FALSE));
        Assert.Equal(FALSE, r.In(() => Dm.truthy(vine.should_atmos_process(null, T20C)) ? TRUE : FALSE));
        r.In(() => { Waiting.NoSleep(vine.atmos_expose(null, 50)); });
        Assert.Equal(FALSE, r.In(() => vine.can_spread));
        r.In(() => { Waiting.NoSleep(vine.atmos_expose(null, T20C)); });
        Assert.Equal(TRUE, r.In(() => vine.can_spread));
        r.In(() => { Waiting.NoSleep(vine.atmos_expose(null, FIRE_MINIMUM_TEMPERATURE_TO_SPREAD + 10)); });
        Assert.True(r.In(() => QDELETED(vine)), "the fire burns it");
        // a fire proof vine, grown, does not burn
        var (_, proof) = Plant(r, 8, 6, processing: false, typeof(datum_spacevine_mutation_fire_proof));
        r.In(() => { Waiting.NoSleep(proof.grow()); });
        r.In(() => { Waiting.NoSleep(proof.atmos_expose(null, FIRE_MINIMUM_TEMPERATURE_TO_SPREAD + 10)); });
        Assert.False(r.In(() => QDELETED(proof)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ExplosiveVinesBlowUpInAChain()
    {
        var (r, _, _, _) = Room(4408);
        var (master, first) = Plant(r, 6, 6, processing: false, typeof(datum_spacevine_mutation_explosive));
        var second = Grow(r, master, first, 7, 6);
        var third = Grow(r, master, second, 8, 6);
        var fourth = Grow(r, master, third, 9, 6);
        Assert.Contains(Mutation<datum_spacevine_mutation_explosive>(r), r.In(() => Dm.each<datum_spacevine_mutation>(fourth.mutations).ToList()));
        r.In(() => qdel(first));
        for (int sec = 0; sec < 10 && !r.In(() => QDELETED(fourth)); sec++)
            r.Step(Second);
        Assert.True(r.In(() => QDELETED(second)), "the blast takes the next vine");
        // three tiles away, past the first blast's reach: blown up by the blasts of the vines between
        Assert.True(r.In(() => QDELETED(fourth)), "the chain reaches the last vine");
    }

    [Fact]
    public void AggressiveVinesThreshWhoeverTheySpreadInto()
    {
        var (r, feed, bob, _) = Room(4409);
        var (_, vine) = Plant(r, 4, 4, processing: false, typeof(datum_spacevine_mutation_aggressive_spread));
        var aggressive = Mutation<datum_spacevine_mutation_aggressive_spread>(r);
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => { Waiting.NoSleep(aggressive.on_spread(vine, bob.loc as turf)); });
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > before, "he is hurt");
        Assert.True(feed.Any("You are lacerated by an outburst of vines!") || feed.Any("You are smashed by a large vine!") || feed.Any("You are nailed by a sharp thorn!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void VineEatersEatTheOtherClustersVinesAndAGoatEatsVines()
    {
        var (r, _, _, _) = Room(4410);
        var (eaters, eater) = Plant(r, 6, 6, processing: false, typeof(datum_spacevine_mutation_vine_eating));
        var (_, prey) = Plant(r, 7, 6, processing: false);
        var eating = Mutation<datum_spacevine_mutation_vine_eating>(r);
        r.In(() => { Waiting.NoSleep(eating.on_spread(eater, prey.loc as turf)); });
        Assert.True(r.In(() => QDELETED(prey)), "the vine eater eats the other vine");
        Assert.False(r.In(() => QDELETED(eater)));
        // a goat eats a vine
        var goat = Place<mob_living_basic_goat>(r, 9, 6);
        r.In(() => { Waiting.NoSleep(goat.eat_plant(Dm.list(eater))); });
        Assert.True(r.In(() => QDELETED(eater)), "the goat ate it");
        Assert.True(r.In(() => QDELETED(eaters)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TransparentVinesLetTheLightThroughAndTimidOnesCoverTheFloor()
    {
        var (r, _, bob, _) = Room(4411);
        var (_, clear) = Plant(r, 6, 6, processing: false, typeof(datum_spacevine_mutation_transparency));
        Assert.Equal(125f, r.In(() => clear.alpha));
        r.In(() => { Waiting.NoSleep(clear.grow()); });
        Assert.Equal(PASS_LIGHT, r.In(() => (int)Dm.num(clear.opacity)));
        Assert.False(r.In(() => HAS_TRAIT(clear.loc, TRAIT_TURF_SUN_BLOCKED)), "no sunlight blocked");
        var (_, timid) = Plant(r, 3, 4, processing: false, typeof(datum_spacevine_mutation_timid));
        Assert.Equal(ABOVE_OPEN_TURF_LAYER, r.In(() => timid.layer));
        r.In(() => { Waiting.NoSleep(timid.grow()); Waiting.NoSleep(timid.grow()); });
        r.In(() => { Waiting.NoSleep(timid.entangle(bob)); });
        Assert.Null(r.In(() => bob.buckled));
        // a plain grown vine blocks the sun on its floor
        var (_, plain) = Plant(r, 10, 6, processing: false);
        Assert.True(r.In(() => HAS_TRAIT(plain.loc, TRAIT_TURF_SUN_BLOCKED)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFlowerBudGrowsAndBearsAVenusHumanTrap()
    {
        var (r, feed, _, _) = Room(4412);
        var (_, vine) = Plant(r, 8, 6, processing: false, typeof(datum_spacevine_mutation_flowering), typeof(datum_spacevine_mutation_hardened));
        var bud = r.In(() =>
        {
            var b = Dm.@new<obj_structure_alien_resin_flower_bud>(vine.loc);
            b.mutations = vine.mutations;
            return b;
        });
        var spawner = r.In(() => bud.spawner);
        Assert.NotNull(spawner);
        Assert.Same(bud, r.In(() => spawner.flower_bud));
        Assert.Equal(4, r.In(() => bud.vines.len)); // a thorny vine to each diagonal
        Assert.Equal(FALSE, r.In(() => Dm.truthy(spawner.allow_spawn(null, TRUE)) ? TRUE : FALSE));
        r.Step(31 * Second);
        Assert.Equal("bud1", r.In(() => bud.icon_state));
        r.Step(90 * Second);
        Assert.Equal("bud3", r.In(() => bud.icon_state));
        Assert.True(feed.Any("The plant has borne fruit!"));
        Assert.Equal(TRUE, r.In(() => spawner.ready));
        Assert.Equal(TRUE, r.In(() => Dm.truthy(spawner.allow_spawn(null, TRUE)) ? TRUE : FALSE));
        // the trap the ghost becomes takes the bud's mutations: hardened doubles its health
        var trap = r.In(() =>
        {
            var t = Dm.@new<mob_living_basic_venus_human_trap>(vine.loc);
            spawner.equip(t);
            return t;
        });
        Assert.Equal(100f, r.In(() => Dm.num(trap.maxHealth)));
        Assert.NotNull(r.In(() => Dm.locate<datum_action_cooldown_mob_cooldown_projectile_attack_vine_tangle>(trap.actions)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVenusHumanTrapHealsOnTheVinesAndWiltsOffThem()
    {
        var (r, feed, _, _) = Room(4413);
        var (_, vine) = Plant(r, 8, 6, processing: false);
        var trap = r.In(() => Dm.@new<mob_living_basic_venus_human_trap>(locate(12, 8, 1)));
        r.In(() => { trap.Life(2); });
        Assert.Equal(12.5f, r.In(() => Dm.num(trap.get_brute_loss())));
        Assert.True(r.In(() => Dm.truthy(trap.alert_shown)), "do not leave vines!");
        r.In(() => { Waiting.NoSleep(trap.forceMove(vine.loc)); trap.Life(2); });
        Assert.Equal(7.5f, r.In(() => Dm.num(trap.get_brute_loss())));
        Assert.Equal(FALSE, r.In(() => trap.alert_shown));
        // the trap is of the vines: they neither tangle it nor let it be blocked
        r.In(() => { Waiting.NoSleep(vine.entangle(trap)); });
        Assert.Null(r.In(() => trap.buckled));
        Assert.Equal(TRUE, r.In(() => Dm.truthy(isvineimmune(trap)) ? TRUE : FALSE));
        r.AssertNoRuntimes();
    }
}
