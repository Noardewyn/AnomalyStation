// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/nodes/modsuit_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's node procs; the nodes are var blocks (generated, D111).

// tg: code/modules/research/techweb/nodes/modsuit_nodes.dm:122
public partial class datum_techweb_node_mod_engi_adv : datum_techweb_node
{
    // tg: code/modules/research/techweb/nodes/modsuit_nodes.dm:135
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_RADIOACTIVE_NEBULA)) //we'll really need the rad protection modsuit module
            node_flags = Dm.bitor(node_flags, TECHWEB_NODE_STARTER);
    }
}
