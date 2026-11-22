// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/research_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD2, D333: the R&D consoles and servers): the technology disk the R&D console reads into its techweb
// or writes the web's researched nodes to (it starts with the starter nodes), and CentCom's debug disk with the admin web's nodes.
// The var blocks are generated (D111).

// tg: code/modules/research/research_disk.dm:1
public partial class obj_item_disk_tech_disk : obj_item_disk
{
    // tg: code/modules/research/research_disk.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (stored_nodes == null)
        {
            // gotta make it assoc or else it breaks
            stored_nodes = Dm.list();
            foreach (object node_path in Dm.each(SSresearch.techweb_nodes_starting))
                stored_nodes[node_path] = TRUE;
        }
        pixel_x = base_pixel_x + rand(-5, 5);
        pixel_y = base_pixel_y + rand(-5, 5);
        return dot;
    }
}

// tg: code/modules/research/research_disk.dm:18
public partial class obj_item_disk_tech_disk_debug : obj_item_disk_tech_disk
{
    // tg: code/modules/research/research_disk.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_techweb_admin admin_techweb = Dm.locate<datum_techweb_admin>(SSresearch.techwebs);
        admin_techweb ??= Dm.@new<datum_techweb_admin>();
        stored_nodes = admin_techweb.researched_nodes.Copy();
        return base.Initialize(mapload, __extra);
    }
}
