// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/nodes/alien_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's node procs; the nodes are var blocks (generated, D111).

// tg: code/modules/research/techweb/nodes/alien_nodes.dm:1
public partial class datum_techweb_node_alien : datum_techweb_node
{
    // tg: code/modules/research/techweb/nodes/alien_nodes.dm:5
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.append(ref required_items_to_unlock, subtypesof(typeof(obj_item_abductor)));
        Dm.append(ref required_items_to_unlock, subtypesof(typeof(obj_item_circuitboard_machine_abductor)));
    }
}

// tg: code/modules/research/techweb/nodes/alien_nodes.dm:10
public partial class datum_techweb_node_alien_base : datum_techweb_node_alien
{
    // tg: code/modules/research/techweb/nodes/alien_nodes.dm:38
    public override async DmTask on_station_research(atom research_source = null)
    {
        await base.on_station_research(research_source);
        SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_ALIENTECH] = TRUE;
    }
}
