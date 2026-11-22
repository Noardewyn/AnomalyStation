// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/infuser_entries/infuser_tier_zero_entries.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the tier zero entries (the rejected fly, fox, mothroach, lizard, cat, penguin, plant) and the plant's
// organ choice. The var blocks are generated (D111).

// tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_zero_entries.dm:132
public partial class datum_infuser_entry_plants : datum_infuser_entry
{
    // tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_zero_entries.dm:160
    public override DmList get_output_organs(mob_living_carbon_human target = null, atom_movable infused_from = null)
    {
        // Prioritize pod hair since it's the only thing that matters here
        if (target.get_organ_by_type(typeof(obj_item_organ_pod_hair)) == null)
            return Dm.list(typeof(obj_item_organ_pod_hair));

        return base.get_output_organs(target, infused_from);
    }
}
