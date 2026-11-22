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
/// Gaps package C2: Meta's pets (basic/pets, their AI, the pet commands, taming, petting, feeding, the deaths). Each pet spawns, plans on its real tree and goes away with
/// no runtime; Ian obeys speech, follows, fetches and comes back with the item; petting gives tg's heart; the dog tames with its bone; the cat chases a mouse; a goose
/// swallows food and throws it up; a parrot learns a line and says it; the deaths draw tg's dead state, the corgi's hat comes off with it.
/// </summary>
public class PetsTests
{
    private static datum_ai_controller Brain(mob_living mob) => (datum_ai_controller)mob.ai_controller;

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

    public static IEnumerable<object[]> MetaPets() => new[]
    {
        typeof(mob_living_basic_pet_dog_corgi), typeof(mob_living_basic_pet_dog_corgi_puppy_ian), typeof(mob_living_basic_pet_dog_corgi_puppy_void),
        typeof(mob_living_basic_pet_dog_corgi_puppy_slime), typeof(mob_living_basic_pet_dog_pug_mcgriff), typeof(mob_living_basic_pet_cat_runtime),
        typeof(mob_living_basic_parrot_poly), typeof(mob_living_basic_pet_fox_renault), typeof(mob_living_basic_sloth_citrus), typeof(mob_living_basic_lizard_wags_his_tail),
        typeof(mob_living_basic_goose_vomit), typeof(mob_living_basic_pet_penguin_baby_permanent), typeof(mob_living_basic_spider_giant_sgt_araneus),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(MetaPets))]
    public void A_pet_of_Meta_spawns_plans_on_its_own_tree_and_goes_away_cleanly(Type path)
    {
        var r = new AudioRoom(13, 9, seed: 8101);
        mob_living_carbon_human bob = r.Join(9, 4, "Bob");
        var pet = (mob_living)r.In(() => Dm.@new(path, locate(4, 4, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(pet)));
        datum_ai_controller brain = Brain(pet);
        Assert.NotNull(brain);
        Assert.NotEqual("Abstract", r.In(() => brain.behavior_tree_json as string));
        Assert.True(r.In(() => Dm.truthy(LAZYLEN(brain.behavior_nodes))), "the tree loaded");
        RunFor(r, 100);
        Assert.False(r.In(() => QDELETED(pet)));
        r.In(() => qdel(pet));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Ian_without_a_save_is_a_puppy_and_with_one_he_grows_up_with_his_hat()
    {
        var r = new AudioRoom(13, 9, seed: 8102);
        r.Join(9, 4, "Bob");
        var first = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi_ian>(locate(4, 4, 1)));
        r.Step();
        Assert.True(r.In(() => QDELETED(first)));
        Assert.Contains(r.In(() => Dm.each_cast<atom>(locate(4, 4, 1).contents).OfType<mob_living_basic_pet_dog_corgi_puppy_ian>().ToList()), m => m.name == "Ian");
        // his first round wrote his age; the next round's Ian reads it and is grown up
        var second = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi_ian>(locate(5, 4, 1)));
        r.Step();
        Assert.False(r.In(() => QDELETED(second)));
        Assert.Equal(1f, r.In(() => second.age));
        r.In(() => Dm.@new<obj_item_clothing_head_hats_warden>(locate(5, 4, 1)));
        var hat = r.In(() => Dm.locate<obj_item_clothing_head_hats_warden>(locate(5, 4, 1)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(second.place_on_head(hat)))));
        Assert.StartsWith("Officer", r.In(() => second.name)); // the fashion's name
        r.In(() => second.Write_Memory(FALSE));
        var third = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi_ian>(locate(6, 4, 1)));
        r.Step();
        Assert.Equal(2f, r.In(() => third.age));
        Assert.IsType<obj_item_clothing_head_hats_warden>(r.In(() => third.inventory_head));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Ian_obeys_a_friends_speech_follows_and_fetches()
    {
        var r = new AudioRoom(15, 9, seed: 8103);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var ian = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi_puppy_ian>(locate(3, 4, 1)));
        datum_ai_controller brain = Brain(ian);
        // not a friend yet: he does not listen
        r.In(() => Waiting.NoSleep(bob.say("heel")));
        r.Step();
        Assert.Null(r.In(() => brain.blackboard[BB_ACTIVE_PET_COMMAND]));
        Assert.Contains("Bob", r.In(() => Waiting.NoSleep(ian.befriend(bob)) == null ? "" : "Bob"));
        Assert.True(r.In(() => Dm.truthy(ian.has_ally(bob))));
        // activate_on_befriend: the dog's follow runs at once
        Assert.IsType<datum_pet_command_follow_dog>(r.In(() => brain.blackboard[BB_ACTIVE_PET_COMMAND]));
        Assert.Same(bob, r.In(() => brain.blackboard[BB_CURRENT_PET_TARGET]));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(12, 4, 1))));
        Assert.True(RunUntil(r, 200, () => get_dist(ian, bob) <= 2), r.In(() => $"Ian stands at {ian.x},{ian.y}"));

        // "sit": he stays where he is
        r.In(() => Waiting.NoSleep(bob.say("stay")));
        r.Step();
        Assert.IsType<datum_pet_command_idle>(r.In(() => brain.blackboard[BB_ACTIVE_PET_COMMAND]));
        float x = r.In(() => ian.x), y = r.In(() => ian.y);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 4, 1))));
        RunFor(r, 60);
        Assert.Equal((x, y), r.In(() => (ian.x, ian.y)));

        // "fetch" and a point at a toy: he picks it up and brings it, a click takes it from his mouth
        var toy = r.In(() => Dm.@new<obj_item_toy_plush_lizard_plushie>(locate(8, 6, 1)));
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(10, 4, 1))));
        r.In(() => Waiting.NoSleep(bob.say("fetch")));
        r.Step();
        Assert.IsType<datum_pet_command_fetch>(r.In(() => brain.blackboard[BB_ACTIVE_PET_COMMAND]));
        r.In(() => Waiting.NoSleep(bob._pointed(toy)));
        r.Step();
        Assert.Same(toy, r.In(() => brain.blackboard[BB_CURRENT_PET_TARGET]));
        Assert.True(RunUntil(r, 300, () => toy.loc != locate(8, 6, 1)), "Ian picked the toy up");
        Assert.True(RunUntil(r, 300, () => toy.loc == get_turf(bob) || (get_dist(ian, bob) <= 1 && toy.loc == ian) || get_dist(toy, bob) <= 1), r.In(() => $"Ian at {ian.x},{ian.y}, the toy in {toy.loc}"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Petting_gives_tgs_reaction_and_harm_makes_a_friend_an_enemy()
    {
        var r = new AudioRoom(13, 9, seed: 8104);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var pug = r.In(() => Dm.@new<mob_living_basic_pet_dog_pug_mcgriff>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(pug.befriend(bob)));
        var feed = GapsHarness.Listen(r);
        r.In(() => Waiting.NoSleep(pug.attack_hand(bob)));
        Assert.Contains(r.In(() => Dm.each_cast<atom>(get_turf(pug).contents).ToList()), a => a is obj_effect_temp_visual_heart);
        Assert.True(feed.Any("pet"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        // the heart is tg's picture from the simple animal sheet
        var heart = r.In(() => Dm.locate<obj_effect_temp_visual_heart>(get_turf(pug)));
        Assert.Equal("heart", r.In(() => heart.icon_state));

        // a kick ends it, with tg's line
        r.In(() => bob.combat_mode = TRUE);
        r.In(() => Waiting.NoSleep(pug.attack_hand(bob)));
        Assert.False(r.In(() => Dm.truthy(pug.has_ally(bob))));
        Assert.True(feed.Any("look of betrayal"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        // and now it retaliates: the bite is on the retaliate list
        Assert.True(r.In(() => Dm.@in(bob, Brain(pug).blackboard[BB_BASIC_MOB_RETALIATE_LIST])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dog_eats_a_bone_from_the_hand_and_a_jumbo_bone_tames_it_at_once()
    {
        var r = new AudioRoom(13, 9, seed: 8105);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var corgi = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(4, 4, 1)));
        // tame_chance 30, bonus 15: at most five bones, each eaten
        for (int i = 0; i < 6 && !r.In(() => Dm.truthy(corgi.has_ally(bob))); i++)
        {
            var bone = r.In(() => Dm.@new<obj_item_stack_sheet_bone>(locate(5, 4, 1)));
            r.In(() => Waiting.NoSleep(bone.melee_attack_chain(bob, corgi)));
            r.Step();
            Assert.True(r.In(() => QDELETED(bone) || Dm.num(bone.amount) == 0), "the bone was eaten");
        }
        Assert.True(r.In(() => Dm.truthy(corgi.has_ally(bob))), "five bones tame a corgi at tg's chances");

        var fox = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(6, 4, 1)));
        var jumbo = r.In(() => Dm.@new<obj_item_dog_bone>(locate(5, 4, 1)));
        r.In(() => fox.health = 5);
        r.In(() => Waiting.NoSleep(jumbo.pre_attack(fox, bob)));
        Assert.True(r.In(() => Dm.truthy(fox.has_ally(bob))));
        Assert.True(r.In(() => QDELETED(jumbo)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cat_chases_and_kills_a_mouse()
    {
        var r = new AudioRoom(15, 9, seed: 8106);
        r.Join(13, 7, "Bob");
        r.In(() =>
        {
            for (int x = 1; x <= 15; x++) // a live mouse breathes (5 health and 1 brute a second in vacuum, atmos_requirements.dm)
                for (int y = 1; y <= 9; y++)
                    if (locate(x, y, 1) is turf_open open)
                        open.air = SSair.parse_gas_string("o2=21;n2=79;TEMP=293.15") as datum_gas_mixture;
        });
        var cat = r.In(() => Dm.@new<mob_living_basic_pet_cat>(locate(3, 4, 1)));
        var mouse = r.In(() => Dm.@new<mob_living_basic_mouse_gray>(locate(9, 4, 1)));
        datum_ai_controller brain = Brain(cat);
        Assert.True(RunUntil(r, 600, () => brain.blackboard[BB_MOUSE_TARGET] == mouse || QDELETED(mouse) || mouse.stat == DEAD), "the cat's source found the mouse");
        Assert.True(RunUntil(r, 600, () => get_dist(cat, mouse) <= 1 || mouse.stat == DEAD), r.In(() => $"the cat at {cat.x},{cat.y}"));
        Assert.True(RunUntil(r, 1200, () => mouse.stat == DEAD), "a pounce at tg's 70% every 10 s, or the cat's combat branch: a live mouse is of another faction");
        var splatted = r.In(() => Dm.@new<mob_living_basic_mouse_gray>(locate(2, 2, 1)));
        r.In(() => splatted.splat());
        Assert.Equal("mouse_gray_splat", r.In(() => splatted.icon_dead));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cat_carries_a_dead_mouse_and_shows_it()
    {
        var r = new AudioRoom(15, 9, seed: 8107);
        r.Join(13, 7, "Bob");
        var cat = r.In(() => Dm.@new<mob_living_basic_pet_cat>(locate(3, 4, 1)));
        var food = r.In(() => Dm.@new<obj_item_food_deadmouse>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(cat.early_melee_attack(food)));
        Assert.Same(food, r.In(() => cat.held_food));
        Assert.Same(cat, r.In(() => food.loc));
        Assert.Equal("cat_mouse_overlay", r.In(() => cat.held_item_overlay?.icon_state as string));
        Assert.True(r.In(() => Dm.truthy(length(cat.overlays))), "the mouse in its mouth is drawn");
        // resting draws tg's other state
        r.In(() => Waiting.NoSleep(cat.set_resting(TRUE, TRUE)));
        Assert.Equal("cat2_rest", r.In(() => cat.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goose_swallows_food_and_the_vomit_goose_throws_it_all_up()
    {
        var r = new AudioRoom(15, 9, seed: 8108);
        r.Join(13, 7, "Bob");
        var goose = r.In(() => Dm.@new<mob_living_basic_goose_vomit>(locate(5, 4, 1)));
        var burger = r.In(() => Dm.@new<obj_item_food_donkpocket>(locate(6, 4, 1)));
        Assert.True(RunUntil(r, 400, () => burger.loc == goose), r.In(() =>
        {
            DmList lines = Dm.list();
            ((datum_bt_node)Brain(goose).behavior_nodes[1]).append_full_tree_state(lines, "");
            return $"the goose at {goose.x},{goose.y} food {Brain(goose).blackboard[BB_TARGET_FOOD]} cooldown {Brain(goose).blackboard[BB_NEXT_FOOD_EAT]} now {world.time}\n" + lines.Join("\n");
        }));
        Assert.True(r.In(() => Dm.num(Brain(goose).blackboard[BB_GOOSE_VOMIT_CHANCE]) > 0), "a meal raises the vomit chance");
        // vomit: tg's picture and status effect
        r.In(() => Waiting.NoSleep(goose.vomit()));
        Assert.Equal("vomit", r.In(() => goose.icon_state));
        Assert.True(RunUntil(r, 100, () => burger.loc != goose), "the burger came back up");
        Assert.True(RunUntil(r, 100, () => goose.icon_state == goose.icon_living), "tg's picture comes back when it's done");
        // its death spills what is inside
        var pocket = r.In(() => Dm.@new<obj_item_food_donkpocket>(goose));
        r.In(() => Waiting.NoSleep(goose.death()));
        Assert.True(r.In(() => isturf(pocket.loc)), "content_barfer");
        Assert.NotEmpty(r.In(() => Dm.each_cast<atom>(world.contents).OfType<obj_effect_decal_cleanable_vomit>().ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Poly_learns_a_line_he_hears_and_can_say_it_and_the_cracker_tames_him()
    {
        var r = new AudioRoom(13, 9, seed: 8109);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var poly = r.In(() => Dm.@new<mob_living_basic_parrot_poly>(locate(4, 4, 1)));
        Assert.Same(typeof(obj_item_radio_headset_headset_eng), r.In(() => poly.ears?.GetType()));
        r.In(() => Waiting.NoSleep(bob.say("The crystal is fine")));
        r.Step();
        var repeat = r.In(() => (datum_component_listen_and_repeat)poly.GetComponent(typeof(datum_component_listen_and_repeat)));
        Assert.True(r.In(() => Dm.@in("The crystal is fine", repeat.speech_buffer)), r.In(() => string.Join(" | ", Dm.each<object>(repeat.speech_buffer))));
        Assert.True(r.In(() => Dm.@in("Poly wanna cracker!", repeat.speech_buffer)));
        // the memory it writes at the round's end holds it
        r.In(() => poly.Write_Memory(FALSE));
        Assert.Contains("The crystal is fine", r.In(() => Dm.file2text("data/npc_saves/Poly.json")));

        var cracker = r.In(() => Dm.@new<obj_item_food_cracker>(locate(5, 4, 1)));
        r.In(() => bob.combat_mode = FALSE);
        r.In(() => Waiting.NoSleep(cracker.melee_attack_chain(bob, poly)));
        Assert.True(r.In(() => Dm.truthy(poly.has_ally(bob))), "tg's parrot tames at 100%");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_pet_draws_tgs_corpse_and_drops_what_it_carries()
    {
        var r = new AudioRoom(13, 9, seed: 8110);
        r.Join(9, 4, "Bob");
        var corgi = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(4, 4, 1)));
        var hat = r.In(() => Dm.@new<obj_item_clothing_head_hats_warden>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(corgi.place_on_head(hat)));
        Assert.Contains(r.In(() => Dm.each_cast<object>(corgi.overlays).ToList()), o => o != null);
        r.In(() => Waiting.NoSleep(corgi.death()));
        Assert.Equal(DEAD, r.In(() => corgi.stat));
        Assert.Equal("corgi_dead", r.In(() => corgi.icon_state));
        Assert.False(r.In(() => QDELETED(corgi))); // a corpse stays: basic_mob_flags has no DEL_ON_DEATH
        r.In(() => Waiting.NoSleep(corgi.gib()));
        Assert.Same(locate_turf(r, 4, 4), r.In(() => hat.loc)); // undress_dog
        var poly = r.In(() => Dm.@new<mob_living_basic_parrot_poly>(locate(6, 4, 1)));
        var ears = r.In(() => poly.ears);
        r.In(() => Waiting.NoSleep(poly.death()));
        Assert.Equal("parrot_dead", r.In(() => poly.icon_state));
        Assert.Same(locate_turf(r, 6, 4), r.In(() => ears.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_crewman_scoops_a_pet_up_and_it_wriggles_free_when_he_lets_go()
    {
        var r = new AudioRoom(13, 9, seed: 8111);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var fox = r.In(() => Dm.@new<mob_living_basic_pet_fox_renault>(locate(4, 4, 1)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(fox.mob_try_pickup(bob, TRUE)))));
        var holder = r.In(() => bob.get_active_held_item() as obj_item_mob_holder);
        Assert.NotNull(holder);
        Assert.Same(holder, r.In(() => fox.loc));
        Assert.Equal("Renault", r.In(() => holder.name));
        Assert.Equal("fox", r.In(() => holder.inhand_icon_state));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(holder)));
        RunFor(r, 2);
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => fox.loc));
        Assert.True(r.In(() => QDELETED(holder)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_collar_names_a_corgi_and_draws_tgs_collar_and_tag()
    {
        var r = new AudioRoom(13, 9, seed: 8112);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var corgi = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(4, 4, 1)));
        var collar = r.In(() => Dm.@new<obj_item_clothing_neck_petcollar>(locate(5, 4, 1)));
        r.In(() => collar.tagname = "Spot");
        var before = r.In(() => length(corgi.overlays));
        r.In(() => Waiting.NoSleep(collar.forceMove(corgi)));
        Assert.Contains("Spot", r.In(() => corgi.name));
        Assert.True(r.In(() => length(corgi.overlays)) > before, "the collar and its tag are drawn");
        r.In(() => Waiting.NoSleep(collar.forceMove(locate(5, 4, 1))));
        Assert.Equal(r.In(() => length(corgi.overlays)), before);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Poly_steals_a_small_thing_and_repeats_a_line_over_his_headset()
    {
        var r = new AudioRoom(17, 11, seed: 8113);
        r.Join(15, 8, "Bob");
        var feed = GapsHarness.Listen(r);
        var poly = r.In(() => Dm.@new<mob_living_basic_parrot_poly>(locate(3, 4, 1)));
        var pen = r.In(() => Dm.@new<obj_item_pen>(locate(8, 4, 1)));
        Assert.True(RunUntil(r, 1800, () => pen.loc != locate(8, 4, 1)), r.In(() => $"Poly at {poly.x},{poly.y}, nest {Brain(poly).blackboard[BB_HOARD_LOCATION]}, target {Brain(poly).blackboard[BB_CURRENT_TARGET]}"));
        // the leaf itself: a line with a channel prefix goes out of his mouth (the parrot's say cuts the prefix to its channel)
        r.In(() => poly.speech_probability_rate = 100);
        r.In(() => poly.update_speech_blackboards());
        Assert.True(RunUntil(r, 1800, () => feed.Chat.Any(c => c.Html != null && c.Html.Contains("Poly", StringComparison.Ordinal) && c.Html.Contains("say", StringComparison.OrdinalIgnoreCase))), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_throw_sends_a_friendly_dog_after_it_and_the_radials_fetch_takes_the_next_click()
    {
        var r = new AudioRoom(15, 9, seed: 8114);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var ian = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(4, 4, 1)));
        datum_ai_controller brain = Brain(ian);
        r.In(() => Waiting.NoSleep(ian.befriend(bob)));
        r.In(() => Waiting.NoSleep(bob.say("free")));
        r.Step();
        Assert.Null(r.In(() => brain.blackboard[BB_ACTIVE_PET_COMMAND]));
        var toy = r.In(() => Dm.@new<obj_item_toy_plush_lizard_plushie>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(toy)));
        r.In(() => Waiting.NoSleep(bob.throw_item(locate(10, 4, 1))));
        Assert.True(RunUntil(r, 100, () => brain.blackboard[BB_CURRENT_PET_TARGET] == toy || brain.blackboard[BB_SIMPLE_CARRY_ITEM] == toy), r.In(() => $"the toy in {toy.loc}, the command {brain.blackboard[BB_ACTIVE_PET_COMMAND]}"));
        Assert.Same(bob, r.In(() => brain.blackboard[BB_FETCH_DELIVER_TO]));

        // the radial's fetch: the mouse waits for a point; the next click is it, with tg's emote
        var feed = GapsHarness.Listen(r);
        var ball = r.In(() => Dm.@new<obj_item_toy_plush_lizard_plushie>(locate(6, 6, 1)));
        var command = r.In(() => (datum_pet_command)((datum_component_obeys_commands)ian.GetComponent(typeof(datum_component_obeys_commands))).available_commands["Fetch"]);
        r.In(() => brain.clear_blackboard_key(BB_FETCH_DELIVER_TO));
        r.In(() => Waiting.NoSleep(command.try_activate_command(bob, radial_command: TRUE)));
        GapsHarness.Click(r, bob, ball);
        Assert.Same(ball, r.In(() => brain.blackboard[BB_CURRENT_PET_TARGET]));
        Assert.True(feed.Any("signals"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Null(r.In(() => bob.client.mouse_override_icon));
        r.AssertNoRuntimes();
    }

    private static turf locate_turf(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));
}
