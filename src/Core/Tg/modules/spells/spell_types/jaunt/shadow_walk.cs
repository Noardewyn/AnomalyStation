// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/jaunt/shadow_walk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the nightmare's Shadow Walk, a jaunt that only lasts while the ground is dark. The var blocks are
// generated (D111).

// tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:2
public partial class datum_action_cooldown_spell_jaunt_shadow_walk : datum_action_cooldown_spell_jaunt
{
    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:15
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        RegisterSignal(grant_to, COMSIG_MOVABLE_MOVED, nameof(update_status_on_signal));
        return dot;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:19
    public override async DmTask Remove(mob remove_from = null)
    {
        await base.Remove(remove_from);
        UnregisterSignal(remove_from, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:23
    public override async DmTask<object> enter_jaunt(mob_living jaunter = null, turf loc_override = null)
    {
        obj_effect_dummy_phased_mob_shadow shadow = await base.enter_jaunt(jaunter, loc_override) as obj_effect_dummy_phased_mob_shadow;
        if (shadow != null)
            shadow.light_max = light_threshold;
        return shadow;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:29
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (istype(((atom)owner).loc, typeof(obj_effect_dummy_phased_mob))) // is_jaunting
            return TRUE;
        turf cast_turf = get_turf(owner);
        if (!Dm.truthy(cast_turf.check_lumcount_below(light_threshold)))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("It isn't dark enough here!")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:42
    public override async DmTask<object> cast(atom cast_on_atom = null)
    {
        mob_living cast_on = (mob_living)cast_on_atom;
        object dot = await base.cast(cast_on);
        if (istype(cast_on.loc, typeof(obj_effect_dummy_phased_mob))) // is_jaunting
        {
            await exit_jaunt(cast_on);
            return dot;
        }

        playsound(get_turf(owner), Dm.resource("sound/effects/nightmare_poof.ogg"), 50, TRUE, -1, ignore_walls: FALSE);
        cast_on.visible_message(span_boldwarning(I18n.T("[] melts into the shadows!", cast_on)));
        await cast_on.SetAllImmobility(0);
        await cast_on.set_stamina_loss(0, FALSE);
        await enter_jaunt(cast_on);
        return dot;
    }
}

// tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:55
public partial class obj_effect_dummy_phased_mob_shadow : obj_effect_dummy_phased_mob
{
    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:70
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:74
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        turf T = get_turf(this);
        if (jaunter == null || jaunter.loc != this)
        {
            qdel(this);
            return null;
        }

        if (Dm.truthy(check_light_level(T)))
            Waiting.NoSleep(eject_jaunter(TRUE));

        if (!QDELETED(jaunter) && isliving(jaunter)) //heal in the dark
        {
            mob_living living_jaunter = (mob_living)jaunter;
            Waiting.NoSleep(living_jaunter.heal_overall_damage(brute: healing_rate * Dm.num(seconds_per_tick), burn: healing_rate * Dm.num(seconds_per_tick), required_bodytype: BODYTYPE_ORGANIC));
        }
        return null;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:88
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        atom oldloc = (atom)loc;
        object dot = await base.relaymove(user, direction);
        if (loc != oldloc)
        {
            if (Dm.truthy(check_light_level(loc)))
                await eject_jaunter(TRUE);
        }
        return dot;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:95
    public override async DmTask<object> phased_check(mob_living user = null, object direction = null)
    {
        object dot = await base.phased_check(user, direction);
        if (Dm.truthy(dot) && isspaceturf(dot))
        {
            to_chat(user, span_warning(I18n.T("It really would not be wise to go into space.")));
            return FALSE;
        }
        if (Dm.truthy(check_light_level(dot as atom)))
        {
            if (!light_step_warning())
                return FALSE;
        }
        return dot;
    }

    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:104
    public override async DmTask eject_jaunter(params object[] __extra)
    {
        object forced_out = Dm.arg(__extra, 0) ?? FALSE;
        turf reveal_turf = get_turf(this);

        if (reveal_turf != null)
        {
            if (Dm.truthy(forced_out))
                reveal_turf.visible_message(span_boldwarning(I18n.T("[] is revealed by the light!", jaunter)));
            else
                reveal_turf.visible_message(span_boldwarning(I18n.T("[] emerges from the darkness!", jaunter)));
            playsound(reveal_turf, Dm.resource("sound/effects/nightmare_reappear.ogg"), 50, TRUE, -1, ignore_walls: FALSE);
        }

        await base.eject_jaunter(__extra);
    }

    /**
     * Checks the light level. If above the minimum acceptable amount (0.2), returns TRUE.
     *
     * Checks the light level of a given location to see if it is too bright to
     * continue a jaunt in. Returns FALSE if it's acceptably dark, and TRUE if it is too bright.
     *
     * * location_to_check - The location to have its light level checked.
     */
    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:126
    public virtual object check_light_level(atom location_to_check = null)
    {
        turf light_turf = get_turf(location_to_check);
        return light_turf.check_lumcount_above(light_max); // jaunt ends on TRUE
    }

    /**
     * Checks if the user should receive a warning that they're moving into light.
     *
     * Checks the cooldown for the warning message on moving into the light.
     * If the message has been displayed, and the cooldown (delay period) is complete, returns TRUE.
     */
    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:139
    public virtual bool light_step_warning()
    {
        if (!Dm.truthy(light_alert_given)) //Give the user a warning that they're leaving the darkness
        {
            ((atom)this).balloon_alert((mob)jaunter, I18n.T("leaving the shadows..."));
            light_alert_given = TRUE;
            light_step_cooldown = world.time + 0.75f * SECONDS; // COOLDOWN_START
            _addtimer(Dm.@new<datum_callback>(this, nameof(reactivate_light_alert)), 1 * SECONDS, file: "code/modules/spells/spell_types/jaunt/shadow_walk.dm", line: 143); //You get a .5 second window to bypass the warning before it comes back
            return false;
        }

        if (!(light_step_cooldown <= world.time)) // COOLDOWN_FINISHED
            return false;

        light_alert_given = FALSE;
        return true; //Our jaunter is ignoring the warning, so we proceed
    }

    /**
     * Sets light_alert_given to false.
     *
     * Sets light_alert_given to false, making the light alert pop up and intercept movement once again.
     * Added in its own proc to reset the alert without having to call light_step_warning().
     */
    // tg: code/modules/spells/spell_types/jaunt/shadow_walk.dm:155
    public virtual void reactivate_light_alert()
    {
        light_alert_given = FALSE;
    }
}
