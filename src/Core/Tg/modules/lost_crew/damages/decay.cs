// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/decay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).

/// We simulate decay on bodies. This is always used, but severity can differ (though tending to the more severe)
// tg: code/modules/lost_crew/damages/decay.dm:2
public partial class datum_corpse_damage_post_mortem_organ_decay : datum_corpse_damage_post_mortem
{
    // tg: code/modules/lost_crew/damages/decay.dm:7
    [DmParams("body", "severity", "sorage", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        if (!Dm.truthy(body.organs))
            return FALSE;

        // * 0.5 because life ticks happen about every 2 seconds (we really need a way to get the current life tickspeed)
        float decay_ticks = max_decay_time * Dm.num(severity) * 0.5f;

        foreach (obj_item_organ @internal in Dm.each<obj_item_organ>(body.organs))
            await @internal.apply_organ_damage(decay_ticks * @internal.decay_factor);

        return TRUE;
    }
}
