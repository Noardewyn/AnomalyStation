// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/marauder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/combat/marauder.dm:1
public partial class obj_vehicle_sealed_mecha_marauder : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/marauder.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        smoke_system = Dm.@new<datum_effect_system_fluid_spread_smoke>(this, 3, null, this);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/marauder.dm:42
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref smoke_system);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/combat/marauder.dm:46
    public override void generate_actions()
    {
        base.generate_actions();
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_smoke));
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_zoom));
    }

    // tg: code/modules/vehicles/mecha/combat/marauder.dm:67
    public override async DmTask<object> remove_occupant(mob M = null)
    {
        object dot = await base.remove_occupant(M);
        zoom_mode = FALSE;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/marauder.dm:71
    public override object can_move(object direction = null)
    {
        object dot = base.can_move(direction);
        if (!Dm.truthy(dot) || !Dm.truthy(zoom_mode))
            return dot;

        if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_MECHA_MESSAGE)))
        {
            to_chat(occupants, span_warning(I18n.T("Unable to move while in zoom mode!")));
            timer_cooldown_start(COOLDOWN_MECHA_MESSAGE, 2 * SECONDS);
        }
        return FALSE;
    }
}

// tg: code/modules/vehicles/mecha/combat/marauder.dm:60
public partial class obj_vehicle_sealed_mecha_marauder_loaded : obj_vehicle_sealed_mecha_marauder
{
    // tg: code/modules/vehicles/mecha/combat/marauder.dm:60
    public override void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_bluespace>(this);
        scanmod = Dm.@new<obj_item_stock_parts_scanning_module_triphasic>(this);
        capacitor = Dm.@new<obj_item_stock_parts_capacitor_quadratic>(this);
        servo = Dm.@new<obj_item_stock_parts_servo_femto>(this);
        update_part_values();
    }
}

// tg: code/modules/vehicles/mecha/combat/marauder.dm:81
public partial class datum_action_vehicle_sealed_mecha_mech_smoke : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/marauder.dm:85
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;

        obj_vehicle_sealed_mecha_marauder maradeur = chassis as obj_vehicle_sealed_mecha_marauder;
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(maradeur, COOLDOWN_MECHA_SMOKE)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("smoke charges on cooldown!"));
            return FALSE;
        }

        if (!Dm.truthy(maradeur.smoke_charges))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("out of smoke charges!"));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/marauder.dm:101
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        obj_vehicle_sealed_mecha_marauder maradeur = chassis as obj_vehicle_sealed_mecha_marauder;
        if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(maradeur, COOLDOWN_MECHA_SMOKE)) && Dm.truthy(maradeur.smoke_charges))
        {
            await maradeur.smoke_system.start();
            maradeur.smoke_charges--;
            maradeur.timer_cooldown_start(COOLDOWN_MECHA_SMOKE, maradeur.smoke_cooldown);
        }
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/combat/marauder.dm:113
public partial class datum_action_vehicle_sealed_mecha_mech_zoom : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/marauder.dm:117
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (owner.client == null || chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        obj_vehicle_sealed_mecha_marauder maradeur = chassis as obj_vehicle_sealed_mecha_marauder;
        maradeur.zoom_mode = Dm.truthy(maradeur.zoom_mode) ? 0 : 1;
        button_icon_state = Dm.text("mech_zoom_[]", Dm.truthy(maradeur.zoom_mode) ? "on" : "off");
        maradeur.log_message("Toggled zoom mode.", LOG_MECHA);
        to_chat(owner, Dm.text("<font color='[]'>[]</font>", Dm.truthy(maradeur.zoom_mode) ? "blue" : "red", I18n.T("Zoom mode []abled.", Dm.truthy(maradeur.zoom_mode) ? "en" : "dis")));
        // owner.client.view_size.setTo(4.5) / resetToDefault(): a tile-map view size has no first-person counterpart; the mode's movement lock and sound stay
        if (Dm.truthy(maradeur.zoom_mode))
            Dm.output_to(owner, sound(Dm.resource("sound/vehicles/mecha/imag_enh.ogg"), volume: 50));
        await build_all_button_icons();
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/combat/marauder.dm:213
public partial class obj_vehicle_sealed_mecha_marauder_mauler : obj_vehicle_sealed_mecha_marauder
{
    // tg: code/modules/vehicles/mecha/combat/marauder.dm:213
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // add_minimap_blip(src, MINIMAP_SYNDICATE_MECH_BLIP, "syndiemech"): the station map's blip, the minimap is not ported
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/combat/marauder.dm:192
public partial class obj_vehicle_sealed_mecha_marauder_mauler_loaded : obj_vehicle_sealed_mecha_marauder_mauler
{
    // tg: code/modules/vehicles/mecha/combat/marauder.dm:205
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        max_ammo();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/marauder.dm:209
    public override void populate_parts()
    {
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_bluespace>(this);
        scanmod = Dm.@new<obj_item_stock_parts_scanning_module_triphasic>(this);
        capacitor = Dm.@new<obj_item_stock_parts_capacitor_quadratic>(this);
        servo = Dm.@new<obj_item_stock_parts_servo_femto>(this);
        update_part_values();
    }
}
