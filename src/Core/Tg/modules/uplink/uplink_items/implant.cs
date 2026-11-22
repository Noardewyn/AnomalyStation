// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/implant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks). The uplink implant
// waits for G3's implanter (DV343: UplinkWaits).

// tg: code/modules/uplink/uplink_items/implant.dm:11
public partial class datum_uplink_item_implants_freedom : datum_uplink_item_implants
{
    // tg: code/modules/uplink/uplink_items/implant.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        desc += Dm.text(" Implant has enough energy for [] uses before it becomes inert and harmlessly self-destructs.", FREEDOM_IMPLANT_CHARGES);
    }
}

// tg: code/modules/uplink/uplink_items/implant.dm:44
public partial class datum_uplink_item_implants_uplink : datum_uplink_item_implants
{
    // tg: code/modules/uplink/uplink_items/implant.dm:54
    public override async DmTask<object> spawn_item(object spawn_path = null, mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        obj_item_storage_box_syndie_kit uplink_box = await base.spawn_item(spawn_path, user, uplink_handler, source) as obj_item_storage_box_syndie_kit;
        uplink_box.name = I18n.T("Uplink Implant Box");
        await Dm.new_async<obj_item_implanter_uplink>(uplink_box, uplink_handler);
        return uplink_box;
    }
}
