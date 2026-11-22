// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/bioware/_bioware.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file (the bioware operations grant these). The var blocks are generated.

/**
 * ## Bioware status effect
 *
 * Simple holder status effects that grants the owner mob basic buffs
 */
// tg: code/datums/status_effects/buffs/bioware/_bioware.dm:6
public partial class datum_status_effect_bioware : datum_status_effect
{
    // tg: code/datums/status_effects/buffs/bioware/_bioware.dm:12
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!ishuman(owner))
            return FALSE;

        bioware_gained();
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs/bioware/_bioware.dm:19
    public override async DmTask<object> on_remove()
    {
        bioware_lost();
        return null;
    }

    /// Called when applying to the mob.
    // tg: code/datums/status_effects/buffs/bioware/_bioware.dm:23
    public virtual void bioware_gained()
    {
        return;
    }

    /// Called when removing from the mob.
    // tg: code/datums/status_effects/buffs/bioware/_bioware.dm:27
    public virtual void bioware_lost()
    {
        return;
    }
}
