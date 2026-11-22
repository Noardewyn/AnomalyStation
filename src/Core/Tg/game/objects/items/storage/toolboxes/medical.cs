// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps package P1 (D298): the medical and coroner toolboxes, the rare stand-ins of Meta's surgery tray spawners; the whole file.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/storage/toolboxes/medical.dm:1
public partial class obj_item_storage_toolbox_medical : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/medical.dm:15
    public override async DmTask PopulateContents()
    {
        atom_storage.max_slots = 0;
        atom_storage.max_total_storage = 0;

        atom fake_tray = (atom)Dm.@new(tray_type, null);
        foreach (obj_item tool in Dm.each<obj_item>(fake_tray.contents.Copy())) // DM iterates a copy of the contents
        {
            await tool.forceMove(this);
            atom_storage.max_slots += 1;
            atom_storage.max_total_storage += tool.w_class;
        }

        qdel(fake_tray);
    }
}

// tg: code/game/objects/items/storage/toolboxes/medical.dm:36
public partial class obj_item_storage_toolbox_medical_coroner : obj_item_storage_toolbox_medical
{
    // tg: code/game/objects/items/storage/toolboxes/medical.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_UNDEAD), Dm.named("damage_multiplier", 2))); //Just in case one of the tennants get uppity
        return dot;
    }
}
