// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/temporary_description.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file (a corpse's story in its examine, gone when it is revived). The var block is generated (D111).

/**
 * Adds examine text to something which is removed when receiving specified signals, by default the revive signal.
 * The default settings are set up to be applied to a corpse to add some kind of immersive storytelling text which goes away upon revival.
 */
// tg: code/datums/components/temporary_description.dm:5
public partial class datum_component_temporary_description : datum_component
{
    // tg: code/datums/components/temporary_description.dm:13
    [DmParams("description_text", "naive_description", "removal_signals")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object description_text = Dm.arg<object>(__extra, 0) ?? "There's something unusual about them.";
        object naive_description = Dm.arg<object>(__extra, 1) ?? "";
        var removal_signals = Dm.arg<DmList>(__extra, 2) ?? Dm.list(COMSIG_LIVING_REVIVE);
        object dot = await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(description_text))
            stack_trace(Dm.text("[] applied to [] with empty description, which is pointless.", type, parent), "code/datums/components/temporary_description.dm", 21);
        this.description_text = Dm.str(description_text);
        this.naive_description = Dm.str(naive_description);
        if (Dm.length(removal_signals) > 0)
            this.removal_signals = removal_signals;
        return dot;
    }

    // tg: code/datums/components/temporary_description.dm:28
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignals(parent, removal_signals, nameof(remove_component));
    }

    // tg: code/datums/components/temporary_description.dm:32
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.bitor(removal_signals, Dm.list(COMSIG_ATOM_EXAMINE)));
    }

    // tg: code/datums/components/temporary_description.dm:36
    public virtual void on_examined(atom corpse = null, mob thing_inspector = null, DmList examine_list = null)
    {
        // SIGNAL_HANDLER
        if (Dm.truthy(naive_description) && HAS_MIND_TRAIT(thing_inspector, TRAIT_NAIVE))
        {
            Dm.append(examine_list, span_notice(naive_description));
            return;
        }
        Dm.append(examine_list, span_notice(description_text));
    }

    // tg: code/datums/components/temporary_description.dm:43
    public virtual void remove_component()
    {
        // SIGNAL_HANDLER
        qdel(this); // It wouldn't be immersive if the circumstances of my grisly death remained after I was revived
    }
}
