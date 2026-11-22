// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * Toggleable action that makes the surrounding area dirtier and spawns miasma
 */
// tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:5
public partial class datum_action_cooldown_mob_cooldown_domain : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:23
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(Dm.bitand(owner.movement_type, VENTCRAWLING)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "can't use while ventcrawling!");
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:32
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(10 * SECONDS);
        await set_domain_active(!Dm.truthy(is_active) ? TRUE : FALSE);
        StartCooldown();
        return null;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:37
    public override async DmTask Remove(mob removed_from = null)
    {
        await set_domain_active(FALSE);
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:41
    public override void update_status_on_signal(datum source = null, object new_stat = null, object old_stat = null)
    {
        base.update_status_on_signal(source, new_stat, old_stat);
        if (!Dm.truthy(Waiting.NoSleep(IsAvailable())))
            Waiting.NoSleep(set_domain_active(FALSE));
    }

    /// Enable or disable the ability
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:47
    public virtual async DmTask set_domain_active(object should_active = null)
    {
        if (Dm.eq(is_active, should_active) || owner == null)
            return;
        is_active = Dm.num(should_active);
        if (Dm.truthy(is_active))
        {
            RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_MOVE_VENTCRAWLING), nameof(cancel_on_signal));
            button_icon_state = "coffer";
            await spread_domain();
        }
        else
        {
            UnregisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_MOVE_VENTCRAWLING));
            button_icon_state = "coffer_off";
        }
        await build_all_button_icons(update_flags: UPDATE_BUTTON_ICON);
    }

    /// Stop spreading shit when one of these events happens
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:63
    [SignalHandler]
    public virtual void cancel_on_signal()
    {
        Waiting.NoSleep(set_domain_active(FALSE));
    }

    /// Create gas and spawn mess
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:68
    public virtual async DmTask spread_domain()
    {
        if (!Dm.truthy(is_active) || !(mess_cooldown <= world.time) || owner == null)
            return;
        turf our_location = get_turf(owner) as turf;
        our_location.atmos_spawn_air(Dm.text("[]=4;[]", GAS_MIASMA, TURF_TEMPERATURE(T20C)));
        DmList available_spots = Dm.list(our_location);
        Dm.combine(ref available_spots, get_adjacent_open_turfs(owner));
        turf mess_location = pick(available_spots) as turf;
        float roll = rand(1, 10);
        if (roll == 8)
            Dm.@new<obj_effect_decal_cleanable_vomit>(mess_location);
        else if (roll == 9)
            Dm.@new<obj_effect_decal_cleanable_vomit_old>(mess_location);
        else if (roll == 10)
            Dm.@new<obj_effect_decal_cleanable_blood_oil_slippery>(mess_location);
        else
            Dm.@new<obj_effect_decal_cleanable_dirt>(mess_location);
        mess_cooldown = world.time + mess_interval; // We use a cooldown AND timer because of the toggle
        _addtimer(Dm.@new<datum_callback>(this, nameof(spread_domain)), mess_interval, TIMER_DELETE_ME, file: "code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm", line: 88);
    }
}

/**
 * Action that raises the rats and other pests around the rat.
 */
// tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:95
public partial class datum_action_cooldown_mob_cooldown_riot : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:109
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(Dm.bitand(owner.movement_type, VENTCRAWLING)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "can't use while ventcrawling!");
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:118
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(10 * SECONDS);
        riot();
        StartCooldown();
        return null;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:129
    public virtual void riot()
    {
        playsound(owner, Dm.resource("sound/mobs/non-humanoids/mouse/mousesqueek.ogg"), vol: 150, frequency: 10000);
        Dm.@new<obj_effect_temp_visual_circle_wave_brown>(get_turf(owner));
        foreach (mob_living possible_minion in Dm.each<mob_living>(oview(owner, range)))
            if (Dm.truthy(possible_minion._listen_lookup?[COMSIG_REGAL_RAT_RIOTED]))
                possible_minion._SendSignal(COMSIG_REGAL_RAT_RIOTED, Dm.list(possible_minion, owner));
    }
}

/**
 * Spit reagent used by regal rats to poison food or anything else with a reagent holder
 */
// tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:159
public partial class datum_reagent_rat_spit : datum_reagent
{
    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:167
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        if (HAS_TRAIT(affected_mob, TRAIT_AGEUSIA))
            return;
        to_chat(affected_mob, span_notice("This food has a funny taste!"));
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:173
    public override async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.overdose_start(affected_mob, metabolization_ratio);
        if (affected_mob is mob_living_carbon victim && !Dm.truthy(victim.has_faction(FACTION_RAT)))
        {
            to_chat(victim, span_userdanger("With this last sip, you feel your body convulsing horribly from the contents you've ingested. As you contemplate your actions, you sense an awakened kinship with rat-kind and their newly risen leader!"));
            victim.add_faction(FACTION_RAT);
            await victim.vomit(VOMIT_CATEGORY_DEFAULT);
        }
        metabolization_rate = 10 * REAGENTS_METABOLISM;
    }

    // tg: code/modules/mob/living/basic/space_fauna/regal_rat/regal_rat_actions.dm:182
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        mob_living_carbon carbon_mob = affected_mob as mob_living_carbon;
        if (prob(15))
        {
            to_chat(carbon_mob, span_notice("You feel queasy!"));
            carbon_mob.adjust_disgust(3);
        }
        else if (prob(10))
        {
            to_chat(carbon_mob, span_warning("That food does not sit up well!"));
            carbon_mob.adjust_disgust(5);
        }
        else if (prob(5))
            await carbon_mob.vomit(VOMIT_CATEGORY_DEFAULT);
        return dot;
    }
}
