// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ai_retaliate_advanced.dm
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
 * Differs from the element as it passes new entries through a callback.
 */
// tg: code/datums/components/ai_retaliate_advanced.dm:6
public partial class datum_component_ai_retaliate_advanced : datum_component
{
    // tg: code/datums/components/ai_retaliate_advanced.dm:10
    [DmParams("post_retaliate_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_callback post_retaliate_callback = Dm.arg<datum_callback>(__extra, 0);
        if (!ismob(parent))
            return ELEMENT_INCOMPATIBLE;

        this.post_retaliate_callback = post_retaliate_callback;
        await parent.AddElement(typeof(datum_element_relay_attackers));

        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return null;
    }

    // tg: code/datums/components/ai_retaliate_advanced.dm:19
    public override object Destroy(float? force = null)
    {
        post_retaliate_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/ai_retaliate_advanced.dm:23
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
    }

    // tg: code/datums/components/ai_retaliate_advanced.dm:26
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_WAS_ATTACKED);
    }

    /// Add an attacking atom to a blackboard list of things which attacked us
    // tg: code/datums/components/ai_retaliate_advanced.dm:30
    [SignalHandler]
    public virtual void on_attacked(mob victim = null, atom attacker = null)
    {
        if (victim.ai_controller == null)
            return;

        ((datum_ai_controller)victim.ai_controller).set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, attacker, world.time);
        post_retaliate_callback?.InvokeAsync(attacker);
    }
}
