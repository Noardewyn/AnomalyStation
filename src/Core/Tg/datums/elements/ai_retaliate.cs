// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ai_retaliate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/**
 * Attached to a mob with an AI controller, passes things which have damaged it to a blackboard.
 * The AI controller is responsible for doing anything with that information.
 */
// tg: code/datums/elements/ai_retaliate.dm:5
public partial class datum_element_ai_retaliate : datum_element
{
    // tg: code/datums/elements/ai_retaliate.dm:7
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismob(target))
            return ELEMENT_INCOMPATIBLE;

        await target.AddElement(typeof(datum_element_relay_attackers));
        RegisterSignal(target, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));

        ADD_TRAIT(target, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return dot;
    }

    // tg: code/datums/elements/ai_retaliate.dm:17
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_WAS_ATTACKED);
        return dot;
    }

    /// Add an attacking atom to a blackboard list of things which attacked us.
    // tg: code/datums/elements/ai_retaliate.dm:22
    [SignalHandler]
    public virtual void on_attacked(mob victim = null, atom attacker = null)
    {
        if (victim == attacker)
            return;
        (victim.ai_controller as datum_ai_controller)?.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, attacker, world.time);
    }
}
