// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/bioware/nerves.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file (the bioware operations grant these). The var blocks are generated.

// Grounded Nerves - Immunity to being zapped
// tg: code/datums/status_effects/buffs/bioware/nerves.dm:6
public partial class datum_status_effect_bioware_nerves_grounded : datum_status_effect_bioware_nerves
{
    // tg: code/datums/status_effects/buffs/bioware/nerves.dm:8
    public override void bioware_gained()
    {
        ADD_TRAIT(owner, TRAIT_SHOCKIMMUNE, TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/buffs/bioware/nerves.dm:11
    public override void bioware_lost()
    {
        REMOVE_TRAIT(owner, TRAIT_SHOCKIMMUNE, TRAIT_STATUS_EFFECT(id));
    }
}

// Spliced Nerves - Reduced stun time and stamina damage taken
// tg: code/datums/status_effects/buffs/bioware/nerves.dm:15
public partial class datum_status_effect_bioware_nerves_spliced : datum_status_effect_bioware_nerves
{
    // tg: code/datums/status_effects/buffs/bioware/nerves.dm:17
    public override void bioware_gained()
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_STUN, 0.5f);
        MODIFY_PHYSIOLOGY(owner, STAMINA, 0.8f);
    }

    // tg: code/datums/status_effects/buffs/bioware/nerves.dm:21
    public override void bioware_lost()
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_STUN, 2);
        MODIFY_PHYSIOLOGY(owner, STAMINA, 1.25f);
    }
}
