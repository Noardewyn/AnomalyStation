// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/_design_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD2, D333: the R&D consoles and servers): the design disk whose blueprints the R&D console uploads to
// its techweb (the lavaland and ruin loot disks are its subtypes), and the B.E.P.I.S.'s experimental disks that unhide and research
// their node. The var blocks are generated (D111).

// tg: code/modules/research/designs/_design_disk.dm:1
public partial class obj_item_disk_design_disk : obj_item_disk
{
    // tg: code/modules/research/designs/_design_disk.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            pixel_x = base_pixel_x + rand(-5, 5);
            pixel_y = base_pixel_y + rand(-5, 5);
        }
        return dot;
    }

    // tg: code/modules/research/designs/_design_disk.dm:16
    public override object Destroy(float? force = null)
    {
        blueprints = null;
        return base.Destroy(force);
    }

    /**
     * Used for special interactions with a techweb when uploading the designs.
     * Args:
     * - stored_research - The techweb that's storing us.
     */
    // tg: code/modules/research/designs/_design_disk.dm:25
    public virtual DmTask on_upload(datum_techweb stored_research = null, atom research_source = null) => DmTask.CompletedTask;
}

// tg: code/modules/research/designs/_design_disk.dm:28
public partial class obj_item_disk_design_disk_bepis : obj_item_disk_design_disk
{
    // tg: code/modules/research/designs/_design_disk.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        bepis_node = pick(SSresearch.techweb_nodes_experimental);

        datum_techweb_node actual_node = SSresearch.techweb_nodes[bepis_node] as datum_techweb_node;
        blueprints = assoc_to_keys(actual_node.unlocked_designs) as DmList;
        return dot;
    }

    ///Unhide and research our node so we show up in the R&D console.
    // tg: code/modules/research/designs/_design_disk.dm:45
    public override async DmTask on_upload(datum_techweb stored_research = null, atom research_source = null)
    {
        Dm.remove(ref stored_research.hidden_nodes, bepis_node);
        await stored_research.research_node(bepis_node, force: TRUE, auto_adjust_cost: FALSE, research_source: research_source);
    }
}

/**
 * Subtype of Bepis tech disk
 * Removes the tech disk that's held on it from the experimental node list, making them not show up in future disks.
 */
// tg: code/modules/research/designs/_design_disk.dm:53
public partial class obj_item_disk_design_disk_bepis_remove_tech : obj_item_disk_design_disk_bepis
{
    // tg: code/modules/research/designs/_design_disk.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.remove(ref SSresearch.techweb_nodes_experimental, bepis_node);
        datum_techweb_node actual_node = SSresearch.techweb_nodes[bepis_node] as datum_techweb_node;
        log_research(Dm.text("[] has been removed from experimental nodes through the BEPIS techweb's \"remove tech\" feature.", actual_node.display_name));
        return dot;
    }
}
