// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/eyes/eyes_species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OG1 (D339, D341): the zombie's eyes under a penlight and the golem eyes' ore resonance. The var blocks are generated (D111).

// tg: code/modules/surgery/organs/internal/eyes/eyes_species.dm:10
public partial class obj_item_organ_eyes_zombie : obj_item_organ_eyes
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_species.dm:16
    [DmParams("viewer", "examtool")]
    public override async DmTask<object> penlight_examine(mob_living viewer = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return span_danger(I18n.T("[] eyes []", owner.p_Their(), penlight_message));
    }
}

/// Send an ore detection pulse on a cooldown
// tg: code/modules/surgery/organs/internal/eyes/eyes_species.dm:41
public partial class datum_action_cooldown_golem_ore_sight : datum_action_cooldown
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_species.dm:49
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        mineral_scan_pulse(get_turf(target), scanner: target as obj_item);
        return dot;
    }
}
