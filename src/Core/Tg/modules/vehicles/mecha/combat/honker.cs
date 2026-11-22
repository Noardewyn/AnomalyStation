// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/honker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/combat/honker.dm:1
public partial class obj_vehicle_sealed_mecha_honker : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/honker.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(AddElementTrait(TRAIT_WADDLING, REF(this), typeof(datum_element_waddling)));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/honker.dm:34
    public override void play_stepsound()
    {
        if (Dm.truthy(squeak))
            playsound(this, SFX_CLOWN_STEP, 70, 1);
        squeak = Dm.truthy(squeak) ? 0 : 1;
    }
}

// tg: code/modules/vehicles/mecha/combat/honker.dm:63
public partial class obj_vehicle_sealed_mecha_honker_dark : obj_vehicle_sealed_mecha_honker
{
    // tg: code/modules/vehicles/mecha/combat/honker.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // add_minimap_blip(src, MINIMAP_SYNDICATE_MECH_BLIP, "syndiemech"): the station map's blip, the minimap is not ported
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/combat/honker.dm:76
public partial class obj_vehicle_sealed_mecha_honker_dark_loaded : obj_vehicle_sealed_mecha_honker_dark
{
    // tg: code/modules/vehicles/mecha/combat/honker.dm:76
    public override void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_hyper>(this);
        scanmod = Dm.@new<obj_item_stock_parts_scanning_module_phasic>(this);
        capacitor = Dm.@new<obj_item_stock_parts_capacitor_super>(this);
        servo = Dm.@new<obj_item_stock_parts_servo_pico>(this);
        update_part_values();
    }
}
