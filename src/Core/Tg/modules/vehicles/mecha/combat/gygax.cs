// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/gygax.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/combat/gygax.dm:76
public partial class obj_vehicle_sealed_mecha_gygax_dark : obj_vehicle_sealed_mecha_gygax
{
    // tg: code/modules/vehicles/mecha/combat/gygax.dm:76
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // add_minimap_blip(src, MINIMAP_SYNDICATE_MECH_BLIP, "syndiemech"): the station map's blip, the minimap is not ported
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/combat/gygax.dm:80
public partial class obj_vehicle_sealed_mecha_gygax_dark_loaded : obj_vehicle_sealed_mecha_gygax_dark
{
    // tg: code/modules/vehicles/mecha/combat/gygax.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        max_ammo();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/gygax.dm:84
    public override void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_bluespace>(this);
        scanmod = Dm.@new<obj_item_stock_parts_scanning_module_triphasic>(this);
        capacitor = Dm.@new<obj_item_stock_parts_capacitor_quadratic>(this);
        servo = Dm.@new<obj_item_stock_parts_servo_femto>(this);
        update_part_values();
    }
}
