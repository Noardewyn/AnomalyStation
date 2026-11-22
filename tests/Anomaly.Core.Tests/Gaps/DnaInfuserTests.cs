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
/// Gaps package P3b (D327): the DNA infuser. A dead mouse in the slot and a subject in the chamber: the operator starts the infusion,
/// the needles prick, the subject grows a rat organ; four of them make the rat set bonus (ventcrawling), which unlocks the next tier;
/// a sample too complex for the tier grows a fly organ and prints an error report; every infused organ goes in and out of a human with
/// no runtime; the moth wings burn off and heal, the lizard tongue hisses, the penguin ears waddle; the DNA infusion book lists the
/// entries in its window.
/// </summary>
public class DnaInfuserTests
{
    private const int InfuserX = 4, InfuserY = 4;

    private sealed record Lab(AudioRoom R, Feed Feed, mob_living_carbon_human Operator, mob_living_carbon_human Subject, obj_machinery_dna_infuser Infuser);

    private static string Diag(Lab l) => "chat: " + string.Join(" | ", l.Feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", l.R.Room.RuntimeText);

    private static Lab Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        r.In(() => (get_area(locate(InfuserX, InfuserY, 1)) as area).requires_power = FALSE); // the toy room has no APC
        var infuser = r.In(() => Dm.@new<obj_machinery_dna_infuser>(locate(InfuserX, InfuserY, 1)));
        mob_living_carbon_human op = r.Join(InfuserX + 1, InfuserY, "Operator");
        mob_living_carbon_human subject = r.Dummy(InfuserX - 1, InfuserY);
        r.Step(Second);
        r.In(() => ADD_TRAIT(op, TRAIT_ADVANCEDTOOLUSER, "test"));
        return new Lab(r, feed, op, subject, infuser);
    }

    // a sample in the slot (through item_interaction, as a click with it in hand) and the subject in the chamber
    private static void Load<T>(Lab l) where T : obj_item
    {
        l.R.In(() =>
        {
            var sample = Dm.@new<T>(l.Operator.loc);
            Waiting.NoSleep(l.Operator.put_in_active_hand(sample));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(l.Infuser.item_interaction(l.Operator, sample)));
            if (!Dm.truthy(l.Infuser.state_open))
                Waiting.NoSleep(l.Infuser.open_machine(drop: FALSE));
            Waiting.NoSleep(l.Infuser.close_machine(l.Subject));
        });
        l.R.Step(2);
        Assert.Same(l.Infuser, l.R.In(() => l.Subject.loc));
    }

    private static void Infuse(Lab l)
    {
        l.R.In(() => Waiting.NoSleep(l.Infuser.interact(l.Operator)));
        Assert.Equal(1f, l.R.In(() => l.Infuser.infusing));
        Assert.Equal("infuser_on", l.R.In(() => l.Infuser.icon_state));
        l.R.Step(5 * Second); // INFUSING_TIME, 4 s
        Assert.Equal(0f, l.R.In(() => l.Infuser.infusing));
    }

    [Fact]
    public void Four_rat_infusions_grow_the_rat_set_and_unlock_the_next_tier()
    {
        Lab l = Room(9401);
        float brute = l.R.In(() => Dm.num(l.Subject.get_brute_loss()));
        Load<obj_item_food_deadmouse>(l);
        Infuse(l);
        Assert.True(l.R.In(() => Dm.num(l.Subject.get_brute_loss()) + Dm.num(l.Subject.get_fire_loss())) > brute, Diag(l)); // the needles
        Assert.True(l.Feed.Any("Little needles repeatedly prick you!"), Diag(l));
        Assert.True(l.Feed.Any("You feel yourself becoming more... skittish?"), Diag(l));
        Assert.Null(l.R.In(() => l.Infuser.infusing_from)); // the mouse is used up
        Assert.NotSame(l.Infuser, l.R.In(() => l.Subject.loc)); // the door opens and lets the subject out
        int ratOrgans() => l.R.In(() => new Type[] { typeof(obj_item_organ_eyes_night_vision_rat), typeof(obj_item_organ_heart_rat), typeof(obj_item_organ_stomach_rat), typeof(obj_item_organ_tongue_rat) }
            .Count(t => l.Subject.get_organ_by_type(t) != null));
        Assert.Equal(1, ratOrgans());

        for (int i = 2; i <= 4; i++)
        {
            Load<obj_item_food_deadmouse>(l);
            Infuse(l);
            Assert.Equal(i, ratOrgans());
        }
        var bonus = l.R.In(() => l.Subject.has_status_effect(typeof(datum_status_effect_organ_set_bonus_rat)) as datum_status_effect_organ_set_bonus_rat);
        Assert.NotNull(bonus);
        Assert.Equal(1f, l.R.In(() => bonus.bonus_active));
        Assert.True(l.R.In(() => HAS_TRAIT(l.Subject, TRAIT_VENTCRAWLER_NUDE)));
        Assert.True(l.R.In(() => HAS_TRAIT(l.Subject, TRAIT_DWARF))); // the rat heart
        Assert.Equal(2f, l.R.In(() => l.Infuser.max_tier_allowed)); // a full tier one set unlocks tier two
        Assert.True(l.Feed.Any("dings as it records the results of the full infusion"), Diag(l));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_sample_too_complex_for_the_tier_grows_a_fly_organ_and_prints_an_error_report()
    {
        Lab l = Room(9402);
        Load<obj_item_food_meat_slab_gondola>(l); // tier two, the infuser starts at tier one
        Infuse(l);
        Type[] fly = { typeof(obj_item_organ_appendix_fly), typeof(obj_item_organ_eyes_fly), typeof(obj_item_organ_heart_fly), typeof(obj_item_organ_lungs_fly), typeof(obj_item_organ_stomach_fly), typeof(obj_item_organ_tongue_fly) };
        Assert.Equal(1, l.R.In(() => fly.Count(t => l.Subject.get_organ_by_type(t) != null)));
        var report = l.R.In(() => Dm.locate<obj_item_paper>(l.Infuser.loc));
        Assert.NotNull(report);
        Assert.Equal("error report - 'Overcomplexity'", l.R.In(() => report.name));
        Assert.True(l.Feed.Any("prints an error report"), Diag(l));
        Assert.Equal(1f, l.R.In(() => l.Infuser.max_tier_allowed));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void An_empty_machine_refuses_a_living_sample_and_a_dna_less_subject()
    {
        Lab l = Room(9403);
        mob_living_basic_carp carp = l.R.In(() => Dm.@new<mob_living_basic_carp>(locate(2, 2, 1)));
        l.R.Step(2);
        Assert.False(l.R.In(() => l.Infuser.is_valid_infusion(carp, l.Operator))); // only dead creatures
        Assert.True(l.Feed.Any("balloon: only dead creatures!"), Diag(l));
        l.R.In(() => Waiting.NoSleep(carp.death()));
        l.R.Step(2);
        Assert.True(l.R.In(() => l.Infuser.is_valid_infusion(carp, l.Operator)));
        Assert.Same(l.R.In(() => GLOB.infuser_entries[typeof(datum_infuser_entry_carp)]), l.R.In(() => carp.get_infusion_entry()));
        var paper = l.R.In(() => Dm.@new<obj_item_paper>(locate(2, 3, 1)));
        Assert.False(l.R.In(() => l.Infuser.is_valid_infusion(paper, l.Operator))); // only creatures
        Assert.Same(l.R.In(() => GLOB.infuser_entries[typeof(datum_infuser_entry_fly)]), l.R.In(() => paper.get_infusion_entry())); // the rejected one
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void Every_infused_organ_goes_in_and_out_of_a_human_with_no_runtimes()
    {
        Lab l = Room(9404);
        List<Type> organs = l.R.In(() => Dm.each_cast<datum_infuser_entry>(assoc_to_values(GLOB.infuser_entries) as DmList)
            .SelectMany(e => Dm.each(e.output_organs).Cast<Type>()).Distinct().ToList());
        Assert.True(organs.Count > 30, organs.Count.ToString());
        int x = 2;
        foreach (Type organType in organs)
        {
            mob_living_carbon_human human = l.R.Dummy(x, 2);
            x = x == 9 ? 2 : x + 1;
            l.R.Step(2);
            var organ = l.R.In(() =>
            {
                var made = Dm.@new(organType) as obj_item_organ;
                // a visual organ reads its look from the DNA's feature (a player's preference); tg stack traces for a plain human
                // that has none or "None" (mutant_bodypart_overlay.dm:33 and :37, the stoat's snout, the lizard's tail), so the
                // test gives the human one first
                if (made.bodypart_overlay is datum_bodypart_overlay_mutant overlay && !string.IsNullOrEmpty(overlay.feature_key)
                    && (human.dna.features[overlay.feature_key] == null || Dm.eq(human.dna.features[overlay.feature_key], SPRITE_ACCESSORY_NONE)))
                {
                    DmList names = overlay.get_global_feature_list() as DmList;
                    string name = names == null ? null : Dm.each(names).Select(n => Dm.str(n)).FirstOrDefault(n => n != SPRITE_ACCESSORY_NONE);
                    if (name != null)
                        human.dna.features[overlay.feature_key] = name;
                }
                try
                {
                    Waiting.NoSleep(made.replace_into(human));
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(organType.Name + " in: " + e.Message, e);
                }
                return made;
            });
            l.R.Step(2 * Second);
            Assert.True(l.R.Room.Runtimes.Count == 0, organType.Name + " in: " + string.Join("\n", l.R.Room.RuntimeText));
            l.R.In(() => Waiting.NoSleep(organ.Remove(human)));
            l.R.Step(2);
            l.R.In(() =>
            {
                qdel(organ);
                qdel(human);
            });
            l.R.Step(2);
            Assert.True(l.R.Room.Runtimes.Count == 0, organType.Name + " out: " + string.Join("\n", l.R.Room.RuntimeText));
        }
    }

    [Fact]
    public void The_moth_wings_burn_off_and_heal_the_lizard_tongue_hisses_the_penguin_ears_waddle()
    {
        Lab l = Room(9406);
        mob_living_carbon_human human = l.Operator;
        var wings = l.R.In(() =>
        {
            human.dna.features[FEATURE_MOTH_WINGS] = "Plain"; // a player's preference
            var made = Dm.@new<obj_item_organ_wings_moth>();
            Waiting.NoSleep(made.replace_into(human));
            return made;
        });
        l.R.Step(2);
        Assert.Same(wings, l.R.In(() => human.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS)));
        Assert.NotNull(l.R.In(() => wings.GetComponent(typeof(datum_component_jetpack)))); // the flightless wings drift in low gravity
        Assert.Null(l.R.In(() => wings.fly)); // and give no flight action
        var overlay = l.R.In(() => wings.bodypart_overlay as datum_bodypart_overlay_mutant_wings_moth);
        Assert.Equal("plain", l.R.In(() => Dm.str(overlay.get_base_icon_state())));

        // a fire hot enough burns them off: the burnt sprite and a bad mood
        l.R.In(() =>
        {
            human.bodytemperature = 900;
            Waiting.NoSleep(human.adjust_fire_stacks(10));
            human.ignite_mob();
        });
        l.R.Step(3 * Second);
        Assert.True(l.Feed.Any("Your precious wings burn to a crisp!"), Diag(l));
        Assert.Equal(1f, l.R.In(() => wings.burnt));
        Assert.Equal("burnt_off", l.R.In(() => Dm.str(overlay.get_base_icon_state())));
        Assert.Equal(FALSE, l.R.In(() => wings.can_soften_fall()));

        // a full heal grows them back
        l.R.In(() => Waiting.NoSleep(human.fully_heal(HEAL_ALL)));
        l.R.Step(2);
        Assert.Equal(0f, l.R.In(() => wings.burnt));
        Assert.Equal("plain", l.R.In(() => Dm.str(overlay.get_base_icon_state())));

        // the lizard tongue hisses its s's
        l.R.In(() => Waiting.NoSleep(Dm.@new<obj_item_organ_tongue_lizard>().replace_into(human)));
        l.R.Step(2);
        l.R.In(() => Waiting.NoSleep(human.say("yes sir")));
        l.R.Step(Second);
        Assert.True(l.Feed.Any("Yesss sssir"), Diag(l)); // say capitalizes the first letter

        // the penguin ears waddle
        var ears = l.R.In(() => Dm.@new<obj_item_organ_ears_penguin>());
        l.R.In(() => Waiting.NoSleep(ears.replace_into(human)));
        l.R.Step(2);
        Assert.True(l.Feed.Any("You suddenly feel like you've lost your balance."), Diag(l));
        Assert.True(l.R.In(() => HAS_TRAIT(human, TRAIT_WADDLING)));
        l.R.In(() => Waiting.NoSleep(ears.Remove(human)));
        l.R.Step(2);
        Assert.False(l.R.In(() => HAS_TRAIT(human, TRAIT_WADDLING)));
        Assert.True(l.Feed.Any("Your sense of balance comes back to you."), Diag(l));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_infusion_book_lists_every_obtainable_entry()
    {
        Lab l = Room(9405);
        var book = l.R.In(() => Dm.@new<obj_item_tgui_book_manual_infuser>(l.Operator.loc));
        l.R.In(() => Waiting.NoSleep(l.Operator.put_in_active_hand(book)));
        DmList data = l.R.In(() => Waiting.NoSleep(book.ui_static_data(l.Operator)) as DmList);
        DmList entries = l.R.In(() => data["entries"] as DmList);
        int obtainable = l.R.In(() => Dm.each_cast<datum_infuser_entry>(assoc_to_values(GLOB.infuser_entries) as DmList).Count(e => !Dm.eq(e.tier, DNA_MUTANT_UNOBTAINABLE)));
        Assert.Equal(obtainable, (int)l.R.In(() => entries.len));
        Assert.Contains(l.R.In(() => Dm.each(entries).Select(e => Dm.str(((DmList)e)["name"])).ToList()), n => n == "Rat");

        // the book opens its InfuserBook window in hand
        Take(l.Operator);
        l.R.In(() => Waiting.NoSleep(book.ui_interact(l.Operator)));
        l.R.Step(2);
        JsonElement payload = LastUpdate(Take(l.Operator), out _);
        Assert.Equal("InfuserBook", InterfaceOf(payload));
        l.R.AssertNoRuntimes();
    }
}

