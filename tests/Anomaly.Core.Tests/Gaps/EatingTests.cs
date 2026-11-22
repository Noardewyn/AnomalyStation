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
/// Gaps package S1 (D291): eating, drinking and hunger in a booted toy room (edible.dm, _food.dm, food_trash.dm, the stomach's
/// handle_hunger, food_reagents.dm, the hunger display of screen_objects.dm, carbon.dm's vomit). A click on oneself with food eats it a
/// bite a second (2 u of a donk pocket's 5 u into the stomach, tg's message and eatfood.ogg) until it is gone; a click on someone else
/// feeds them after tg's 3 s; the hunger display follows the fullness thresholds; a full stomach refuses more; long overeating makes a
/// body fat; vomiting leaves a decal with the stomach's reagents; a cup is drunk a gulp at a time.
/// </summary>
public class EatingTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    private static datum_reagents Stomach(mob_living_carbon who) => (datum_reagents)((obj_item_organ_stomach)who.get_organ_slot(ORGAN_SLOT_STOMACH)).reagents;

    private static float In(datum_reagents holder, Type reagent) => Dm.num(holder.get_reagent_amount(reagent));

    private static datum_component_edible Edible(atom food) => (datum_component_edible)food.GetComponent(typeof(datum_component_edible));

    [Fact]
    public void AClickOnOneselfEatsTheFoodABiteASecondUntilItIsGone()
    {
        var (r, feed, bob) = Room(9701);
        r.In(() => Waiting.NoSleep(bob.set_nutrition(40))); // fullness 50 or less: "hungrily ..., gobbling it down!" (edible.dm:426)
        var pocket = Hold<obj_item_food_donkpocket>(r, bob);
        r.In(() =>
        {
            Assert.NotNull(Edible(pocket));
            Assert.Equal(5f, ((datum_reagents)pocket.reagents).total_volume); // nutriment 3, protein 2 (donkpocket.dm:7)
        });
        float lining = r.In(() => In(Stomach(bob), typeof(datum_reagent_consumable_nutriment)));

        Click(r, bob, bob);
        r.Step(Second + 2); // eat_time 1 s (edible.dm:58)
        r.In(() =>
        {
            Assert.Equal(1f, Edible(pocket).bitecount);
            Assert.Equal(3f, ((datum_reagents)pocket.reagents).total_volume, 3); // bite_consumption 2
            Assert.True(Stomach(bob).total_volume - lining > 1.9f, "the bite reached the stomach");
        });
        Assert.True(feed.Any("hungrily"), "the bite's message: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/items/eatfood.ogg"));

        r.Step(3 * Second); // the next bites follow on their own (edible.dm:482)
        Assert.True(r.In(() => QDELETED(pocket)));
        Assert.True(feed.Any("There is nothing left of"), "the last bite consumes it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnExoticMealIsRemembered()
    {
        var (r, _, bob) = Room(9707);
        r.In(() => Waiting.NoSleep(bob.set_nutrition(40)));
        var pocket = Hold<obj_item_food_donkpocket>(r, bob);
        r.In(() => pocket.venue_value = FOOD_PRICE_EXOTIC); // checkLiked's good_food memory (edible.dm:613)
        r.Step(5 * Second); // checkLiked waits 5 s after its last check, which starts at 0 (edible.dm:604)
        Click(r, bob, bob);
        r.Step(Second + 2);
        var memory = r.In(() => bob.mind.memories[typeof(datum_memory_good_food)] as datum_memory_good_food);
        Assert.NotNull(memory);
        Assert.Equal(r.In(() => Dm.str(pocket.name)), Dm.str(memory.food));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FeedingSomeoneElseTakesThreeSecondsAndTellsThem()
    {
        var (r, feed, bob) = Room(9702);
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        var cracker = Hold<obj_item_food_cracker>(r, bob);
        float before = r.In(() => Stomach(alice).total_volume);
        Click(r, bob, alice);
        Assert.True(feed.Any("attempts to feed"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.Step(2 * Second);
        r.In(() => Assert.Equal(before, Stomach(alice).total_volume)); // EAT_TIME_FORCE_FEED 3 s
        r.Step(Second + 4);
        Assert.Contains(feed.Chat, c => ReferenceEquals(c.Target, alice) && c.Html.Contains("forces you to eat", StringComparison.Ordinal));
        r.In(() => Assert.True(Stomach(alice).total_volume > before, "the bite reached her stomach"));
        r.In(() => Assert.Equal(1f, Edible(cracker).bitecount)); // a forced bite does not repeat
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHungerDisplayFollowsTheFullnessThresholds()
    {
        var (r, _, bob) = Room(9703);
        var bar = r.In(() => bob.hud_used.screen_objects[HUD_MOB_HUNGER] as atom_movable_screen_hunger);
        Assert.NotNull(bar);
        r.In(() =>
        {
            Assert.Equal(ui_hunger, bar.screen_loc); // a human has a mood since gaps MS1, so the bar keeps ui_hunger beside the face (screen_objects.dm:1176)
            Assert.True(Dm.@in(bar.hunger_bar, bar.vis_contents), "the bar is in its vis_contents");
            void At(float nutrition, float state)
            {
                Waiting.NoSleep(Stomach(bob).clear_reagents());
                Waiting.NoSleep(bob.set_nutrition(nutrition));
                bar.update_hunger_bar(instant: TRUE);
                Assert.Equal(state, Dm.num(bar.state));
            }
            At(NUTRITION_LEVEL_FULL + 50, 4 /* HUNGER_STATE_FULL */);
            Assert.Equal(255f, bar.alpha);
            At(NUTRITION_LEVEL_FED, 3 /* HUNGER_STATE_FINE */);
            Assert.Equal(0f, bar.alpha); // fine and nothing digesting: it fades out
            At(NUTRITION_LEVEL_VERY_HUNGRY - 10, 2 /* HUNGER_STATE_HUNGRY */);
            Assert.Equal(255f, bar.alpha);
            At(NUTRITION_LEVEL_STARVING - 50, 0 /* HUNGER_STATE_STARVING */);
            Assert.True(Dm.truthy(bar.get_filter("hunger_outline")), "the starving outline");
            var inner = (atom_movable_screen_hunger_bar)bar.hunger_bar;
            DmList rgb = Dm.rgb2num(Dm.str(inner.color)); // 100 / 550 rounds to the band 0.2: tg's gradient stop #FF8000
            Assert.Equal(255f, Dm.num(rgb[1]));
            Assert.InRange(Dm.num(rgb[2]), 120f, 136f);
            Assert.True(Dm.truthy(inner.get_filter("hunger_bar_mask")), "the bar's mask");
            At(NUTRITION_LEVEL_FED, 3 /* HUNGER_STATE_FINE */);
            Assert.False(Dm.truthy(bar.get_filter("hunger_outline")));
            ADD_TRAIT(bob, TRAIT_FAT, "test");
            bar.update_hunger_bar(instant: TRUE);
            Assert.Equal(5 /* HUNGER_STATE_FAT */, Dm.num(bar.state));
            Assert.Equal(COLOR_DARK, Dm.str(bar.food_image.color));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFullStomachRefusesMoreAndLongOvereatingMakesABodyFat()
    {
        var (r, feed, bob) = Room(9704);
        r.In(() => Waiting.NoSleep(bob.set_nutrition(NUTRITION_LEVEL_FAT + 50)));
        var pocket = Hold<obj_item_food_donkpocket>(r, bob);
        Click(r, bob, bob);
        r.Step(Second + 2);
        Assert.True(feed.Any("You cannot force any more of"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() => Assert.Equal(0f, Edible(pocket).bitecount));

        r.In(() =>
        {
            var stomach = (obj_item_organ_stomach)bob.get_organ_slot(ORGAN_SLOT_STOMACH);
            bob.overeatduration = OVEREAT_TIME_LIMIT;
            Waiting.NoSleep(stomach.handle_hunger(bob, SSMOBS_DT()));
            Assert.True(HAS_TRAIT_FROM(bob, TRAIT_FAT, OBESITY));
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_obesity))));
            bob.overeatduration = 0;
            Waiting.NoSleep(bob.set_nutrition(NUTRITION_LEVEL_FED));
            Waiting.NoSleep(stomach.handle_hunger(bob, SSMOBS_DT()));
            Assert.False(HAS_TRAIT(bob, TRAIT_FAT));
        });
        Assert.True(feed.Any("You suddenly feel blubbery!") && feed.Any("You feel fit again!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void VomitingLeavesADecalWithTheStomachsReagentsAndCostsNutrition()
    {
        var (r, feed, bob) = Room(9705);
        r.In(() =>
        {
            Waiting.NoSleep(bob.set_nutrition(NUTRITION_LEVEL_FED));
            Waiting.NoSleep(Stomach(bob).add_reagent(typeof(datum_reagent_water), 20));
            Assert.Equal(TRUE, Waiting.NoSleep(bob.vomit(VOMIT_CATEGORY_DEFAULT, distance: 0)));
            Assert.Equal(NUTRITION_LEVEL_FED - 10, bob.nutrition, 3); // lost_nutrition 10
            var decal = Dm.locate<obj_effect_decal_cleanable_vomit_toxic>(bob.loc);
            Assert.NotNull(decal);
            Assert.True(Dm.num(((datum_reagents)decal.reagents).get_reagent_amount(typeof(datum_reagent_water))) > 1.9f); // purge_ratio 0.1
        });
        Assert.True(feed.Any("throws up!") || feed.Any("You throw up!"));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/splat.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACupIsDrunkAGulpAtATimeAndItsJuiceFeeds()
    {
        var (r, feed, bob) = Room(9706);
        var cup = Hold<obj_item_reagent_containers_cup_beaker>(r, bob);
        r.In(() => Waiting.NoSleep(((datum_reagents)cup.reagents).add_reagent(typeof(datum_reagent_consumable_applejuice), 30)));
        Click(r, bob, bob);
        r.Step(4);
        Assert.True(feed.Any("You swallow a gulp of"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() =>
        {
            Assert.Equal(25f, ((datum_reagents)cup.reagents).total_volume, 3); // gulp_size 5
            // the gulp is in the stomach, or partly in his blood if his Life ticked since (the stomach passes it on, and the blood
            // metabolizes 0.4u of juice a Life tick: REAGENTS_METABOLISM * 2 seconds)
            Assert.InRange(In(Stomach(bob), typeof(datum_reagent_consumable_applejuice)) + In((datum_reagents)bob.reagents, typeof(datum_reagent_consumable_applejuice)), 4.6f - 0.001f, 5f + 0.001f);
            float nutrition = bob.nutrition;
            Waiting.NoSleep(Stomach(bob).trans_to(bob, 5, target_id: typeof(datum_reagent_consumable_applejuice)));
            var juice = (datum_reagent_consumable)((datum_reagents)bob.reagents).has_reagent(typeof(datum_reagent_consumable_applejuice));
            Waiting.NoSleep(juice.on_mob_life(bob, SSMOBS_DT(), 1));
            // 0.5 * nutriment_factor * REAGENTS_METABOLISM * purity * 2 * ratio * seconds (food_reagents.dm:32)
            Assert.Equal(nutrition + 0.5f * Dm.num(juice.get_nutriment_factor(bob)) * 2, bob.nutrition, 3);
        });
        r.AssertNoRuntimes();
    }
}
