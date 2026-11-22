// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/simple_access.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316). The var block is generated (D111).

///This component allows us to give a mob access without giving them an ID card.
// tg: code/datums/components/simple_access.dm:2
public partial class datum_component_simple_access : datum_component
{
    // tg: code/datums/components/simple_access.dm:8
    [DmParams("new_access", "donor_atom")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList new_access = Dm.arg<DmList>(__extra, 0);
        atom donor_atom = Dm.arg<atom>(__extra, 1);
        object dot = await base.Initialize(__extra);
        if (!ismob(parent))
            return COMPONENT_INCOMPATIBLE;
        access = new_access;
        RegisterSignal(parent, COMSIG_MOB_RETRIEVE_ACCESS, nameof(retrieve_access));
        if (donor_atom == null)
            return dot;
        if (isorgan(donor_atom))
            RegisterSignal(donor_atom, COMSIG_ORGAN_REMOVED, nameof(on_donor_removed));
        else if (donor_atom is obj_item_implant)
            RegisterSignal(donor_atom, COMSIG_IMPLANT_REMOVED, nameof(on_donor_removed));
        RegisterSignal(donor_atom, COMSIG_QDELETING, nameof(on_donor_removed));
        return dot;
    }

    // tg: code/datums/components/simple_access.dm:22
    [SignalHandler]
    public virtual void retrieve_access(datum source = null, DmList access_list = null)
    {
        Dm.append(access_list, access);
    }

    // tg: code/datums/components/simple_access.dm:26
    [SignalHandler]
    public virtual void on_donor_removed(datum source = null)
    {
        qdel(this);
    }
}
