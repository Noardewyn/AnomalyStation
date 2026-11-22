// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/generic_positive_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111); saved_life keeps the [DmPath] of its M2 skeleton.

// tg: code/datums/mood_events/generic_positive_events.dm:1
public partial class datum_mood_event_hug : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:6
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_aloof))))
        {
            mood_change = -1;
            description = I18n.T("I would prefer not to be touched.");
            return;
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous))))
        {
            mood_change = 0;
            description = I18n.T("I hate hugs.");
            return;
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:16
public partial class datum_mood_event_bear_hug : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:21
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_aloof)))
            || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous)))
        )
        {
            mood_change = -2;
            description = I18n.T("They're squeezing me too tightly!");
            return;
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:29
public partial class datum_mood_event_betterhug : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:34
    [DmParams("friend")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob friend = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_aloof))))
        {
            mood_change = 1;
            description = I18n.T("[] is nice, but I wish they'd stop touching me.", friend.name);
            return;
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous))))
        {
            mood_change = 0;
            description = I18n.T("[] is way too nice for this station.", friend.name);
            return;
        }

        description = I18n.T("[] was very nice to me.", friend.name);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:46
public partial class datum_mood_event_besthug : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:51
    [DmParams("friend")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob friend = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_aloof))))
        {
            mood_change = 2;
            description = I18n.T("[] is great to be around, but I wish they'd stop touching me.", friend.name);
            return;
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous))))
        {
            mood_change = 0;
            description = I18n.T("[] is way too nice for this station.", friend.name);
            return;
        }

        description = I18n.T("[] is great to be around, [] make[] me feel so happy!", friend.name, friend.p_they(), friend.p_s());
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:63
public partial class datum_mood_event_warmhug : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:68
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_aloof))))
        {
            mood_change = 0;
            description = I18n.T("I would prefer not to be touched, but the warmth is nice....");
            return;
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous))))
        {
            mood_change = 0;
            description = I18n.T("I hate hugs. I can warm myself up.");
            return;
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:78
public partial class datum_mood_event_tailpulled : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:83
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_aloof)))
            || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous)))
        )
        {
            mood_change = -2;
            description = I18n.T("Who the hell is touching my tail?");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:90
public partial class datum_mood_event_arcade : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:96
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_industrious))) || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_slacking_diligent))))
        {
            mood_change = -1;
            description = I18n.T("Wow, I beat the game. I could've been doing anything productive instead.");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:107
public partial class datum_mood_event_maintenance_adaptation : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:110
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        description = I18n.T("[] has helped me adapt to the maintenance shafts!", GLOB.deity);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:113
public partial class datum_mood_event_book_nerd : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:118
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_erudite))))
        {
            mood_change = 2;
            description = I18n.T("I love reading books!");
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_uneducated))))
        {
            mood_change = -1;
            description = I18n.T("Who cares about books?");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:126
public partial class datum_mood_event_exercise : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:130
    [DmParams("fitness_level")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object fitness_level = Dm.arg(__extra, 0);
        mood_change = Dm.num(fitness_level); // the more fit you are, the more you like to work out
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_slacking_lazy))))
        {
            mood_change *= -0.5f;
            description = I18n.T("Working out, what a chore!");
        }
        else if (!Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_athletic))))
        {
            mood_change *= 0.5f;
            description = I18n.T("Working out is a bit of a chore, but it is pretty fulfilling.");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:139
public partial class datum_mood_event_pet_animal : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:145
    [DmParams("animal")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob animal = Dm.arg<mob>(__extra, 0);
        animal_type = animal?.type;

        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_animal_disliker))))
        {
            mood_change = -1;
            description = I18n.T("Ewww, [] is so dirty! I don't want to touch it!", animal);
            return;
        }

        object dog_fan = HAS_PERSONALITY(owner, typeof(datum_personality_dog_fan));
        bool isdog = istype(animal, typeof(mob_living_basic_pet_dog));
        object cat_fan = HAS_PERSONALITY(owner, typeof(datum_personality_cat_fan));
        bool iscat = istype(animal, typeof(mob_living_basic_pet_cat));
        if ((Dm.truthy(dog_fan) && isdog) || (Dm.truthy(cat_fan) && iscat) || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_animal_friend))))
        {
            mood_change = 3;
            description = I18n.T("I love [] so much, [] so adorable! I can't stop petting []!", animal, animal.p_theyre(), animal.p_them());
            return;
        }
        if (Dm.truthy(dog_fan) && iscat)
        {
            mood_change = -1;
            description = I18n.T("I don't like []! I prefer dogs!", animal);
            return;
        }
        if (Dm.truthy(cat_fan) && isdog)
        {
            mood_change = -1;
            description = I18n.T("I don't like []! I prefer cats!", animal);
            return;
        }

        mood_change = 1;
        description = I18n.T("[] is adorable!", animal);
    }

    // Change the moodlet if we get refreshed (we may have pet another type of animal)
    // tg: code/datums/mood_events/generic_positive_events.dm:174
    [DmParams("home", "animal")]
    public override object be_refreshed(datum_mood home = null, params object[] __extra)
    {
        mob animal = Dm.arg<mob>(__extra, 0);
        object dot = null;
        dot = base.be_refreshed(home, __extra);
        if (Dm.eq(animal_type, animal?.type))
            return dot;
        Waiting.NoSleep(add_effects(animal));
        return dot;
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:188
[DmPath("/datum/mood_event/saved_life")]
public partial class datum_mood_event_saved_life : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:193
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous))))
        {
            mood_change = 0;
            description = I18n.T("I don't care much for saving lives.");
        }
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_misanthropic))))
        {
            mood_change = -1;
            description = I18n.T("Saving lives is a waste of time.");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:304
public partial class datum_mood_event_sacrifice_good : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:310
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(owner.mind?.holy_role) && Dm.truthy(GLOB.deity))
            description = I18n.T("[] is pleased with this offering!", GLOB.deity);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:337
public partial class datum_mood_event_hope_lavaland : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:341
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_pessimistic))))
        {
            mood_change = 0;
            description = I18n.T("This emblem is a lie. There is no hope for me.");
            return;
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:361
public partial class datum_mood_event_helped_up : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:365
    public override object can_effect_mob(datum_mood home = null, mob_living who = null, params object[] __extra)
    {
        // tg reads owner here, before on_add sets it: the null read is a runtime in tg, so the moodlet never lands. Kept, without
        // the runtime (DV1 would unwind the helper's whole click).
        if (owner == null)
            return FALSE;
        if (!Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_compassionate)))
            && !Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous)))
            && !Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_misanthropic))))
            return FALSE;

        return base.can_effect_mob(home, who, __extra);
    }

    // tg: code/datums/mood_events/generic_positive_events.dm:373
    [DmParams("other_person", "helper")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob other_person = Dm.arg<mob>(__extra, 0);
        object helper = Dm.arg(__extra, 1);
        if (!Dm.truthy(other_person))
            return;

        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_callous))) || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_misanthropic))))
        {
            mood_change = -2;
            if (Dm.truthy(helper))
                description = I18n.T("They should have helped themselves.");
            else
                description = I18n.T("I could've gotten up myself.");
        }

        else if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_compassionate))))
        {
            mood_change = 2;
            if (Dm.truthy(helper))
                description = I18n.T("Helping [] up felt good!", other_person);
            else
                description = I18n.T("[] helped me up, how nice of []!", other_person, other_person.p_them());
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:412
public partial class datum_mood_event_kiss : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:417
    [DmParams("beau", "direct")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob beau = Dm.arg<mob>(__extra, 0);
        object direct = Dm.arg(__extra, 1);
        if (!Dm.truthy(beau))
            return;
        if (Dm.truthy(direct))
            description = I18n.T("[] gave me a kiss, ahh!!", beau.name);
        else
            description = I18n.T("[] blew a kiss at me, I must be a real catch!", beau.name);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:439
public partial class datum_mood_event_observed_soda_spill : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:445
    [DmParams("spilled_mob", "soda_can")]
    public override async DmTask add_effects(params object[] __extra)
    {
        mob spilled_mob = Dm.arg<mob>(__extra, 0);
        atom soda_can = Dm.arg<atom>(__extra, 1);
        if (!Dm.truthy(spilled_mob))
            return;

        description = I18n.T("Ahaha! [] spilled [] [] all over []self! Classic.", spilled_mob, spilled_mob.p_their(), Dm.truthy(soda_can) ? soda_can.name : I18n.T("soda"), spilled_mob.p_them());
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:451
public partial class datum_mood_event_gaming : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:457
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_industrious))) || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_slacking_diligent))))
        {
            mood_change = -1;
            description = I18n.T("Is now really the time to be playing games? I should be working.");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:468
public partial class datum_mood_event_love_reagent : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:472
    [DmParams("duration")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_pessimistic))))
        {
            mood_change = 0;
            description = I18n.T("This food is okay, I guess.");
            return;
        }

        if (isnum(duration))
            timeout = Dm.num(duration);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:487
public partial class datum_mood_event_playing_cards : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:493
    [DmParams("param")]
    public override async DmTask add_effects(params object[] __extra)
    {
        float card_players = 1;
        foreach (mob_living_carbon player in Dm.each<mob_living_carbon>(viewers(COMBAT_MESSAGE_RANGE, owner)))
        {
            // player.is_holding(/obj/item/toy/singlecard) with a typepath: held_items.Find of the path (inventory.dm:109), never a
            // match in tg (kept)
            object or__1 = player.held_items.Find(typeof(obj_item_toy_singlecard));
            object player_has_cards = Dm.truthy(or__1) ? or__1 : player.is_holding_item_of_type(typeof(obj_item_toy_cards));
            if (Dm.truthy(player_has_cards))
            {
                card_players++;
                if (card_players > 5)
                    break;
            }
        }

        mood_change *= card_players;
        await base.add_effects(__extra);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:509
public partial class datum_mood_event_russian_roulette_win : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:514
    [DmParams("loaded_rounds")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object loaded_rounds = Dm.arg(__extra, 0);
        float @base = Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_gambler))) ? 2 : 1.8f;
        mood_change = round(Dm.num(Dm.pow(@base, loaded_rounds)), 1);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:523
public partial class datum_mood_event_fish_released : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:528
    [DmParams("morbid", "fish")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object morbid = Dm.arg(__extra, 0);
        obj_item_fish fish = Dm.arg<obj_item_fish>(__extra, 1);
        if (!Dm.truthy(morbid))
        {
            description = I18n.T("Go, [], swim and be free!", fish.name);
            return;
        }
        if (fish.status == FISH_DEAD)
            description = I18n.T("Some scavenger will surely find a use for the remains of []. How pragmatic.", fish.name);
        else
            description = I18n.T("Returned to the burden of the deep. But is this truly a mercy, []? There will always be bigger fish...", fish.name);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:537
public partial class datum_mood_event_fish_petting : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:543
    [DmParams("fish", "morbid")]
    public override async DmTask add_effects(params object[] __extra)
    {
        obj_item_fish fish = Dm.arg<obj_item_fish>(__extra, 0);
        object morbid = Dm.arg(__extra, 1);
        if (!Dm.truthy(morbid))
            description = I18n.T("It felt nice to pet \\the [].", fish);
        else
            description = I18n.T("I caress \\the [] as [] squirm[] under my touch, blissfully unaware of how cruel this world is.", fish, fish.p_they(), fish.p_s());
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:597
public partial class datum_mood_event_slots : datum_mood_event
{
    // tg: code/datums/mood_events/generic_positive_events.dm:603
    [DmParams()]
    public override async DmTask add_effects(params object[] __extra)
    {
        if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_gambler))))
            mood_change *= 2;
        else if (Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_industrious))) || Dm.truthy(HAS_PERSONALITY(owner, typeof(datum_personality_slacking_diligent))))
        {
            mood_change *= -1;
            description = I18n.T("Why am I gambling my time and money away?");
        }
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:611
public partial class datum_mood_event_slots_win : datum_mood_event_slots
{
    // tg: code/datums/mood_events/generic_positive_events.dm:617
    public override object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        if (istype(new_event, typeof(datum_mood_event_slots_all_gone)))
            return ALLOW_NEW_MOOD;
        if (new_event.mood_change < mood_change)
            return BLOCK_NEW_MOOD;
        return base.be_replaced(home, new_event, __extra);
    }
}

// tg: code/datums/mood_events/generic_positive_events.dm:632
public partial class datum_mood_event_slots_all_gone : datum_mood_event_slots
{
    // tg: code/datums/mood_events/generic_positive_events.dm:637
    public override object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        if (istype(new_event, typeof(datum_mood_event_slots_win_jackpot)))
            return ALLOW_NEW_MOOD;
        description = I18n.T("I'll never get it back...");
        return BLOCK_NEW_MOOD;
    }
}
