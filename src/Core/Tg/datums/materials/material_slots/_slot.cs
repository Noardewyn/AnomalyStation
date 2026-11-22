// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/material_slots/_slot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var block is generated (D111). The slots themselves
// (generic.dm: weapon heads and handles) come with the spears and maces that use them.

/// Singleton datum which controls how materials affect atoms they're applied to
// tg: code/datums/materials/material_slots/_slot.dm:2
public partial class datum_material_slot : datum
{
    /// Called when the material in this slot is applied to the atom. Return FALSE to prevent base apply_single_mat_effect from running.
    /// If the material is main, main material application will also be cancelled. Should be consistent with on_removed.
    // tg: code/datums/materials/material_slots/_slot.dm:13
    public virtual async DmTask<object> on_applied(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        return TRUE;
    }

    /// Called when the material in this slot is removed from the atom. Return FALSE to prevent base remove_single_mat_effect from running.
    /// If the material is main, main material removal will also be cancelled. Should be consistent with on_applied.
    // tg: code/datums/materials/material_slots/_slot.dm:18
    public virtual async DmTask<object> on_removed(atom target = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        return TRUE;
    }
}
