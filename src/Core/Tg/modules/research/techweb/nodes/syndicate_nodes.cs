// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/nodes/syndicate_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's node procs; the nodes are var blocks (generated, D111).

// tg: code/modules/research/techweb/nodes/syndicate_nodes.dm:1
public partial class datum_techweb_node_syndicate_basic : datum_techweb_node
{
    // tg: code/modules/research/techweb/nodes/syndicate_nodes.dm:21
    public override async DmTask New(params object[] __extra) //Crappy way of making syndicate gear decon supported until there's another way.
    {
        await base.New(__extra);
        // DEVIATION(DV44): tg waits for SSearly_assets, which generates the uplink's asset (and so SStraitor.uplink_items); the port
        // has no SSearly_assets and SStraitor's Initialize generates that asset itself (traitor.cs), so the node waits for SStraitor
        // (a host-built room without the Master has no SStraitor, rules 10.6: no uplink item unlocks it there)
        if (SStraitor == null)
            return;
        if (!Dm.truthy(SStraitor.initialized))
            RegisterSignal(SStraitor, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(register_uplink_items));
        else
            register_uplink_items();
    }

    // tg: code/modules/research/techweb/nodes/syndicate_nodes.dm:28
    public virtual void register_uplink_items(datum source = null)
    {
        UnregisterSignal(SStraitor, COMSIG_SUBSYSTEM_POST_INITIALIZE);
        required_items_to_unlock = Dm.list();
        foreach (datum_uplink_item item in Dm.each_cast<datum_uplink_item>(SStraitor.uplink_items))
        {
            if (item.item == null || item.item == typeof(obj_item_loot_table_maker) /* ABSTRACT_UPLINK_ITEM */ || !Dm.truthy(Dm.bitand(item.uplink_item_flags, SYNDIE_ILLEGAL_TECH)))
                continue;
            required_items_to_unlock = Dm.bitor(required_items_to_unlock, item.item) as DmList; //allows deconning to unlock.
        }
    }
}
