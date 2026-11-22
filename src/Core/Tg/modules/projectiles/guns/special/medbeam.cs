// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/special/medbeam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package P6, D298: the medical beamgun of the medibeam and combat arm implants, the nuclear operatives' and the
// mech's beamgun). The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/special/medbeam.dm:1
public partial class obj_item_gun_medbeam : obj_item_gun
{
    // tg: code/modules/projectiles/guns/special/medbeam.dm:21
    public override object Destroy(float? force = null)
    {
        LoseTarget();
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:25
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        LoseTarget();
        return null;
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:29
    [DmParams("user")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        LoseTarget();
    }

    /**
     * Proc that always is called when we want to end the beam and makes sure things are cleaned up, see beam_died()
     */
    // tg: code/modules/projectiles/guns/special/medbeam.dm:36
    public virtual void LoseTarget()
    {
        if (Dm.truthy(active))
        {
            QDEL_NULL(ref current_beam);
            active = FALSE;
            on_beam_release(current_target);
        }
        STOP_PROCESSING(SSobj, this);
        current_target = null;
    }

    /**
     * Proc that is only called when the beam fails due to something, so not when manually ended.
     * manual disconnection = LoseTarget, so it can silently end
     * automatic disconnection = beam_died, so we can give a warning message first
     */
    // tg: code/modules/projectiles/guns/special/medbeam.dm:49
    [SignalHandler]
    public virtual void beam_died()
    {
        current_beam = null;
        active = FALSE; //skip qdelling the beam again if we're doing this proc, because
        if (isliving(loc))
            to_chat(loc, span_warning(I18n.T("You lose control of the beam!")));
        LoseTarget();
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:57
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        if (isliving(user))
            add_fingerprint(user);

        if (current_target != null)
            LoseTarget();
        if (!isliving(target))
            return null;

        current_target = (mob_living)target;
        active = TRUE;
        current_beam = user.Beam(current_target, icon_state: "medbeam", time: 10 * MINUTES, maxdistance: max_range, beam_type: typeof(obj_effect_ebeam_medical)) as datum_beam;
        RegisterSignal(current_beam, COMSIG_QDELETING, nameof(beam_died)); //this is a WAY better rangecheck than what was done before (process check)
        START_PROCESSING(SSobj, this);

        SSblackbox.record_feedback("tally", "gun_fired", 1, type);
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:75
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(mounted) && !isliving(loc))
        {
            LoseTarget();
            return null;
        }

        if (current_target == null)
        {
            LoseTarget();
            return null;
        }

        if (world.time <= last_check + check_delay)
            return null;

        last_check = world.time;

        if (!Dm.truthy(Waiting.NoSleep(los_check(get_atom_on_turf(this) as atom_movable, current_target, mid_check: Dm.@new<datum_callback>(this, nameof(mid_los_check))))))
        {
            QDEL_NULL(ref current_beam); //this will give the target lost message
            return null;
        }

        if (current_target != null)
            Waiting.NoSleep(on_beam_tick(current_target));
        return null;
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:96
    public override object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        return NONE;
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:99
    public virtual async DmTask<object> mid_los_check(atom_movable user = null, mob target = null, float? pass_args = null, turf next_step = null, obj dummy = null)
    {
        pass_args ??= Dm.num(Dm.bitor(Dm.bitor(PASSTABLE, PASSGLASS), PASSGRILLE));
        foreach (obj_effect_ebeam_medical B in Dm.each<obj_effect_ebeam_medical>(next_step)) // Don't cross the str-beams!
        {
            if (QDELETED(current_beam))
                break; //We shouldn't be processing anymore.
            if (QDELETED(B))
                continue;
            if (B.owner == null)
            {
                stack_trace(Dm.text("beam without an owner! []", B), "code/modules/projectiles/guns/special/medbeam.dm", 106);
                continue;
            }
            if (B.owner.origin != current_beam.origin)
            {
                await explosion(B.loc as atom, heavy_impact_range: 3, light_impact_range: 5, flash_range: 8, explosion_cause: this);
                qdel(dummy);
                return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:114
    public virtual void on_beam_hit(mob_living target = null)
    {
        return;
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:117
    public virtual async DmTask on_beam_tick(mob_living target = null)
    {
        if (target.health != target.maxHealth)
            Dm.@new<obj_effect_temp_visual_heal>(get_turf(target), COLOR_HEALING_CYAN);
        float need_mob_update;
        need_mob_update = Dm.num(await target.adjust_brute_loss(-4, updating_health: FALSE));
        need_mob_update += Dm.num(await target.adjust_fire_loss(-4, updating_health: FALSE));
        need_mob_update += Dm.num(await target.adjust_tox_loss(-1, updating_health: FALSE, forced: TRUE));
        need_mob_update += Dm.num(await target.adjust_oxy_loss(-1, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            await target.updatehealth();
    }

    // tg: code/modules/projectiles/guns/special/medbeam.dm:129
    public virtual void on_beam_release(mob_living target = null)
    {
        return;
    }
}

//////////////////////////////Mech Version///////////////////////////////
// tg: code/modules/projectiles/guns/special/medbeam.dm:136
public partial class obj_item_gun_medbeam_mech : obj_item_gun_medbeam
{
    // tg: code/modules/projectiles/guns/special/medbeam.dm:139
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        STOP_PROCESSING(SSobj, this); //Mech mediguns do not process until installed, and are controlled by the holder obj
        return dot;
    }
}
