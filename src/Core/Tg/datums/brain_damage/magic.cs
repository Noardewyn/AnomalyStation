// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/magic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the magical traumas (the lumiphobia, the poltergeist, the athaumasia and the stalking phantom with
// its heretic stargazer). /datum/brain_trauma/magic and the phantoms are var blocks only, generated (D111).

//Magical traumas, caused by spells and curses.
//Blurs the line between the victim's imagination and reality
//Unlike regular traumas this can affect the victim's body and surroundings

// tg: code/datums/brain_damage/magic.dm:9
public partial class datum_brain_trauma_magic_lumiphobia : datum_brain_trauma_magic
{
    // tg: code/datums/brain_damage/magic.dm:22
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        turf owner_turf = owner.loc as turf;
        if (!istype(owner_turf, typeof(turf)))
            return;

        if (!Dm.truthy(owner_turf.check_lumcount_above(SHADOW_SPECIES_LIGHT_THRESHOLD))) //if there's enough light, start dying
            return;

        if (damage_warning_cooldown <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            to_chat(owner, span_warning(I18n.T("<b>The light burns you!</b>")));
            damage_warning_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START, inline
        }
        await owner.take_overall_damage(burn: 1.5f * Dm.num(seconds_per_tick));
    }
}

// tg: code/datums/brain_damage/magic.dm:36
public partial class datum_brain_trauma_magic_poltergeist : datum_brain_trauma_magic
{
    // tg: code/datums/brain_damage/magic.dm:45
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (!SPT_PROB(2, seconds_per_tick))
            return;

        float most_violent = -1; //So it can pick up items with 0 throwforce if there's nothing else
        obj_item throwing = null;
        foreach (obj_item I in Dm.each<obj_item>(view(5, get_turf(owner))))
        {
            if (Dm.truthy(I.anchored))
                continue;
            if (Dm.num(I.throwforce) > most_violent)
            {
                most_violent = Dm.num(I.throwforce);
                throwing = I;
            }
        }
        if (throwing != null)
            await throwing.throw_at(owner, 8, 2);
    }
}

// tg: code/datums/brain_damage/magic.dm:61
public partial class datum_brain_trauma_magic_antimagic : datum_brain_trauma_magic
{
    // tg: code/datums/brain_damage/magic.dm:70
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_ANTIMAGIC, TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/magic.dm:74
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_ANTIMAGIC, TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/magic.dm:78
public partial class datum_brain_trauma_magic_stalker : datum_brain_trauma_magic
{
    // tg: code/datums/brain_damage/magic.dm:94
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref stalker);
        return base.Destroy(force);
    }

    // tg: code/datums/brain_damage/magic.dm:98
    public override async DmTask<object> on_gain()
    {
        create_stalker();
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/magic.dm:102
    public virtual void create_stalker()
    {
        turf stalker_source = locate(owner.x + Dm.num(pick(-12, 12)), owner.y + Dm.num(pick(-12, 12)), owner.z); //random corner
        stalker = Dm.@new(stalker_type, stalker_source, owner) as obj_effect_client_image_holder_stalker_phantom;
    }

    // tg: code/datums/brain_damage/magic.dm:106
    public override async DmTask on_lose(object silent = null)
    {
        QDEL_NULL(ref stalker);
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/magic.dm:110
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        // Dead and unconscious people are not interesting to the psychic stalker.
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return;

        // Not even nullspace will keep it at bay.
        if (stalker == null || stalker.loc == null || stalker.z != owner.z)
        {
            qdel(stalker);
            create_stalker();
        }

        if (get_dist(owner, stalker) <= 1)
        {
            playsound(owner, Dm.resource("sound/effects/magic/demon_attack1.ogg"), 50);
            owner.visible_message(span_warning(I18n.T("[] is torn apart by invisible claws!", owner)), span_userdanger(I18n.T("Ghostly claws tear your body apart!")));
            await owner.take_bodypart_damage(rand(20, 45), wound_bonus: CANT_WOUND);
        }
        else if (SPT_PROB(30, seconds_per_tick))
            await stalker.forceMove(get_step_towards(stalker, owner));
        if (get_dist(owner, stalker) <= 8)
        {
            if (!Dm.truthy(close_stalker))
            {
                sound slowbeat = Dm.sound(Dm.resource("sound/effects/health/slowbeat.ogg"), repeat: TRUE);
                owner.playsound_local(owner, slowbeat, 40, 0, channel: CHANNEL_HEARTBEAT, use_reverb: FALSE);
                close_stalker = TRUE;
            }
        }
        else
        {
            if (Dm.truthy(close_stalker))
            {
                owner.stop_sound_channel(CHANNEL_HEARTBEAT);
                close_stalker = FALSE;
            }
        }
        await base.on_life(seconds_per_tick);
    }
}
