// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/turfs_in_oview.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). tg's quirk kept: the second collect_candidates is declared on oview_water_turfs (a typo for oview_land_turfs), so DM takes the last
// definition for the water source and the land source returns the base's empty list.

/// The open non-water floors in view (see above).
// tg: code/datums/ai/basic_mobs/target_sources/turfs_in_oview.dm:14
public partial class datum_target_source_oview_water_turfs : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/turfs_in_oview.dm:16
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (turf_open potential_floor in Dm.each<turf_open>(oview(range, pawn)))
        {
            if (iswaterturf(potential_floor))
                continue;
            Dm.append(ref candidates, potential_floor);
        }

        return candidates;
    }
}
