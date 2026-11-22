// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/can_flash_from_behind.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package X4 (D316): the whole file (the blood brothers' flash from behind; the revolution's heads use it too). The var
// block is generated.

/// This mob can flash others from behind and still get at least a partial
// Component and not element because elements can't stack.
// I don't want to have a bunch of helpers for that. We need to do this generally
// because this keeps coming up.
// tg: code/datums/components/can_flash_from_behind.dm:5
public partial class datum_component_can_flash_from_behind : datum_component
{
    // tg: code/datums/components/can_flash_from_behind.dm:8
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismob(parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/can_flash_from_behind.dm:12
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOB_PRE_FLASHED_MOB, nameof(on_pre_flashed_carbon));
    }

    // tg: code/datums/components/can_flash_from_behind.dm:15
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOB_PRE_FLASHED_MOB);
    }

    // tg: code/datums/components/can_flash_from_behind.dm:18
    [SignalHandler]
    public virtual object on_pre_flashed_carbon(object source = null, object flashed = null, object flash = null, object deviation = null)
    {
        // Always partial flash at the very least
        return Dm.eq(deviation, DEVIATION_FULL) ? DEVIATION_OVERRIDE_PARTIAL : NONE;
    }
}
