// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ai_flee_while_injured.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * Attached to a mob with an AI controller, simply sets a flag on whether or not to run away based on current health values.
 */
// tg: code/datums/elements/ai_flee_while_injured.dm:5
public partial class datum_element_ai_flee_while_injured : datum_element
{
    // tg: code/datums/elements/ai_flee_while_injured.dm:12
    [DmParams("target", "stop_fleeing_at", "start_fleeing_below")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object stop_fleeing_at = Dm.arg(__extra, 0) ?? 1f;
        object start_fleeing_below = Dm.arg(__extra, 1) ?? 0.5f;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        mob_living living_target = target as mob_living;
        if (!Dm.truthy(living_target.ai_controller))
            return ELEMENT_INCOMPATIBLE;
        this.stop_fleeing_at = stop_fleeing_at;
        this.start_fleeing_below = start_fleeing_below;
        RegisterSignal(target, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_health_changed));
        on_health_changed(living_target);
        return dot;
    }

    // tg: code/datums/elements/ai_flee_while_injured.dm:24
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_HEALTH_UPDATE);
        return dot;
    }

    /// When the mob's health changes, check what the blackboard state should be
    // tg: code/datums/elements/ai_flee_while_injured.dm:29
    [SignalHandler]
    public virtual void on_health_changed(mob_living source = null)
    {
        if (source.ai_controller == null)
            return;

        datum_ai_controller controller = source.ai_controller as datum_ai_controller;
        float current_health_percentage = Dm.num(source.health) / Dm.num(source.maxHealth);
        if (Dm.truthy(controller.blackboard[BB_BASIC_MOB_STOP_FLEEING]))
        {
            if (current_health_percentage > Dm.num(start_fleeing_below))
                return;
            controller.set_blackboard_key(BB_BASIC_MOB_STOP_FLEEING, FALSE);
            return;
        }

        if (current_health_percentage < Dm.num(stop_fleeing_at))
            return;
        controller.set_blackboard_key(BB_BASIC_MOB_STOP_FLEEING, TRUE);
    }
}
