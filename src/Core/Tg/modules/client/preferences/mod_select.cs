// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/mod_select.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The game preference that picks the mouse button (middle or alt click) a MODsuit's active module
// is used with; changing it rebinds the module the wearer has selected. The var block is generated (D111).

/// Switches between mouse buttons for MODsuit active modules
// tg: code/modules/client/preferences/mod_select.dm:2
public partial class datum_preference_choiced_mod_select : datum_preference_choiced
{
    // tg: code/modules/client/preferences/mod_select.dm:7
    public override DmList init_possible_values()
    {
        return Dm.list(MIDDLE_CLICK, ALT_CLICK);
    }

    // tg: code/modules/client/preferences/mod_select.dm:10
    public override object create_default_value()
    {
        return MIDDLE_CLICK;
    }

    // tg: code/modules/client/preferences/mod_select.dm:13
    public override void apply_to_client_updated(client client = null, object value = null)
    {
        if (!ishuman(client.mob))
            return;
        mob_living_carbon_human client_owner = client.mob as mob_living_carbon_human;
        if (!istype(client_owner.back, typeof(obj_item_mod_control)))
            return;
        obj_item_mod_control mod = client_owner.back as obj_item_mod_control;
        if (mod.selected_module == null)
            return;
        mod.selected_module.UnregisterSignal(mod.wearer, mod.selected_module.used_signal);
        mod.selected_module.update_signal(value);
    }
}
