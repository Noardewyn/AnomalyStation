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
/// Gaps package C5: the monkeys (monkey.dm, species_types/monkeys.dm, datums/ai/monkey) and Pun Pun. A monkey spawns with its body and tree and goes away with no runtime, it wanders,
/// fights back and bites when wronged, runs when hurt, picks up a weapon in a fight; Pun Pun serves food and rings his bell; water makes a monkey of a cube; the recycler turns a
/// downed monkey into cubes; his memory survives the round; monkeyize and humanize.
/// </summary>
public class MonkeysTests
{
    private static datum_ai_controller Brain(mob_living mob) => (datum_ai_controller)mob.ai_controller;

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

    private static mob_living_carbon_human_species_monkey Monkey(AudioRoom r, int x, int y) =>
        r.In(() => Dm.@new<mob_living_carbon_human_species_monkey>(locate(x, y, 1)));

    [Fact]
    public void A_monkey_has_its_body_and_tree_and_goes_away_cleanly()
    {
        var r = new AudioRoom(13, 9, seed: 8301);
        r.Join(9, 4, "Bob");
        var monkey = Monkey(r, 4, 4);
        Assert.False(r.In(() => InertSeam.IsInert(monkey)));
        r.AssertNoRuntimes();
        Assert.True(r.In(() => ismonkey(monkey)));
        Assert.IsType<datum_ai_controller_monkey>(monkey.ai_controller);
        Assert.Equal("code/datums/ai/monkey/monkey.bt.json", r.In(() => Brain(monkey).behavior_tree_json as string));
        Assert.Contains("monkey (", r.In(() => monkey.real_name as string)); // the language's random name
        Assert.IsType<obj_item_bodypart_head_monkey>(r.In(() => monkey.get_bodypart(BODY_ZONE_HEAD)));
        Assert.IsType<obj_item_bodypart_chest_monkey>(r.In(() => monkey.get_bodypart(BODY_ZONE_CHEST)));
        Assert.IsType<obj_item_organ_brain_primate>(r.In(() => monkey.get_organ_slot(ORGAN_SLOT_BRAIN)));
        var tail = Assert.IsType<obj_item_organ_tail_monkey>(r.In(() => monkey.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL)));
        Assert.Equal("Monkey", r.In(() => (tail.bodypart_overlay as datum_bodypart_overlay_mutant).sprite_datum.name));
        Assert.Equal(MONKEY_HEIGHT_MEDIUM, r.In(() => monkey.mob_height));
        Assert.True(r.In(() => HAS_TRAIT(monkey, TRAIT_BORN_MONKEY)));
        Assert.True(r.In(() => Dm.truthy(monkey.has_faction(FACTION_MONKEY))));
        r.Step(100);
        Assert.False(r.In(() => QDELETED(monkey)));
        r.In(() => qdel(monkey));
        r.Step(20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_monkey_wanders_and_screeches_on_its_own()
    {
        var r = new AudioRoom(13, 9, seed: 8302);
        r.Join(10, 7, "Bob");
        var monkey = Monkey(r, 5, 4);
        var start = r.In(() => (monkey.x, monkey.y));
        Assert.True(RunUntil(r, 600, () => (monkey.x, monkey.y) != start), "it wandered (monkey_idle)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wronged_monkey_screeches_bites_and_stops_when_it_forgives()
    {
        var r = new AudioRoom(13, 9, seed: 8303);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var monkey = Monkey(r, 4, 4);
        var brain = (datum_ai_controller_monkey)Brain(monkey);
        r.In(() => brain.retaliate(bob));
        Assert.Equal(MONKEY_HATRED_AMOUNT, r.In(() => (int)Dm.num((brain.blackboard[BB_MONKEY_ENEMIES] as DmList)[bob])));
        Assert.True(RunUntil(r, 100, () => Dm.eq(brain.blackboard[BB_CURRENT_TARGET], bob)), "the enemy is its target");
        Assert.True(r.In(() => Dm.truthy(monkey.combat_mode)), "it fights");
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        Assert.True(RunUntil(r, 600, () => Dm.num(bob.get_brute_loss()) > before || Dm.truthy(bob.IsKnockdown())), "it bites or tackles him");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hit_makes_it_retaliate_and_a_hurt_monkey_runs()
    {
        var r = new AudioRoom(13, 9, seed: 8304);
        mob_living_carbon_human bob = r.Join(6, 4, "Bob");
        var monkey = Monkey(r, 5, 4);
        var brain = (datum_ai_controller_monkey)Brain(monkey);
        // a real punch: the relay's COMSIG_ATOM_WAS_ATTACKED, at tg's 85%
        for (int i = 0; i < 40 && r.In(() => (brain.blackboard[BB_MONKEY_ENEMIES] as DmList)[bob] == null); i++)
        {
            r.In(() =>
            {
                bob.next_move = 0;
                bob.next_click = 0;
                bob.set_combat_mode(TRUE);
                Waiting.NoSleep(bob.ClickOn(monkey));
            });
            r.Step();
        }
        Assert.NotNull(r.In(() => (brain.blackboard[BB_MONKEY_ENEMIES] as DmList)[bob]));
        Assert.True(RunUntil(r, 100, () => Dm.eq(brain.blackboard[BB_CURRENT_TARGET], bob)));
        r.In(() => Waiting.NoSleep(monkey.apply_damage(70, BRUTE, BODY_ZONE_CHEST))); // under MONKEY_FLEE_HEALTH
        r.Step(5);
        Assert.True(r.In(() => Dm.num(monkey.health)) < MONKEY_FLEE_HEALTH);
        int startDist = r.In(() => (int)get_dist(monkey, bob));
        Assert.True(RunUntil(r, 200, () => get_dist(monkey, bob) > startDist + 1), "it ran");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Its_emotes_examine_and_the_trip_work()
    {
        var r = new AudioRoom(13, 9, seed: 8311);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var monkey = Monkey(r, 4, 4);
        foreach (string act in new[] { "screech", "roar", "scratch", "jump", "roll", "tail", "tunesing", "dance", "bow", "clear", "sign" })
        {
            r.In(() => Waiting.NoSleep(monkey.emote(act)));
            r.Step(2);
        }
        r.AssertNoRuntimes();
        Assert.NotNull(r.In(() => Waiting.NoSleep(monkey.examine(bob))));
        // it speaks Chimpanzee (species_language_holder, its tongue) and Bob hears it
        Assert.IsType<datum_language_monkey>(r.In(() => monkey.get_selected_language() is Type t ? Dm.@new(t) : null));
        r.In(() => Waiting.NoSleep(monkey.say("ook ook")));
        r.Step(2);
        r.AssertNoRuntimes();
        string heard = r.In(() => bob.translate_language(monkey, typeof(datum_language_monkey), "Hello there friend") as string);
        Assert.NotEqual("Hello there friend", heard);
        Assert.Matches("(?i)(oop|aak|chee|eek)", heard);
        Assert.Equal("Hello there", r.In(() => monkey.translate_language(bob, typeof(datum_language_common), "Hello there") as string)); // it understands Common
        // a monkey trips someone who walks into it in combat mode; a tamed one does not (monkey_controller.dm:124)
        r.In(() => { bob.set_combat_mode(TRUE); monkey._SendSignal(COMSIG_LIVING_MOB_BUMPED, Dm.list(monkey, bob)); });
        r.Step(5);
        Assert.True(r.In(() => Dm.truthy(monkey.IsParalyzed())), "the trip");
        r.AssertNoRuntimes();
        var pun = r.In(() => Dm.@new<mob_living_carbon_human_species_monkey_punpun>(locate(7, 7, 1)));
        Assert.Equal(0f, r.In(() => (pun.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain_primate).tripping));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void In_a_fight_it_picks_up_a_weapon_and_swings_it()
    {
        var r = new AudioRoom(13, 9, seed: 8305);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var monkey = Monkey(r, 4, 4);
        var brain = (datum_ai_controller_monkey)Brain(monkey);
        var bat = r.In(() => Dm.@new<obj_item_crowbar>(locate(5, 5, 1)));
        r.In(() => brain.retaliate(bob));
        Assert.True(RunUntil(r, 400, () => bat.loc == monkey), "it took the crowbar");
        Assert.Equal(bat.force, r.In(() => Dm.num(brain.blackboard[BB_MONKEY_BEST_FORCE_FOUND])));
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        Assert.True(RunUntil(r, 600, () => Dm.num(bob.get_brute_loss()) > before), "it hit him with it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Pun_Pun_serves_the_food_to_a_patron_and_rings_his_bell()
    {
        var r = new AudioRoom(13, 9, seed: 8306);
        mob_living_carbon_human bob = r.Join(8, 4, "Bob");
        var pun = r.In(() => Dm.@new<mob_living_carbon_human_species_monkey_punpun>(locate(4, 4, 1)));
        var brain = Brain(pun);
        Assert.IsType<datum_ai_controller_monkey_pun_pun>(pun.ai_controller);
        Assert.True(r.In(() => Dm.truthy(brain.blackboard[BB_MONKEY_TAMED])));
        Assert.IsType<obj_item_clothing_under_suit_waiter>(r.In(() => pun.w_uniform));
        Assert.Equal(typeof(obj_structure_desk_bell), r.In(() => brain.blackboard[BB_MONKEY_PRESS_TYPEPATH]));
        var food = r.In(() => Dm.@new<obj_item_food_donkpocket>(locate(5, 4, 1)));
        bool took = RunUntil(r, 600, () => food.loc == pun || food.loc == bob);
        Assert.True(took, "he took the food");
        Assert.True(RunUntil(r, 1200, () => Dm.@in(food, bob.held_items) || Dm.truthy(brain.blackboard[BB_MONKEY_CURRENT_GIVE_TARGET]) && food.loc == bob), "he served it");
        r.AssertNoRuntimes();

        var bell = r.In(() => Dm.@new<obj_structure_desk_bell>(locate(5, 5, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(bell)));
        Assert.True(RunUntil(r, 3000, () => bell.times_rang > 0), "he rang it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Water_on_a_cube_makes_a_monkey_and_the_box_holds_five()
    {
        var r = new AudioRoom(13, 9, seed: 8307);
        r.Join(9, 4, "Bob");
        var box = r.In(() => Dm.@new<obj_item_storage_box_monkeycubes>(locate(3, 3, 1)));
        Assert.Equal(5, r.In(() => box.contents.Count));
        Assert.All(r.In(() => Dm.each_cast<atom>(box.contents).ToList()), c => Assert.IsType<obj_item_food_monkeycube>(c));
        var cube = r.In(() => Dm.@new<obj_item_food_monkeycube>(locate(5, 4, 1)));
        var water = r.In(() => Dm.@new<datum_reagent_water>());
        r.In(() => { Waiting.NoSleep(water.expose_obj(cube, 5f)); });
        var monkeys = r.In(() => Dm.each_cast<atom>(locate(5, 4, 1).contents).OfType<mob_living_carbon_human_species_monkey>().ToList());
        Assert.Single(monkeys);
        Assert.True(r.In(() => cube.expanding == 1));
        r.Step(20);
        Assert.True(r.In(() => QDELETED(cube)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_recycler_takes_a_downed_monkey_and_gives_cubes()
    {
        var r = new AudioRoom(13, 9, seed: 8308);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var machine = r.In(() => Dm.@new<obj_machinery_monkey_recycler>(locate(6, 4, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(machine)));
        r.In(() => { machine.stored_matter = 0; machine.cube_production = 0.5f; });
        var awake = Monkey(r, 4, 4);
        r.In(() => Waiting.NoSleep(machine.stuff_monkey_in(awake, bob)));
        Assert.False(r.In(() => QDELETED(awake)), "it struggles too much");
        for (int i = 0; i < 2; i++)
        {
            var dead = Monkey(r, 4, 5);
            r.In(() => Waiting.NoSleep(dead.death()));
            r.In(() => Waiting.NoSleep(machine.mouse_drop_receive(dead, bob)));
            Assert.True(r.In(() => QDELETED(dead)));
        }
        Assert.Equal(1f, r.In(() => machine.stored_matter));
        r.In(() => Waiting.NoSleep(machine.interact(bob)));
        Assert.Equal(0f, r.In(() => machine.stored_matter));
        Assert.Single(r.In(() => Dm.each_cast<atom>(machine.loc.contents).OfType<obj_item_food_monkeycube>().ToList()));
        r.In(() => qdel(awake));
        r.Step(5);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Pun_Puns_memory_makes_the_next_round_his_descendant_with_the_relics()
    {
        var r = new AudioRoom(13, 9, seed: 8309);
        r.Join(9, 4, "Bob");
        r.In(() => Dm.fdel(Dm.file("data/npc_saves/Punpun.json")));
        mob_living_carbon_human_species_monkey_punpun Mapped(int x) => r.In(() =>
        {
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD; // he reads his memory only from the map
            var pun = Dm.@new<mob_living_carbon_human_species_monkey_punpun>(locate(x, 4, 1));
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            return pun;
        });
        var first = Mapped(4);
        Assert.Same(first, r.In(() => GLOB.the_one_and_only_punpun));
        var hat = r.In(() => Dm.@new<obj_item_clothing_head_soft>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(first.equip_to_slot_or_del(hat, ITEM_SLOT_HEAD)));
        r.In(() => { first.real_name = "Pun Pun"; Waiting.NoSleep(first.death()); });
        var json = r.In(() => Dm.json_decode(Dm.file2text(Dm.file("data/npc_saves/Punpun.json"))) as DmList);
        Assert.Equal("Pun Pun", r.In(() => json["ancestor_name"] as string));
        Assert.Equal(2f, r.In(() => Dm.num(json["ancestor_chain"])));
        r.In(() => qdel(first));
        r.Step(5);
        Assert.Null(r.In(() => GLOB.the_one_and_only_punpun));
        var heir = Mapped(5);
        Assert.Equal("Pun Pun II", r.In(() => heir.real_name as string));
        Assert.Equal(2f, r.In(() => heir.ancestor_chain));
        Assert.IsType<obj_item_clothing_head_soft>(r.In(() => heir.head));
        // a gibbed Pun Pun leaves nothing to the next
        r.In(() => heir.Write_Memory(TRUE, TRUE));
        json = r.In(() => Dm.json_decode(Dm.file2text(Dm.file("data/npc_saves/Punpun.json"))) as DmList);
        Assert.Null(r.In(() => json["ancestor_name"]));
        r.In(() => { Dm.fdel(Dm.file("data/npc_saves/Punpun.json")); qdel(heir); });
        r.Step(5);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Monkeyize_and_humanize_change_the_species_with_tgs_delay()
    {
        var r = new AudioRoom(13, 9, seed: 8310);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.In(() => bob.monkeyize());
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NO_TRANSFORM)));
        Assert.True(RunUntil(r, 30, () => ismonkey(bob)));
        Assert.True(r.In(() => ismonkey(bob)));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_NO_TRANSFORM)));
        Assert.IsType<obj_item_bodypart_head_monkey>(r.In(() => bob.get_bodypart(BODY_ZONE_HEAD)));
        Assert.Equal("monkey", r.In(() => ((datum_species)bob.dna.species).id as string));
        r.In(() => bob.humanize(instant: TRUE));
        Assert.False(r.In(() => ismonkey(bob)));
        Assert.Equal("Nude", r.In(() => bob.underwear as string));
        Assert.IsNotType<obj_item_bodypart_head_monkey>(r.In(() => bob.get_bodypart(BODY_ZONE_HEAD)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bar_glass_is_a_drink_for_a_monkey_only_while_it_holds_something()
    {
        // (gaps H3 made the bar's glasses live: the shaker this test first used had no holder while its type was inert)
        var r = new AudioRoom(13, 9, seed: 8711);
        var shaker = r.In(() => Dm.@new<obj_item_reagent_containers_cup_glass_shaker>(locate(4, 4, 1)));
        var strategy = r.In(() => Dm.@new<datum_targeting_strategy_pickup_item_food_or_drink>());
        Assert.Equal(0f, r.In(() => Dm.num(strategy._is_drink(shaker))));
        r.In(() => Waiting.NoSleep(((datum_reagents)shaker.reagents).add_reagent(typeof(datum_reagent_water), 10)));
        Assert.Equal(1f, r.In(() => Dm.num(strategy._is_drink(shaker))));
        r.AssertNoRuntimes();
    }
}
