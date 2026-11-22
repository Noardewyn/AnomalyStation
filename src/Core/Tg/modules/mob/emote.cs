// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice B (docs/ai/specs/social.md): the whole file.

public partial class mob : atom_movable
{
    ///How confused a carbon must be before they will vomit
    // tg: code/modules/mob/emote.dm:2  #define BEYBLADE_PUKE_THRESHOLD (30 SECONDS)
    internal const float BEYBLADE_PUKE_THRESHOLD = 30 * SECONDS;
    ///How must nutrition is lost when a carbon pukes
    // tg: code/modules/mob/emote.dm:4  #define BEYBLADE_PUKE_NUTRIENT_LOSS 60
    internal const float BEYBLADE_PUKE_NUTRIENT_LOSS = 60;
    ///How often a carbon becomes penalized
    // tg: code/modules/mob/emote.dm:6  #define BEYBLADE_DIZZINESS_PROBABILITY 20
    internal const float BEYBLADE_DIZZINESS_PROBABILITY = 20;
    ///How long the screenshake lasts
    // tg: code/modules/mob/emote.dm:8  #define BEYBLADE_DIZZINESS_DURATION (20 SECONDS)
    internal const float BEYBLADE_DIZZINESS_DURATION = 20 * SECONDS;
    ///How much confusion a carbon gets every time they are penalized
    // tg: code/modules/mob/emote.dm:10  #define BEYBLADE_CONFUSION_INCREMENT (10 SECONDS)
    internal const float BEYBLADE_CONFUSION_INCREMENT = 10 * SECONDS;
    ///A max for how much confusion a carbon will be for beyblading
    // tg: code/modules/mob/emote.dm:12  #define BEYBLADE_CONFUSION_LIMIT (40 SECONDS)
    internal const float BEYBLADE_CONFUSION_LIMIT = 40 * SECONDS;

    //The code execution of the emote datum is located at code/datums/emotes.dm
    // tg: code/modules/mob/emote.dm:15
    public virtual async DmTask<object> emote(object act = null, float? type_override = null, object message = null, float? intentional = null, float? force_silence = null, float? forced = null)
    {
        type_override ??= NONE;
        intentional ??= FALSE;
        force_silence ??= FALSE;
        forced ??= FALSE;
        object param = message;
        float custom_param = findtext(act, " "); // findchar(act, " "): a set of one character
        if (Dm.truthy(custom_param))
        {
            param = copytext(act, custom_param + 1); // length(act[custom_param]): one character
            act = copytext(act, 1, custom_param);
        }

        act = LOWER_TEXT(act);
        DmList key_emotes = GLOB.emote_list[act] as DmList;

        if (!Dm.truthy(length(key_emotes)))
        {
            if (Dm.truthy(intentional) && !Dm.truthy(force_silence))
                to_chat(this, span_notice(I18n.T("'[]' emote does not exist. Say *help for a list.", act)));
            return FALSE;
        }
        bool silenced = false;
        foreach (datum_emote emote in Dm.each<datum_emote>(key_emotes))
        {
            if (!Dm.truthy(await emote.check_cooldown(this, intentional)))
            {
                silenced = true;
                continue;
            }
            if (!Dm.truthy(forced) && !Dm.truthy(await emote.can_run_emote(this, TRUE, intentional, param)))
                continue;
            if (Dm.truthy(_listen_lookup?[COMSIG_MOB_PRE_EMOTED])
                && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_MOB_PRE_EMOTED, Dm.list(this, emote.key, param, type_override, intentional, emote)), COMPONENT_CANT_EMOTE)))
            {
                silenced = true;
                continue;
            }
            await emote.run_emote(this, param, type_override, intentional);
            if (Dm.truthy(_listen_lookup?[COMSIG_MOB_EMOTE]))
                _SendSignal(COMSIG_MOB_EMOTE, Dm.list(this, emote, act, type_override, message, intentional));
            if (Dm.truthy(_listen_lookup?[COMSIG_MOB_EMOTED(emote.key)]))
                _SendSignal(COMSIG_MOB_EMOTED(emote.key), Dm.list(this));
            return TRUE;
        }
        if (Dm.truthy(intentional) && !silenced && !Dm.truthy(force_silence))
            to_chat(this, span_notice(I18n.T("Unusable emote '[]'. Say *help for a list.", act)));
        return FALSE;
    }
}

// tg: code/modules/mob/emote.dm:48
public partial class datum_emote_help : datum_emote
{
    // tg: code/modules/mob/emote.dm:52
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        DmList keys = Dm.list();
        DmList message = Dm.list(I18n.T("Available emotes, you can use them with say [] \n", span_bold("\"*emote\"")));
        Dm.append(ref message, span_smallnoticeital(I18n.T("Note - emotes highlighted in blue play a sound \n\n")));

        foreach (object key in Dm.each(GLOB.emote_list))
        {
            foreach (datum_emote emote_action in Dm.each<datum_emote>(GLOB.emote_list[key]))
            {
                if (Dm.@in(emote_action.key, keys))
                    continue;
                if (Dm.truthy(await emote_action.can_run_emote(user, status_check: FALSE, intentional: TRUE)))
                    Dm.append(ref keys, emote_action.key);
            }
        }

        keys = sort_list(keys) as DmList;

        // the span formatting will mess up sorting so need to do it afterwards
        for (int i = 1; i <= keys.Count; i++)
        {
            foreach (datum_emote emote_action in Dm.each<datum_emote>(GLOB.emote_list[keys[i]]))
            {
                if (Dm.truthy(emote_action.get_sound(user as mob_living)) && Dm.truthy(emote_action.should_play_sound(user, intentional: TRUE)))
                    keys[i] = span_boldnotice(Dm.str(keys[i]));
            }
        }

        Dm.append(ref message, keys.Join(", "));
        Dm.append(ref message, ".");
        to_chat(user, boxed_message(message.Join("")));
        return dot;
    }
}

// tg: code/modules/mob/emote.dm:78
public partial class datum_emote_flip : datum_emote
{
    // tg: code/modules/mob/emote.dm:90
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        user.SpinAnimation(FLIP_EMOTE_DURATION, 1, clockwise: clockwise_spin);
        return dot;
    }

    // tg: code/modules/mob/emote.dm:94
    public override async DmTask<object> check_cooldown(mob user = null, object intentional = null)
    {
        object dot = await base.check_cooldown(user, intentional);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await can_run_emote(user, intentional: Dm.num(intentional))))
            return dot;
        if (isliving(user))
        {
            var flippy_mcgee = (mob_living)user;
            if (prob(fall_over_prob))
            {
                await flippy_mcgee.Knockdown(1 * SECONDS);
                flippy_mcgee.visible_message(
                    span_notice(I18n.T("[] attempts to do a flip and falls over, what a doofus!", flippy_mcgee)),
                    span_notice(I18n.T("You attempt to do a flip while still off balance from the last flip and fall down!")));
                if (prob(fall_over_prob / 2))
                    await flippy_mcgee.adjust_brute_loss(1);
            }
            else
            {
                flippy_mcgee.visible_message(
                    span_notice(I18n.T("[] stumbles a bit after their flip.", flippy_mcgee)),
                    span_notice(I18n.T("You stumble a bit from still being off balance from your last flip.")));
            }
        }
        return dot;
    }
}

// tg: code/modules/mob/emote.dm:121
public partial class datum_emote_spin : datum_emote
{
    // tg: code/modules/mob/emote.dm:128
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        user.spin(20, 1);
        return dot;
    }

    // tg: code/modules/mob/emote.dm:132
    public override async DmTask<object> check_cooldown(mob user = null, object intentional = null)
    {
        object dot = await base.check_cooldown(user, intentional);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await can_run_emote(user, intentional: Dm.num(intentional))))
            return dot;
        if (!iscarbon(user))
            return dot;
        var carbon_user = (mob_living_carbon)user;

        if (Dm.num(carbon_user.get_timed_status_effect_duration(typeof(datum_status_effect_confusion))) > mob.BEYBLADE_PUKE_THRESHOLD)
        {
            await carbon_user.vomit(VOMIT_CATEGORY_KNOCKDOWN, lost_nutrition: mob.BEYBLADE_PUKE_NUTRIENT_LOSS, distance: 0);
            return dot;
        }

        if (prob(mob.BEYBLADE_DIZZINESS_PROBABILITY))
        {
            to_chat(carbon_user, span_warning(I18n.T("You feel woozy from spinning.")));
            await carbon_user.set_dizzy_if_lower(mob.BEYBLADE_DIZZINESS_DURATION);
            await carbon_user.adjust_confusion_up_to(mob.BEYBLADE_CONFUSION_INCREMENT, mob.BEYBLADE_CONFUSION_LIMIT);
        }
        return dot;
    }
}

// tg: code/modules/mob/emote.dm:158
public partial class datum_emote_jump : datum_emote
{
    // tg: code/modules/mob/emote.dm:166
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);

        jump_animation(user);

        if (!isliving(user))
            return dot;
        foreach (obj_item_storage_box squeeze_box in Dm.each<obj_item_storage_box>(get_turf(user)))
            squeeze_box.flatten_box();
        return dot;
    }

    // tg: code/modules/mob/emote.dm:175
    public virtual void jump_animation(mob user = null)
    {
        object original_transform = user.transform;
        Dm.animate(user, Dm.named("transform", ((matrix)user.transform).Translate(0, 4)), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_PARALLEL));
        Dm.animate(Dm.named("transform", original_transform), Dm.named("time", 0.1f * SECONDS));
    }

    // tg: code/modules/mob/emote.dm:180
    public override object get_sound(mob_living user = null)
    {
        return Dm.resource("sound/items/weapons/thudswoosh.ogg");
    }

    // Avoids playing sounds if we're a ghost
    // tg: code/modules/mob/emote.dm:184
    public override object should_play_sound(mob user = null, float? intentional = null)
    {
        if (isliving(user))
            return base.should_play_sound(user, intentional);
        return FALSE;
    }
}
