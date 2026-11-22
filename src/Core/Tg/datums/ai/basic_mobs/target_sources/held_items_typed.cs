// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/held_items_typed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gathers held items matching a fixed typepath.
// tg: code/datums/ai/basic_mobs/target_sources/held_items_typed.dm:2
public partial class datum_target_source_held_items_typed : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/held_items_typed.dm:4
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (object candidate in Dm.each(pawn.held_items))
        {
            if (istype(candidate, locate_typepath as Type))
                Dm.append(ref candidates, candidate);
        }
        return candidates;
    }
}
