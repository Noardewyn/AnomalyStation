// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/construction_console/construction_console_centcom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///admin-only base consturctino console subtype for building anywhere!
// tg: code/game/objects/structures/construction_console/construction_console_centcom.dm:3
public partial class obj_machinery_computer_camera_advanced_base_construction_centcom : obj_machinery_computer_camera_advanced_base_construction
{
    // tg: code/game/objects/structures/construction_console/construction_console_centcom.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        internal_rcd = Dm.@new<obj_item_construction_rcd_internal>(this);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/structures/construction_console/construction_console_centcom.dm:11
    public override void restock_materials()
    {
        internal_rcd.matter = internal_rcd.max_matter;
    }

    // tg: code/game/objects/structures/construction_console/construction_console_centcom.dm:14
    public override void populate_actions_list()
    {
        Dm.append(ref actions, Dm.@new<datum_action_innate_construction_configure_mode>(this)); //Action for switching the RCD's build modes
        Dm.append(ref actions, Dm.@new<datum_action_innate_construction_build>(this)); //Action for using the RCD
    }
}
