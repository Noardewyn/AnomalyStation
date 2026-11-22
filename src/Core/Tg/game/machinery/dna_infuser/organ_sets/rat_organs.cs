// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/rat_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the rat's infused organs (sensitive night vision eyes, the hungry stomach, the small and frail heart,
// the squeaking tongue with its cheesy lines) and their set bonus, nude ventcrawling. tg registers its_on_the_mouse itself for an item
// given (rat_organs.dm:113; on_item_given, with the whimsy check, is never registered), kept as it is. The colour defines are
// file-local; the var blocks are generated (D111).

///way better night vision, super sensitive. lotta things work like this, huh?
// tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:16
public partial class obj_item_organ_eyes_night_vision_rat : obj_item_organ_eyes_night_vision
{
    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their eyes have deep, shifty black pupils, surrounded by a sickening yellow sclera."), BODY_ZONE_PRECISE_EYES));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_rat)));
        return dot;
    }
}

///increases hunger, disgust recovers quicker, expands what is defined as "food"
// tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:39
public partial class obj_item_organ_stomach_rat : obj_item_organ_stomach
{
    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_rat)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their mouth is drooling excessively."), BODY_ZONE_PRECISE_MOUTH));
        return dot;
    }
}

/// makes you smaller, walk over tables, and take 1.5x damage
// tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:57
public partial class obj_item_organ_heart_rat : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_rat)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_They hunch%PRONOUN_es over unnaturally!")));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:74
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        receiver.damage_resistance -= 50; //but 1.5 damage
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:78
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon heartless = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        heartless.damage_resistance += 50; //revert damage resistance
    }
}

/// you occasionally squeak, and have some rat related verbal tics
// tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:83
public partial class obj_item_organ_tongue_rat : obj_item_organ_tongue
{
    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their teeth are oddly shaped and yellowing."), BODY_ZONE_PRECISE_MOUTH));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_rat)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:102
    public virtual bool whimsy_check(mob_living checking = null)
    {
        if (Dm.truthy(check_holidays(APRIL_FOOLS)))
            return true;
        // HAS_PERSONALITY(checking, /datum/personality/whimsical) (__DEFINES/mood.dm:109: LAZYACCESS(mob.personalities, ...))
        if (Dm.truthy(LAZYACCESS(checking.personalities, typeof(datum_personality_whimsical))))
            return true;
        if (prob(1))
            return true;
        return false;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:111
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        object dot = base.modify_speech(source, speech_args);
        if (!whimsy_check(source as mob_living))
            return dot;
        string message = LOWER_TEXT(speech_args[SPEECH_MESSAGE]);
        if (message == "hi" || message == "hi.")
            speech_args[SPEECH_MESSAGE] = I18n.T("Cheesed to meet you!");
        if (message == "hi?")
            speech_args[SPEECH_MESSAGE] = I18n.T("Um... cheesed to meet you?");
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:121
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon tongue_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(tongue_owner, COMSIG_LIVING_ITEM_GIVEN, nameof(its_on_the_mouse));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:125
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon tongue_owner = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(tongue_owner, COMSIG_LIVING_ITEM_GIVEN);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:129
    [SignalHandler]
    public virtual void on_item_given(mob_living_carbon offerer = null, mob_living taker = null, obj_item given = null)
    {
        if (!whimsy_check(offerer))
            return;
        Waiting.Detach(its_on_the_mouse(offerer, taker)); // INVOKE_ASYNC
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:135
    public virtual async DmTask its_on_the_mouse(mob_living_carbon offerer = null, mob_living taker = null)
    {
        await offerer.say(I18n.T("For you, it's on the mouse."));
        await taker.add_mood_event("it_was_on_the_mouse", typeof(datum_mood_event_it_was_on_the_mouse));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/rat_organs.dm:139
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (prob(5))
        {
            await owner.emote("squeaks");
            playsound(owner, "sound/mobs/non-humanoids/mouse/mousesqueek.ogg", 100);
        }
    }
}
