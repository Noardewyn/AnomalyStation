// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/nodes/robo_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's node procs; the nodes are var blocks (generated, D111).

// tg: code/modules/research/techweb/nodes/robo_nodes.dm:24
public partial class datum_techweb_node_ai : datum_techweb_node
{
    // tg: code/modules/research/techweb/nodes/robo_nodes.dm:53
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_HUMAN_AI))
        {
            Dm.remove(ref unlocked_designs, Dm.list(
                typeof(datum_design_board_aicore),
                typeof(datum_design_board_aifixer),
                typeof(datum_design_boris_ai_controller),
                typeof(datum_design_intellicard),
                typeof(datum_design_mecha_tracking_ai_control)
            ));
        }
        else if (HAS_TRAIT(SSstation, STATION_TRAIT_UNIQUE_AI))
            research_costs[TECHWEB_POINT_TYPE_GENERIC] = Dm.num(research_costs[TECHWEB_POINT_TYPE_GENERIC]) * 3;
    }
}
