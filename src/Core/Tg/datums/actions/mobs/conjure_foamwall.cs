// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/conjure_foamwall.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/datums/actions/mobs/conjure_foamwall.dm:1
public partial class datum_action_cooldown_spell_conjure_foam_wall : datum_action_cooldown_spell_conjure
{
    // tg: code/datums/actions/mobs/conjure_foamwall.dm:14
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        turf owner_turf = get_turf(owner);
        if (Dm.truthy(await owner_turf.is_blocked_turf(exclude_mobs: TRUE)))
            return FALSE;
        return TRUE;
    }
}
