// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The procs of the file (M9 slice B, docs/ai/specs/social.md; its emotes' vars are generated), those of the emotes that put a hand item in
// the hand (circle, noogie, slap, hand, shoesteal) since M9 slice F, with the hand items and the offering.

// tg: code/modules/mob/living/carbon/emote.dm:10
public partial class datum_emote_living_carbon_clap : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:27
    public override object should_play_sound(mob user = null, float? intentional = null)
    {
        intentional ??= FALSE;
        var living_user = (mob_living)user;
        if (!Dm.truthy(living_user.get_bodypart(BODY_ZONE_L_ARM)) || !Dm.truthy(living_user.get_bodypart(BODY_ZONE_R_ARM)))
            return FALSE;
        return base.should_play_sound(user, intentional);
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:32
public partial class datum_emote_living_carbon_crack : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:40
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        if (!iscarbon(user) || ((mob_living_carbon)user).usable_hands < 2)
            return FALSE;
        return await base.can_run_emote(user, status_check, intentional, @params);
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:45
public partial class datum_emote_living_carbon_cry : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:67
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (!ishuman(user))
            return dot;
        var human_user = (mob_living_carbon_human)user;
        // QDEL_IN (__DEFINES/qdel.dm:60, it expands addtimer: inline, rules 11.1 (f))
        object overlay = await human_user.give_emote_overlay(typeof(datum_bodypart_overlay_simple_emote_cry));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 12.8f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(overlay as datum) : overlay), 12.8f * SECONDS, file: "code/modules/mob/living/carbon/emote.dm", line: 72);
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:132
public partial class datum_emote_living_carbon_sign : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:139
    public override object select_param(mob user = null, object @params = null)
    {
        object dot = base.select_param(user, @params);
        if (!isnum(text2num(@params)))
            return message;
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:74
public partial class datum_emote_living_carbon_circle : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:79
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (!Dm.truthy(length(user.get_empty_held_indexes())))
        {
            to_chat(user, span_warning(I18n.T("You don't have any free hands to make a circle with.")));
            return dot;
        }
        var N = Dm.@new<obj_item_hand_item_circlegame>(user);
        if (Dm.truthy(await user.put_in_hands(N)))
            to_chat(user, span_notice(I18n.T("You make a circle with your hand.")));
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:104
public partial class datum_emote_living_carbon_noogie : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:109
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        var noogie = Dm.@new<obj_item_hand_item_noogie>(user);
        if (Dm.truthy(await user.put_in_hands(noogie)))
            to_chat(user, span_notice(I18n.T("You ready your noogie'ing hand.")));
        else
        {
            qdel(noogie);
            to_chat(user, span_warning(I18n.T("You're incapable of noogie'ing in your current state.")));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:151
public partial class datum_emote_living_carbon_slap : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:157
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        var N = Dm.@new<obj_item_hand_item_slapper>(user);
        if (Dm.truthy(await user.put_in_hands(N)))
            to_chat(user, span_notice(I18n.T("You ready your slapping hand.")));
        else
        {
            qdel(N);
            to_chat(user, span_warning(I18n.T("You're incapable of slapping in your current state.")));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:167
public partial class datum_emote_living_carbon_hand : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:173
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        var hand = Dm.@new<obj_item_hand_item_hand>(user);
        if (Dm.truthy(await user.put_in_hands(hand)))
            to_chat(user, span_notice(I18n.T("You ready your hand.")));
        else
        {
            qdel(hand);
            to_chat(user, span_warning(I18n.T("You're incapable of using your hand in your current state.")));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/emote.dm:198
public partial class datum_emote_living_carbon_shoesteal : datum_emote_living_carbon
{
    // tg: code/modules/mob/living/carbon/emote.dm:204
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        var stealing_hand = Dm.@new<obj_item_hand_item_stealer>(user);
        if (Dm.truthy(await user.put_in_hands(stealing_hand)))
            user.balloon_alert(user, I18n.T("preparing to steal shoes..."));
        else
        {
            qdel(stealing_hand);
            user.balloon_alert(user, I18n.T("you can't steal shoes!"));
        }
        return dot;
    }
}
