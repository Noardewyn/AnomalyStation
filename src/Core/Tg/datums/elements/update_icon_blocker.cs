// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/update_icon_blocker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298: the employment contract). The var block is generated.

/// Prevents calling anything in update_icon() like update_icon_state() or update_overlays()
// tg: code/datums/elements/update_icon_blocker.dm:2
public partial class datum_element_update_icon_blocker : datum_element
{
    // tg: code/datums/elements/update_icon_blocker.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_UPDATE_ICON, nameof(block_update_icon));
        return dot;
    }

    // tg: code/datums/elements/update_icon_blocker.dm:11
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_UPDATE_ICON);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/update_icon_blocker.dm:15
    [SignalHandler]
    public virtual object block_update_icon()
    {
        return Dm.bitor(COMSIG_ATOM_NO_UPDATE_ICON_STATE, COMSIG_ATOM_NO_UPDATE_OVERLAYS);
    }
}
