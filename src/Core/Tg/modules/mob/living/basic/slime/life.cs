// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

public partial class mob_living_basic_slime
{
    // tg: code/modules/mob/living/basic/slime/life.dm:1
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead or deleted
            return dot;

        if (!HAS_TRAIT(this, TRAIT_STASIS)) //No hunger in stasis
            handle_nutrition(seconds_per_tick);

        handle_slime_stasis();
        return dot;
    }

    ///Handles if a slime's environment would cause it to enter stasis. Stops processing if it is in stasis
    // tg: code/modules/mob/living/basic/slime/life.dm:11
    public override async DmTask handle_environment(datum_gas_mixture environment = null, object seconds_per_tick = null, params object[] __extra)
    {
        await base.handle_environment(environment, seconds_per_tick, __extra);
        if (Dm.num(bodytemperature) <= (T0C - 40)) // stun temperature
            await apply_status_effect(typeof(datum_status_effect_freon), SLIME_COLD);
        else
            remove_status_effect(typeof(datum_status_effect_freon), SLIME_COLD);
    }

    // tg: code/modules/mob/living/basic/slime/life.dm:18
    public virtual void handle_slime_stasis()
    {
        datum_gas_mixture environment = Waiting.NoSleep(loc.return_air()) as datum_gas_mixture;
        float bz_percentage = Dm.num(environment.moles[typeof(datum_gas_bz)]) / Dm.num(environment.total_moles());

        if (bz_percentage >= 0.05f && Dm.num(bodytemperature) < (T0C + 100)) //Check if we should be in stasis
        {
            if (!Dm.truthy(has_status_effect(typeof(datum_status_effect_grouped_stasis)))) //Check if we don't have the status effect yet
            {
                to_chat(this, span_danger("Nerve gas in the air has put you in stasis!"));
                Waiting.NoSleep(apply_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_SLIME_BZ));
                adjust_power_level(-SLIME_MAX_POWER);
                (ai_controller as datum_ai_controller)?.clear_blackboard_key(BB_SLIME_RABID);
            }
        }
        else if (Dm.truthy(has_status_effect(typeof(datum_status_effect_grouped_stasis)))) //Check if we still have the status effect
        {
            to_chat(this, span_notice("You wake up from the stasis."));
            remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_SLIME_BZ);
        }
    }

    ///Handles the slime's nutrition level, and its passive growth
    // tg: code/modules/mob/living/basic/slime/life.dm:37
    public virtual void handle_nutrition(object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick ?? SSMOBS_DT());
        if (Dm.truthy(hunger_disabled)) //God as my witness, I will never go hungry again
        {
            Waiting.NoSleep(set_nutrition(100));
            return;
        }

        datum_ai_controller controller = ai_controller as datum_ai_controller;
        if (SPT_PROB(1.25f, spt))
            Waiting.NoSleep(adjust_nutrition((Dm.eq(life_stage, SLIME_LIFE_STAGE_ADULT) ? -1 : -0.5f) * spt));

        if (Dm.num(nutrition) < SLIME_STARVE_NUTRITION)
        {
            controller?.set_blackboard_key(BB_SLIME_HUNGER_LEVEL, SLIME_HUNGER_STARVING);

            if (SPT_PROB(0.5f, spt) && Dm.truthy(LAZYLEN(controller?.blackboard[BB_FRIENDS_LIST])))
            {
                object your_fault = pick(controller.blackboard[BB_FRIENDS_LIST] as DmList);
                unfriend(your_fault as mob_living);
            }
        }
        else if (Dm.num(nutrition) < SLIME_HUNGER_NUTRITION || (Dm.num(nutrition) < SLIME_GROW_NUTRITION && SPT_PROB(25, spt)))
            controller?.set_blackboard_key(BB_SLIME_HUNGER_LEVEL, SLIME_HUNGER_HUNGRY);
        else
            controller?.set_blackboard_key(BB_SLIME_HUNGER_LEVEL, SLIME_HUNGER_NONE);

        if (Dm.num(nutrition) == 0) //adjust nutrition ensures it can't go below 0
        {
            if (SPT_PROB(50, spt))
                Waiting.NoSleep(adjust_brute_loss(rand(0, 5)));
            return;
        }

        if (SLIME_GROW_NUTRITION <= Dm.num(nutrition))
        {
            if (amount_grown < SLIME_EVOLUTION_THRESHOLD)
            {
                Waiting.NoSleep(adjust_nutrition(-2.5f * spt));
                amount_grown++;
            }

            if (powerlevel < SLIME_MAX_POWER && SPT_PROB(30 - powerlevel * 2, spt))
                adjust_power_level(1);
        }
        else if (powerlevel < SLIME_MEDIUM_POWER && SLIME_HUNGER_NUTRITION <= Dm.num(nutrition) && SPT_PROB(25 - powerlevel * 5, spt))
            adjust_power_level(1);

        Waiting.NoSleep(update_mob_action_buttons());
    }

    ///Adjusts the slime's power level
    // tg: code/modules/mob/living/basic/slime/life.dm:75
    public virtual void adjust_power_level(object to_adjust = null)
    {
        powerlevel = Dm.clamp(powerlevel + Dm.num(to_adjust), SLIME_MIN_POWER, SLIME_MAX_POWER);
        // The power display (screen_objects.dm:1359) is the slime's own HUD: a player slime's, a row.
    }
}
