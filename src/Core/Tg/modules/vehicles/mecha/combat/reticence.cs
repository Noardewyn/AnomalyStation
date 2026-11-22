// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/reticence.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/combat/reticence.dm:29
public partial class obj_vehicle_sealed_mecha_reticence_loaded : obj_vehicle_sealed_mecha_reticence
{
    // tg: code/modules/vehicles/mecha/combat/reticence.dm:38
    public override void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_bluespace>(this);
        scanmod = Dm.@new<obj_item_stock_parts_scanning_module_phasic>(this);
        capacitor = Dm.@new<obj_item_stock_parts_capacitor_super>(this);
        servo = Dm.@new<obj_item_stock_parts_servo_pico>(this);
        update_part_values();
    }
}
