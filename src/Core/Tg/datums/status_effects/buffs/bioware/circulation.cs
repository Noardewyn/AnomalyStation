// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/bioware/circulation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file (the bioware operations grant these). The var blocks are generated.

/// Muscled veins - Removes the need to have a heart
// tg: code/datums/status_effects/buffs/bioware/circulation.dm:6
public partial class datum_status_effect_bioware_heart_muscled_veins : datum_status_effect_bioware_heart
{
    // tg: code/datums/status_effects/buffs/bioware/circulation.dm:8
    public override void bioware_gained()
    {
        ADD_TRAIT(owner, TRAIT_STABLEHEART, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/buffs/bioware/circulation.dm:11
    public override void bioware_lost()
    {
        REMOVE_TRAIT(owner, TRAIT_STABLEHEART, TRAIT_STATUS_EFFECT(id));
    }
}

/// Threaded veins - Bleed way less
// tg: code/datums/status_effects/buffs/bioware/circulation.dm:15
public partial class datum_status_effect_bioware_heart_threaded_veins : datum_status_effect_bioware_heart
{
    // tg: code/datums/status_effects/buffs/bioware/circulation.dm:17
    public override void bioware_gained()
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 0.25f);
    }

    // tg: code/datums/status_effects/buffs/bioware/circulation.dm:20
    public override void bioware_lost()
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 4);
    }
}
