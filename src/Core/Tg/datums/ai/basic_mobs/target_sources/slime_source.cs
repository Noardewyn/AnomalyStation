// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/slime_source.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// The living mobs in view but the slimes.
// tg: code/datums/ai/basic_mobs/target_sources/slime_source.dm:1
public partial class datum_target_source_oview_living_no_slimes : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/slime_source.dm:3
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living living_candidate in Dm.each<mob_living>(oview(range, pawn)))
        {
            if (isslime(living_candidate))
                continue;
            Dm.append(ref candidates, living_candidate);
        }

        return candidates;
    }
}
