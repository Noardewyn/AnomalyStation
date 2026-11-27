// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Fires a bloody beam. Brimdemon Blast!
// tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:2
public partial class datum_action_cooldown_mob_cooldown_brimbeam : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:23
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner != null)
            await owner.AddElement(typeof(datum_element_relay_attackers));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:28
    public override object Destroy(float? force = null)
    {
        extinguish_laser();
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:32
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(360 * SECONDS);

        abort_blast = FALSE;
        owner.face_atom(target);
        owner.move_resist = MOVE_FORCE_VERY_STRONG;
        owner.balloon_alert_to_viewers("charging...");
        mutable_appearance direction_overlay = mutable_appearance(Dm.resource("icons/mob/simple/lavaland/lavaland_monsters.dmi"), "brimdemon_telegraph_dir") as mutable_appearance;
        // tg also adds emissive_appearance(..., "brimdemon_telegraph_dir", owner, alpha = 150), the arrow's glow in the dark (DEVIATION DV12)
        owner.add_overlay(direction_overlay);
        RegisterSignal(owner, COMSIG_ATOM_WAS_ATTACKED, nameof(on_owner_attacked));

        object fully_charged = await do_after(owner, delay: charge_duration, target: owner, extra_checks: Dm.@new<datum_callback>(this, nameof(beam_charge_check)));
        owner.cut_overlay(direction_overlay);
        if (!Dm.truthy(fully_charged))
        {
            UnregisterSignal(owner, COMSIG_ATOM_WAS_ATTACKED);
            StartCooldown();
            return TRUE;
        }

        if (!Dm.truthy(await fire_laser()))
        {
            DmList fail_emotes = TgState.Current.datum_action_cooldown_mob_cooldown_brimbeam__Activate__fail_emotes ??= Dm.list("coughs.", "wheezes.", "belches out a puff of black smoke.");
            owner.manual_emote(pick(fail_emotes));
            UnregisterSignal(owner, COMSIG_ATOM_WAS_ATTACKED);
            StartCooldown();
            return TRUE;
        }

        if (owner is mob_living_basic_mining_brimdemon)
        {
            mob_living_basic_mining_brimdemon demon = owner as mob_living_basic_mining_brimdemon;
            demon.icon_state = demon.firing_icon_state;
            demon.update_appearance(UPDATE_OVERLAYS);
        }

        await do_after(owner, delay: beam_duration, target: owner, cog_icon: null, extra_checks: Dm.@new<datum_callback>(this, nameof(beam_charge_check)));
        UnregisterSignal(owner, COMSIG_ATOM_WAS_ATTACKED);
        extinguish_laser();
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:71
    [SignalHandler]
    public virtual void on_owner_attacked(datum source = null, atom attacker = null, object attack_flags = null, object direction = null)
    {
        if (!Dm.truthy(Dm.bitand(attack_flags, ATTACK_RANGED)) && !Dm.truthy(Dm.bitand(direction, owner.dir)))
            abort_blast = TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:76
    public virtual object beam_charge_check()
    {
        return Dm.not(abort_blast);
    }

    /// Create a laser in the direction we are facing
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:80
    public virtual async DmTask<object> fire_laser()
    {
        owner.visible_message(span_danger(I18n.T("[] fires a brimbeam!", owner)));
        playsound(owner, Dm.resource("sound/mobs/non-humanoids/brimdemon/brimdemon.ogg"), 150, FALSE, 0, 3);
        turf target_turf = get_ranged_target_turf(owner, owner.dir, beam_range) as turf;
        turf origin_turf = get_turf(owner) as turf;
        DmList affected_turfs = Dm.sub(get_line(origin_turf, target_turf), origin_turf) as DmList;
        foreach (turf affected_turf in Dm.each<turf>(affected_turfs))
        {
            if (Dm.truthy(affected_turf.opacity))
                break;
            float blocked = FALSE;
            foreach (obj potential_block in Dm.each<obj>(affected_turf))
            {
                if (Dm.truthy(potential_block.opacity))
                {
                    blocked = TRUE;
                    break;
                }
            }
            if (Dm.truthy(blocked))
                break;
            obj_effect_brimbeam new_brimbeam = Dm.@new<obj_effect_brimbeam>(affected_turf);
            await new_brimbeam.setDir(owner.dir);
            Dm.append(ref beam_parts, new_brimbeam);
            new_brimbeam.assign_creator(owner as mob_living);
            foreach (mob_living hit_mob in Dm.each<mob_living>(affected_turf))
            {
                await hit_mob.apply_damage(25, BURN, blocked: Dm.num(hit_mob.run_armor_check(null, LASER, silent: TRUE)), wound_bonus: CANT_WOUND);
                to_chat(hit_mob, span_userdanger(I18n.T("You're blasted by []'s brimbeam!", owner)));
            }
            RegisterSignal(new_brimbeam, COMSIG_QDELETING, nameof(extinguish_laser)); // In case idk a singularity eats it or something
        }
        if (!Dm.truthy(Dm.length(beam_parts)))
            return FALSE;
        atom last_brimbeam = beam_parts[Dm.length(beam_parts)] as atom;
        last_brimbeam.icon_state = "brimbeam_end";
        atom first_brimbeam = beam_parts[1] as atom;
        first_brimbeam.icon_state = "brimbeam_start";
        return TRUE;
    }

    /// Get rid of our laser when we are done with it
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:113
    public virtual object extinguish_laser()
    {
        if (!Dm.truthy(Dm.length(beam_parts)))
            return FALSE;
        if (owner != null)
        {
            owner.move_resist = Dm.initial(owner, (mob p) => p.move_resist);
            if (owner is mob_living_basic_mining_brimdemon && owner.stat != DEAD)
            {
                mob_living_basic_mining_brimdemon demon = owner as mob_living_basic_mining_brimdemon;
                demon.icon_state = demon.icon_living as string;
                demon.update_appearance(UPDATE_OVERLAYS);
            }
        }
        foreach (obj_effect_brimbeam beam in Dm.each<obj_effect_brimbeam>(beam_parts))
            beam.disperse();
        beam_parts = Dm.list();
        return null;
    }
}

/// Segments of the actual beam, these hurt if you stand in them
// tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:127
public partial class obj_effect_brimbeam : obj_effect
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:140
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:144
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:148
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object ignore = creator?.resolve();
        foreach (mob_living hit_mob in Dm.each<mob_living>(get_turf(this)))
        {
            if (hit_mob != ignore)
                Waiting.NoSleep(hit_mob.apply_damage(7, BURN, blocked: Dm.num(hit_mob.run_armor_check(null, LASER, silent: TRUE)), wound_bonus: CANT_WOUND));
        }
        return null;
    }

    /// Ignore damage dealt to this mob
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:155
    public virtual void assign_creator(mob_living maker = null)
    {
        creator = WEAKREF(maker) as datum_weakref;
    }

    /// Disappear
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:159
    public virtual void disperse()
    {
        animate(this, Dm.named("time", 0.5f * SECONDS), Dm.named("alpha", 0));
        // QDEL_IN(src, 0.5 SECONDS), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.5f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 0.5f * SECONDS, file: "code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm", line: 161);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimbeam.dm:54 (var/static/list/fail_emotes)
    internal DmList datum_action_cooldown_mob_cooldown_brimbeam__Activate__fail_emotes;
}
