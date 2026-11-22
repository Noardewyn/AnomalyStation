// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/held_items_then_oview.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Returns the pawn's held items first, then nearby atoms via oview(). Used for food searches that prefer held items.
// tg: code/datums/ai/basic_mobs/target_sources/held_items_then_oview.dm:2
public partial class datum_target_source_held_items_then_oview : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/held_items_then_oview.dm:4
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (object candidate_item in Dm.each(pawn.held_items))
        {
            if (candidate_item == null)
                continue;
            Dm.append(ref candidates, candidate_item);
        }
        Dm.append(ref candidates, oview(range, pawn));
        return candidates;
    }
}
