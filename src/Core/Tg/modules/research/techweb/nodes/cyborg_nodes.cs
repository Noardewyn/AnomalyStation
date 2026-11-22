// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/nodes/cyborg_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's node procs; the nodes are var blocks (generated, D111).

// tg: code/modules/research/techweb/nodes/cyborg_nodes.dm:94
public partial class datum_techweb_node_borg_utility : datum_techweb_node
{
    // tg: code/modules/research/techweb/nodes/cyborg_nodes.dm:112
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_disable_secborg)))) /* CONFIG_GET(flag/disable_secborg) */
            Dm.append(ref unlocked_designs, typeof(datum_design_borg_upgrade_disablercooler));
    }
}

// tg: code/modules/research/techweb/nodes/cyborg_nodes.dm:164  (implied by its subtypes' paths)
public partial class datum_techweb_node_cyber : datum_techweb_node
{
    // tg: code/modules/research/techweb/nodes/cyborg_nodes.dm:164
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_CYBERNETIC_REVOLUTION))
            research_costs[TECHWEB_POINT_TYPE_GENERIC] = Dm.num(research_costs[TECHWEB_POINT_TYPE_GENERIC]) / 2;
    }
}
