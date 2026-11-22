// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/damage_chain.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the lightning holoparasite's chains). The var block is hand-written: the generator
// types reset_feedback_timer as a number; it holds the timer's id.

/**
 * Draws a line between you and another atom, hurt anyone stood in the line
 */
// tg: code/datums/components/damage_chain.dm:5
public partial class datum_component_damage_chain : datum_component
{
    /// Time between making any attacks at which we just reset the successful attack counter
    // tg: code/datums/components/damage_chain.dm:23
    public object reset_feedback_timer = 0;

    // tg: code/datums/components/damage_chain.dm:5
    public datum_component_damage_chain()
    {
        dupe_mode = COMPONENT_DUPE_ALLOWED;
    }

    // tg: code/datums/components/damage_chain.dm:27
    [DmParams("linked_to", "max_distance", "beam_icon", "beam_state", "beam_type", "tick_interval", "damage_per_tick", "damage_type", "validate_target", "chain_damage_feedback", "feedback_interval")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object linked_to = Dm.arg(__extra, 0);
        object max_distance = Dm.arg(__extra, 1) ?? 7;
        object beam_icon = Dm.arg(__extra, 2) ?? Dm.resource("icons/effects/beam.dmi");
        object beam_state = Dm.arg(__extra, 3) ?? "medbeam";
        object beam_type = Dm.arg(__extra, 4) ?? typeof(obj_effect_ebeam);
        object tick_interval = Dm.arg(__extra, 5) ?? 0.3f * SECONDS;
        object damage_per_tick = Dm.arg(__extra, 6) ?? 1.2f;
        object damage_type = Dm.arg(__extra, 7) ?? BURN;
        datum_callback validate_target = Dm.arg<datum_callback>(__extra, 8);
        datum_callback chain_damage_feedback = Dm.arg<datum_callback>(__extra, 9);
        object feedback_interval = Dm.arg(__extra, 10) ?? 5;
        await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!isatom(linked_to))
            throw Dm.CRASH(Dm.text("Attempted to create [] linking [] with non-atom []!", type, parent.type, linked_to), "code/datums/components/damage_chain.dm", 44);

        this.tick_interval = tick_interval;
        this.damage_per_tick = damage_per_tick;
        this.damage_type = damage_type;
        this.validate_target = validate_target;
        this.chain_damage_feedback = chain_damage_feedback;
        this.feedback_interval = feedback_interval;

        atom atom_parent = (atom)parent;
        chain = atom_parent.Beam((atom)linked_to, icon: beam_icon, icon_state: Dm.str(beam_state), beam_type: beam_type as Type, maxdistance: Dm.num(max_distance)) as datum_beam;
        RegisterSignal(chain, COMSIG_QDELETING, nameof(end_beam));
        START_PROCESSING(SSfastprocess, this);
        return null;
    }

    // tg: code/datums/components/damage_chain.dm:58
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(end_beam)); // We actually don't really use many signals it's all processing
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/damage_chain.dm:61
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_LIVING_DEATH);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/damage_chain.dm:64
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(chain))
        {
            UnregisterSignal(chain, COMSIG_QDELETING);
            QDEL_NULL(ref chain);
        }
        chain = null;
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    /// Destroy ourself
    // tg: code/datums/components/damage_chain.dm:73
    [SignalHandler]
    public virtual void end_beam()
    {
        qdel(this);
    }

    // tg: code/datums/components/damage_chain.dm:77
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        bool successful_hit = false;
        DmList target_turfs = Dm.list();
        foreach (obj_effect_ebeam chainpart in Dm.each<obj_effect_ebeam>(chain.elements))
        {
            if (chainpart == null || !Dm.truthy(chainpart.x) || !Dm.truthy(chainpart.y) || !Dm.truthy(chainpart.z))
                continue;
            turf overlaps = get_turf_pixel(chainpart) as turf;
            Dm.combine(target_turfs, overlaps);
            if (overlaps == get_turf(chain.origin) || overlaps == get_turf(chain.target))
                continue;
            foreach (turf nearby_turf in Dm.each<turf>(circle_range(overlaps, 1)))
                Dm.combine(target_turfs, nearby_turf);
        }

        foreach (turf hit_turf in Dm.each_cast<turf>(target_turfs))
        {
            foreach (mob_living victim in Dm.each<mob_living>(hit_turf))
            {
                if (victim == parent || victim.stat == DEAD)
                    continue;
                if (validate_target != null && !Dm.truthy(Waiting.NoSleep(validate_target.Invoke(victim))))
                    continue;
                if (successful_attacks == 0 && chain_damage_feedback != null)
                    Waiting.NoSleep(chain_damage_feedback.Invoke(victim));
                Waiting.NoSleep(victim.apply_damage(Dm.num(damage_per_tick), Dm.str(damage_type), wound_bonus: CANT_WOUND));
                successful_hit = true;
            }
        }

        if (chain_damage_feedback == null)
            return null;
        if (successful_hit)
        {
            successful_attacks++;
            reset_feedback_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(reset_feedback)), 10 * SECONDS, Dm.bitor(Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/datums/components/damage_chain.dm", line: 104);
        }
        if (successful_attacks > Dm.num(feedback_interval))
            reset_feedback();
        return null;
    }

    /// Make it so that the next time we hit something we'll invoke the feedback callback
    // tg: code/datums/components/damage_chain.dm:110
    public virtual void reset_feedback()
    {
        successful_attacks = 0;
        deltimer(reset_feedback_timer);
    }
}
