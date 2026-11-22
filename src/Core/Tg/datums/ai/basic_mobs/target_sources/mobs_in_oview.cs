// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/mobs_in_oview.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// The living mobs in view.
// tg: code/datums/ai/basic_mobs/target_sources/mobs_in_oview.dm:1
public partial class datum_target_source_oview_living : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/mobs_in_oview.dm:3
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living living_candidate in Dm.each<mob_living>(oview(range, pawn)))
            Dm.append(ref candidates, living_candidate);

        return candidates;
    }
}

/// The raptors in view.
// tg: code/datums/ai/basic_mobs/target_sources/mobs_in_oview.dm:12
public partial class datum_target_source_oview_raptor_babies : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/mobs_in_oview.dm:14
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_raptor candidate in Dm.each<mob_living_basic_raptor>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);

        return candidates;
    }
}
