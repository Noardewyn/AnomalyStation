// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/fire_breath.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/datums/actions/mobs/fire_breath.dm:1
public partial class datum_action_cooldown_mob_cooldown_fire_breath : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/fire_breath.dm:22
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await attack_sequence(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    /// Apply our specific fire breathing shape, in proc form so we can override it in subtypes
    // tg: code/datums/actions/mobs/fire_breath.dm:30
    public virtual async DmTask attack_sequence(atom target = null)
    {
        playsound(owner.loc, fire_sound, 200, TRUE);
        fire_line(target);
    }

    /// Breathe fire in a line towards the target, optionally rotated at an offset from the target
    // tg: code/datums/actions/mobs/fire_breath.dm:35
    public virtual void fire_line(atom target = null, object offset = null)
    {
        if (target == null)
            return;
        turf target_turf = get_ranged_target_turf_direct(owner, target, fire_range, offset) as turf;
        DmList turfs = Dm.sub(get_line(owner, target_turf), get_turf(owner)) as DmList;
        Waiting.Detach(progressive_fire_line(turfs)); // INVOKE_ASYNC
    }

    /// Creates fire with a delay on the list of targeted turfs
    // tg: code/datums/actions/mobs/fire_breath.dm:43
    public virtual async DmTask progressive_fire_line(DmList burn_turfs = null)
    {
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
        // Guys we have already hit, no double dipping
        DmList hit_list = Dm.list(owner); // also don't burn ourselves
        foreach (turf target_turf in Dm.each<turf>(burn_turfs))
        {
            if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
                return;
            burn_turf(target_turf, hit_list, owner as mob_living);
            await sleep(fire_delay);
        }
    }

    /// Finally spawn the actual fire, spawns the fire hotspot in case you want to recolour it or something
    // tg: code/datums/actions/mobs/fire_breath.dm:55
    public virtual obj_effect_hotspot burn_turf(turf fire_turf = null, DmList hit_list = null, mob_living source = null)
    {
        obj_effect_hotspot fire_hotspot = Dm.@new<obj_effect_hotspot>(fire_turf, fire_volume, fire_temperature);
        fire_turf.hotspot_expose(fire_temperature, fire_volume, TRUE);

        foreach (mob_living barbecued in Dm.each<mob_living>(fire_turf.contents))
        {
            if (Dm.@in(barbecued, hit_list))
                continue;
            Dm.combine(ref hit_list, barbecued);
            on_burn_mob(barbecued, source);
        }

        foreach (obj_vehicle_sealed_mecha robotron in Dm.each<obj_vehicle_sealed_mecha>(fire_turf.contents))
        {
            if (Dm.@in(robotron, hit_list))
                continue;
            Dm.combine(ref hit_list, robotron);
            Waiting.NoSleep(robotron.take_damage(mech_damage, BURN, FIRE));
        }

        return fire_hotspot;
    }

    /// Do something unpleasant to someone we set on fire
    // tg: code/datums/actions/mobs/fire_breath.dm:74
    public virtual void on_burn_mob(mob_living barbecued = null, mob_living source = null)
    {
        if (fire_temperature <= TCMB)
        {
            Waiting.NoSleep(barbecued.apply_status_effect(typeof(datum_status_effect_ice_block_talisman), 2 * SECONDS));
            to_chat(barbecued, span_userdanger(I18n.T("You're frozen solid by []'s icy breath!", source)));
        }
        else
            to_chat(barbecued, span_userdanger(I18n.T("You are burned by []'s fire breath!", source)));
        Waiting.NoSleep(barbecued.adjust_fire_loss(fire_damage));
    }
}

/// Shoot three lines of fire in a sort of fork pattern approximating a cone
// tg: code/datums/actions/mobs/fire_breath.dm:83
public partial class datum_action_cooldown_mob_cooldown_fire_breath_cone : datum_action_cooldown_mob_cooldown_fire_breath
{
    // tg: code/datums/actions/mobs/fire_breath.dm:89
    public override async DmTask attack_sequence(atom target = null)
    {
        playsound(owner.loc, fire_sound, 200, TRUE);
        foreach (object offset in Dm.each(angles))
            fire_line(target, offset);
    }
}

/// Shoot fire in a whole bunch of directions
// tg: code/datums/actions/mobs/fire_breath.dm:95
public partial class datum_action_cooldown_mob_cooldown_fire_breath_mass_fire : datum_action_cooldown_mob_cooldown_fire_breath
{
    // tg: code/datums/actions/mobs/fire_breath.dm:109
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        target_atom = get_step(owner, owner.dir) as atom; // Just shoot it forwards, we don't need to click on someone for this one
        return await base.Activate(target_atom);
    }

    // tg: code/datums/actions/mobs/fire_breath.dm:113
    public override async DmTask attack_sequence(atom target = null)
    {
        float queued_spins = 0;
        for (float i = 1; i <= total_spins; i++)
        {
            float delay = queued_spins * breath_delay;
            queued_spins++;
            _addtimer(Dm.@new<datum_callback>(this, nameof(fire_spin), target, queued_spins), delay, file: "code/datums/actions/mobs/fire_breath.dm", line: 118);
        }
    }

    /// Breathe fire in a circle, with a slight angle offset based on which of our several circles it is
    // tg: code/datums/actions/mobs/fire_breath.dm:121
    public virtual void fire_spin(object target = null, object spin_count = null)
    {
        if (QDELETED(owner) || owner.stat == DEAD)
            return; // Too dead to spin
        playsound(owner.loc, fire_sound, 200, TRUE);
        float angle_increment = 360 / sectors;
        float additional_offset = Dm.num(spin_count) * angle_increment / 2;
        for (float i = 1; i <= sectors; i++)
            fire_line(target as atom, (angle_increment * i) + (additional_offset));
    }
}
