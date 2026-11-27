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
/// The ash walkers' nest: it founds its tribe with the objective to protect it and lays eggs from its store of meat; it tears apart
/// the bodies dragged beside it, feeding itself and healing; a ghost who takes an egg hatches as an ash walker of the tribe, once
/// only; a dead ash walker whose soul is near is remade by the nest; the broken nest drops an anomaly core and collapses into a chasm;
/// and the tribe's round-end report.
/// </summary>
public class AshWalkerTests
{
    private static void Jumpsuit(mob_living_carbon_human wearer)
    {
        if (wearer.w_uniform == null)
            Waiting.NoSleep(wearer.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(wearer.loc), ITEM_SLOT_ICLOTHING));
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

    private static List<obj_effect_mob_spawn_ghost_role_human_ash_walker> Eggs(AudioRoom r) =>
        r.In(() => Dm.each<obj_effect_mob_spawn_ghost_role_human_ash_walker>().Where(e => !QDELETED(e)).ToList());

    /// A nest whose first eggs (from its starting store of six) are laid.
    private static obj_structure_lavaland_ash_walker Nest(AudioRoom r, int x, int y)
    {
        var nest = r.In(() => Dm.@new<obj_structure_lavaland_ash_walker>(locate(x, y, 1)));
        Assert.True(RunUntil(r, 10 * Second, () => nest.meat_counter < 2), "the nest laid its first eggs");
        return nest;
    }

    /// A player who joins on (x, y) and ghosts: their client is on the observer.
    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    private static void Answer(AudioRoom r, mob who, string choice)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains("AlertModal"), "no alert open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == "AlertModal").window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/choose", Dm.list(Dm.kv("choice", choice))));
        r.Step(4);
    }

    /// A ghost clicks the egg's shell and says yes: the ash walker it hatched into.
    private static mob_living_carbon_human Hatch(AudioRoom r, obj_effect_mob_spawn_ghost_role_human_ash_walker egg, mob_dead_observer ghost)
    {
        client player = r.In(() => ghost.client);
        obj_structure_ash_walker_eggshell shell = r.In(() => egg.eggshell);
        r.In(() => Waiting.Detach(shell.attack_ghost(ghost)));
        Answer(r, ghost, "Yes");
        r.Step(Second);
        return Assert.IsType<mob_living_carbon_human>(r.In(() => player.mob));
    }

    [Fact]
    public void The_nest_founds_its_tribe_and_lays_its_first_eggs_from_its_store_of_meat()
    {
        var r = new AudioRoom(15, 15, seed: 13621);
        var nest = r.In(() => Dm.@new<obj_structure_lavaland_ash_walker>(locate(7, 7, 1)));
        r.In(() =>
        {
            Assert.NotNull(nest.ashies);
            var objective = Dm.each<datum_objective_protect_object>(nest.ashies.objectives).Single();
            Assert.Same(nest, objective.protect_target);
            Assert.Same(nest.ashies, objective.team);
            Assert.Equal(6f, nest.meat_counter);
        });
        // two pieces of meat to an egg: the six it starts with lay three, one a second
        Assert.True(RunUntil(r, 10 * Second, () => nest.ashies.eggs_created == 3), "the nest laid three eggs");
        var eggs = Eggs(r);
        Assert.Equal(3, eggs.Count);
        r.In(() =>
        {
            Assert.Equal(0f, nest.meat_counter);
            foreach (var egg in eggs)
            {
                Assert.Same(nest.ashies, egg.team);
                Assert.NotNull(egg.eggshell);
                Assert.Same(egg.eggshell, egg.loc);
                Assert.Same(egg, egg.eggshell.egg);
                Assert.True(get_dist(egg.eggshell, nest) == 1, "an egg tumbles beside the nest");
            }
        });
        r.Step(5 * Second);
        Assert.Equal(3, Eggs(r).Count);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_body_beside_the_nest_is_torn_apart_to_feed_it_and_two_bodies_make_an_egg()
    {
        var r = new AudioRoom(15, 15, seed: 13622);
        Feed feed = Listen(r);
        var nest = Nest(r, 7, 7);
        r.Step(2 * Second);
        int eggsBefore = Eggs(r).Count;
        var watcher = r.Join(7, 11, "Watcher"); // a player sees the nest at work
        r.In(() => nest.atom_integrity = 100);

        var body = r.Dummy(8, 7);
        r.In(() => Waiting.NoSleep(body.death()));
        Assert.True(RunUntil(r, 3 * Second, () => QDELETED(body)), "the body was torn apart");
        Assert.True(feed.Any("Serrated tendrils eagerly pull"), "the nest's message");
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("demon_consume", StringComparison.Ordinal));
        r.In(() =>
        {
            Assert.Equal(1f, nest.meat_counter);
            Assert.Equal(1f, nest.ashies.sacrifices_made);
            Assert.Equal(110f, Dm.num(nest.atom_integrity)); // five percent of its 200
            Assert.NotNull(watcher.mob_mood.mood_events["oogabooga"]);
            Assert.IsType<datum_mood_event_sacrifice_bad>(watcher.mob_mood.mood_events["oogabooga"]);
        });

        // a second body: two pieces of meat, an egg
        var second = r.Dummy(6, 6);
        r.In(() => Waiting.NoSleep(second.death()));
        Assert.True(RunUntil(r, 3 * Second, () => QDELETED(second)), "the second body was torn apart");
        Assert.True(RunUntil(r, 2 * Second, () => Eggs(r).Count == eggsBefore + 1), "an egg came of the two bodies");
        r.In(() => Assert.Equal(4f, nest.ashies.eggs_created));

        // a living body further away is left alone
        var bystander = r.Dummy(10, 7);
        r.Step(3 * Second);
        Assert.False(r.In(() => QDELETED(bystander)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_ghost_who_takes_an_egg_hatches_as_an_ash_walker_of_the_tribe_and_only_once()
    {
        var r = new AudioRoom(15, 15, seed: 13623);
        r.In(() => SSticker.current_state = GAME_STATE_PLAYING);
        var nest = Nest(r, 7, 7);
        var eggs = Eggs(r);
        Assert.True(eggs.Count >= 2);
        mob_dead_observer ghost = Ghost(r, 3, 3, "Ashley");
        var egg = eggs[0];
        var shell = r.In(() => egg.eggshell);
        var walker = Hatch(r, egg, ghost);
        r.In(() =>
        {
            Assert.IsType<datum_species_lizard_ashwalker>(walker.dna.species);
            var antag = walker.mind.has_antag_datum(typeof(datum_antagonist_ashwalker)) as datum_antagonist_ashwalker;
            Assert.NotNull(antag);
            Assert.Same(nest.ashies, antag.get_team());
            Assert.Contains(Dm.each<datum_objective>(antag.objectives), o => o is datum_objective_protect_object);
            Assert.True(Dm.@in(walker.ckey, nest.ashies.players_spawned), "the tribe remembers who hatched");
            Assert.False(Dm.truthy(walker.has_faction(FACTION_NEUTRAL)), "ash walkers are not neutral");
            Assert.True(Dm.truthy(walker.has_language(typeof(datum_language_draconic))));
            Assert.False(Dm.truthy(walker.has_language(typeof(datum_language_common))), "ash walkers forget common");
            Assert.True(Dm.@in(GLOB.crafting_recipes_by_typepath[typeof(datum_crafting_recipe_skeleton_key)], walker.mind.learned_crafting_recipes), "the skeleton key's recipe");
            Assert.True(QDELETED(egg), "the egg hatched");
            Assert.True(QDELETED(shell), "its shell went with it");
            Assert.True(get_dist(walker, nest) <= 1, "the walker hatched beside the nest");
            Assert.Contains(walker.mind, Dm.each<datum_mind>(nest.ashies.members));
        });

        // the same player cannot take a second egg
        r.In(() =>
        {
            var other = Eggs(r).First();
            Assert.False(Dm.truthy(other.allow_spawn(walker, TRUE)), "one egg to a player");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_ash_walker_whose_soul_is_in_its_body_is_remade_by_the_nest()
    {
        var r = new AudioRoom(15, 15, seed: 13624);
        r.In(() => SSticker.current_state = GAME_STATE_PLAYING);
        Feed feed = Listen(r);
        var nest = Nest(r, 7, 7);
        var walker = Hatch(r, Eggs(r)[0], Ghost(r, 3, 3, "Ashley"));
        client player = r.In(() => walker.client);
        string realName = r.In(() => Dm.str(walker.real_name));
        float meat = r.In(() => nest.meat_counter);
        r.Join(7, 11, "Watcher"); // a player sees the nest at work
        r.In(() =>
        {
            Waiting.NoSleep(walker.forceMove(locate(8, 8, 1)));
            Waiting.NoSleep(walker.death());
        });
        Assert.True(RunUntil(r, 3 * Second, () => walker.loc == nest), "the nest took the body in");
        Assert.True(feed.Any("absorbing the body and creating it anew"), "the nest's message");
        Assert.Equal(meat, r.In(() => nest.meat_counter));
        Assert.True(RunUntil(r, 22 * Second, () => player.mob != walker && player.mob is mob_living_carbon_human), "the walker was remade");
        var remade = (mob_living_carbon_human)r.In(() => player.mob);
        r.In(() =>
        {
            Assert.True(QDELETED(walker), "the old body went");
            Assert.IsType<datum_species_lizard_ashwalker>(remade.dna.species);
            Assert.Equal(realName, Dm.str(remade.real_name));
            Assert.Equal(0f, Dm.num(remade.stat)); // conscious
            Assert.NotNull(remade.mind.has_antag_datum(typeof(datum_antagonist_ashwalker)));
            Assert.True(get_dist(remade, nest) <= 1);
        });
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("exit_blood", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_broken_nest_drops_an_anomaly_core_and_collapses_into_a_chasm_after_one_loot_crate_a_head()
    {
        var r = new AudioRoom(15, 15, seed: 13625);
        Feed feed = Listen(r);
        var nest = r.In(() => Dm.@new<obj_structure_lavaland_ash_walker>(locate(7, 7, 1)));
        mob_living_carbon_human bob = r.Join(7, 4, "Bob");
        r.In(() => Jumpsuit(bob));
        r.Step(2);
        r.In(() => Waiting.NoSleep(nest.take_damage(1000, BRUTE, MELEE, FALSE)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(nest)), "the nest broke");
        Assert.True(r.In(() => Dm.each<obj_item_assembly_signaler_anomaly>().Any(c => get_dist(c, locate(7, 7, 1)) <= 1)), "an anomaly core fell beside it");
        var collapse = r.In(() => Dm.each<obj_effect_collapse>(locate(7, 7, 1)).Single());
        Assert.True(feed.Any("The tendril writhes in fury"));
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("tendril_destroyed", StringComparison.Ordinal));

        r.In(() => Waiting.NoSleep(bob.forceMove(locate(7, 6, 1))));
        r.In(() => Waiting.NoSleep(collapse.attack_hand(bob)));
        Assert.Equal(1, r.In(() => Dm.each<obj_structure_closet_crate_necropolis_tendril>(locate(7, 7, 1)).Count()));
        Assert.True(r.In(() => Dm.truthy(collapse.has_collected(bob))));
        r.In(() => Waiting.NoSleep(collapse.attack_hand(bob)));
        Assert.Equal(1, r.In(() => Dm.each<obj_structure_closet_crate_necropolis_tendril>(locate(7, 7, 1)).Count()));
        Assert.True(feed.Any("You've already gotten some loot"));

        Assert.True(RunUntil(r, 6 * Second, () => QDELETED(collapse)), "the tendril fell inward");
        r.In(() =>
        {
            foreach (turf t in Dm.each<turf>(block(5, 5, 1, 9, 9, 1)))
                Assert.True(t is turf_open_chasm_lavaland, "the ground within two tiles turned to chasm: " + t.x + "," + t.y);
            Assert.False(locate(7, 10, 1) is turf_open_chasm, "only within two tiles");
        });
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("explosionfar", StringComparison.Ordinal));
    }

    [Fact]
    public void The_tribes_report_counts_its_sacrifices_and_eggs_and_says_whether_the_nest_stands()
    {
        var r = new AudioRoom(15, 15, seed: 13626);
        r.In(() => SSticker.current_state = GAME_STATE_PLAYING);
        var nest = Nest(r, 7, 7);
        string empty = r.In(() =>
        {
            var lonely = Dm.@new<datum_team_ashwalkers>();
            return Dm.str(lonely.roundend_report());
        });
        Assert.Contains("But none of its eggs hatched!", empty);

        Hatch(r, Eggs(r)[0], Ghost(r, 3, 3, "Ashley"));
        string report = r.In(() => Dm.str(nest.ashies.roundend_report()));
        Assert.Contains("The nest stands! Glory to the Necropolis!", report);
        Assert.Contains("perform <b>0</b> sacrifices", report);
        Assert.Contains("produced <b>3</b> Ash Walker eggs", report);
    }
}
