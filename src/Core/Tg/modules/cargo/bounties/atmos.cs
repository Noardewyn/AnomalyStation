// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/atmos.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file. The var blocks are generated (D111).

// tg: code/modules/cargo/bounties/atmos.dm:1
public partial class datum_bounty_item_atmospherics : datum_bounty_item
{
    // tg: code/modules/cargo/bounties/atmos.dm:11
    public override object applies_to(obj applied_obj = null)
    {
        if (!Dm.truthy(base.applies_to(applied_obj)))
            return FALSE;
        var applied_tank = (obj_item_tank)applied_obj;
        var our_mix = Waiting.NoSleep(applied_tank.return_air()) as datum_gas_mixture;
        if (!Dm.truthy(our_mix.moles[gas_type]))
            return FALSE;
        return Dm.num(our_mix.moles[gas_type]) >= moles_required ? TRUE : FALSE;
    }

    // tg: code/modules/cargo/bounties/atmos.dm:20
    public override object contribution_amount(obj shipped = null)
    {
        var shipped_tank = (obj_item_tank)shipped;
        var our_mix = Waiting.NoSleep(shipped_tank.return_air()) as datum_gas_mixture;
        return our_mix.moles[gas_type];
    }
}
