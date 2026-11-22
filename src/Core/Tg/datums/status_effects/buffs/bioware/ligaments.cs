// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/bioware/ligaments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file (the bioware operations grant these). The var blocks are generated.

// Hooked ligaments - Easier to dismember, but easier to reattach
// tg: code/datums/status_effects/buffs/bioware/ligaments.dm:6
public partial class datum_status_effect_bioware_ligaments_hooked : datum_status_effect_bioware_ligaments
{
    // tg: code/datums/status_effects/buffs/bioware/ligaments.dm:8
    public override void bioware_gained()
    {
        owner.add_traits(Dm.list(TRAIT_LIMBATTACHMENT, TRAIT_EASYDISMEMBER), TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/buffs/bioware/ligaments.dm:11
    public override void bioware_lost()
    {
        owner.remove_traits(Dm.list(TRAIT_LIMBATTACHMENT, TRAIT_EASYDISMEMBER), TRAIT_STATUS_EFFECT(id));
    }
}

// Reinforced ligaments - Easier to break, but cannot be dismembered
// tg: code/datums/status_effects/buffs/bioware/ligaments.dm:15
public partial class datum_status_effect_bioware_ligaments_reinforced : datum_status_effect_bioware_ligaments
{
    // tg: code/datums/status_effects/buffs/bioware/ligaments.dm:17
    public override void bioware_gained()
    {
        owner.add_traits(Dm.list(TRAIT_NODISMEMBER, TRAIT_EASILY_WOUNDED), TRAIT_STATUS_EFFECT(id));
    }

    // tg: code/datums/status_effects/buffs/bioware/ligaments.dm:20
    public override void bioware_lost()
    {
        owner.remove_traits(Dm.list(TRAIT_NODISMEMBER, TRAIT_EASILY_WOUNDED), TRAIT_STATUS_EFFECT(id));
    }
}
