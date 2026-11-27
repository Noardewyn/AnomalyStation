// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/charge_apc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: giving off charge to an APC (the electrogenic fish's action). The var block is generated (D111).

// tg: code/datums/actions/mobs/charge_apc.dm:4
public partial class datum_action_cooldown_mob_cooldown_charge_apc : datum_action_cooldown_mob_cooldown
{
    ///how much charge are we giving off to an APC?
    // tg: code/datums/actions/mobs/charge_apc.dm:2  #define CHARGE_AMOUNT (0.08 * STANDARD_CELL_CHARGE) (file-local)
    private const float CHARGE_AMOUNT = 0.08f * STANDARD_CELL_CHARGE;

    // tg: code/datums/actions/mobs/charge_apc.dm:11
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        if (target_atom is not obj_machinery_power_apc)
            return null;
        obj_machinery_power_apc target_apc = (obj_machinery_power_apc)target_atom;
        if (target_apc.cell == null)
            return null;
        Dm.@new<obj_effect_particle_effect_sparks>(target_apc.loc);
        await target_apc.cell.give(CHARGE_AMOUNT);
        StartCooldown();
        return TRUE;
    }
}
