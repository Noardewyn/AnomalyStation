// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/***************** MECHA ACTIONS *****************/

// tg: code/modules/vehicles/mecha/mecha_actions.dm:3
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:3
    public override object generate_action_type(Type actiontype = null)
    {
        object dot = base.generate_action_type(actiontype);
        if (dot is datum_action_vehicle_sealed_mecha mecha_action)
            mecha_action.set_chassis(this);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:110
    public virtual void toggle_strafe()
    {
        if (!Dm.truthy(Dm.bitand(mecha_flags, CAN_STRAFE)))
        {
            to_chat(occupants, I18n.T("this mecha doesn't support strafing!"));
            return;
        }

        strafe = Dm.truthy(strafe) ? 0 : 1;

        foreach (mob occupant in Dm.each<mob>(occupants))
        {
            balloon_alert(occupant, I18n.T("strafing []", Dm.truthy(strafe) ? "on" : "off"));
            occupant.playsound_local(this, Dm.resource("sound/machines/terminal/terminal_eject.ogg"), 50, TRUE);
        }
        log_message(Dm.text("Toggled strafing mode [].", Dm.truthy(strafe) ? "on" : "off"), LOG_MECHA);

        foreach (object occupant in Dm.each<object>(occupants))
        {
            datum_action action = (LAZYACCESS(occupant_actions, occupant) as DmList)?[typeof(datum_action_vehicle_sealed_mecha_strafe)] as datum_action;
            if (action != null)
                Waiting.Detach(action.build_all_button_icons());
        }
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:9
public partial class datum_action_vehicle_sealed_mecha : datum_action_vehicle_sealed
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:13
    public override object Destroy(float? force = null)
    {
        chassis = null;
        return base.Destroy(force);
    }

    ///Sets the chassis var of our mecha action to the referenced mecha. Used during actions generation in
    ///generate_action_type() chain.
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:19
    public virtual void set_chassis(obj_vehicle_sealed_mecha passed_chassis = null)
    {
        chassis = passed_chassis;
    }

    // Each action's guard: the chassis is there and its owner sits in it.
    protected bool chassis_holds_owner() => chassis != null && Dm.@in(owner, chassis.occupants);
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:22
public partial class datum_action_vehicle_sealed_mecha_mech_eject : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:26
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;
        await chassis.container_resist_act(owner as mob_living);
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:34
public partial class datum_action_vehicle_sealed_mecha_mech_toggle_cabin_seal : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:39
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;
        await chassis.set_cabin_seal(owner, Dm.truthy(chassis.cabin_sealed) ? FALSE : TRUE);
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:47
public partial class datum_action_vehicle_sealed_mecha_mech_toggle_lights : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:51
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;
        await chassis.toggle_lights(user: owner);
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:59
public partial class datum_action_vehicle_sealed_mecha_mech_view_stats : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:63
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;

        await chassis.ui_interact(owner);
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:72
public partial class datum_action_vehicle_sealed_mecha_mech_toggle_safeties : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:76
    public override void set_chassis(obj_vehicle_sealed_mecha passed_chassis = null)
    {
        base.set_chassis(passed_chassis);
        RegisterSignal(chassis, COMSIG_MECH_SAFETIES_TOGGLE, nameof(update_action_icon));
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:80
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;

        chassis.set_safety(owner);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:89
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        button_icon_state = Dm.text("mech_safeties_[]", Dm.truthy(chassis.weapons_safety) ? "on" : "off");
        base.apply_button_icon(current_button, force);
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:93
    public virtual void update_action_icon() => Waiting.Detach(build_all_button_icons());
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:97
public partial class datum_action_vehicle_sealed_mecha_strafe : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:101
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;

        chassis.toggle_strafe();
        return dot;
    }
}

/// Swap seats, for two person mecha
// tg: code/modules/vehicles/mecha/mecha_actions.dm:127
public partial class datum_action_vehicle_sealed_mecha_swap_seat : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:131
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;

        if (Dm.eq(chassis.occupants.Count, chassis.max_occupants))
        {
            chassis.balloon_alert(owner, I18n.T("other seat occupied!"));
            return dot;
        }
        DmList drivers = chassis.return_drivers();
        chassis.balloon_alert(owner, I18n.T("moving to other seat..."));
        chassis.is_currently_ejecting = TRUE;
        if (!Dm.truthy(await do_after(owner, Dm.truthy(chassis.has_gravity()) ? chassis.exit_delay : 0, chassis)))
        {
            chassis.balloon_alert(owner, I18n.T("interrupted!"));
            chassis.is_currently_ejecting = FALSE;
            return dot;
        }
        chassis.is_currently_ejecting = FALSE;
        if (Dm.@in(owner, drivers))
        {
            chassis.balloon_alert(owner, I18n.T("controlling gunner seat"));
            chassis.remove_control_flags(owner, Dm.bitor(VEHICLE_CONTROL_DRIVE, VEHICLE_CONTROL_SETTINGS));
            await chassis.add_control_flags(owner, Dm.bitor(VEHICLE_CONTROL_MELEE, VEHICLE_CONTROL_EQUIPMENT));
            chassis.remove_all_equipment_actions(owner);
        }
        else
        {
            chassis.balloon_alert(owner, I18n.T("controlling pilot seat"));
            chassis.remove_control_flags(owner, Dm.bitor(VEHICLE_CONTROL_MELEE, VEHICLE_CONTROL_EQUIPMENT));
            await chassis.add_control_flags(owner, Dm.bitor(VEHICLE_CONTROL_DRIVE, VEHICLE_CONTROL_SETTINGS));
            await chassis.generate_equipment_actions(owner);
        }
        await chassis.update_icon_state();
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:161
public partial class datum_action_vehicle_sealed_mecha_mech_overclock : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:170
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object forced_state = __extra.Length > 0 ? __extra[0] : null;
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner())
            return dot;
        await chassis.toggle_overclock(forced_state);
        await build_all_button_icons();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:179
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        button_icon_state = get_button_icon_state() as string;
        base.apply_button_icon(current_button, force);
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:183
    public virtual object get_button_icon_state() => Dm.text("mech_overload_[]", Dm.truthy(chassis.overclock_mode) ? "on" : "off");
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:186
public partial class datum_action_vehicle_sealed_mecha_equipment : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:192
    public override object Destroy(float? force = null)
    {
        equipment = null;
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:196
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!chassis_holds_owner() || equipment == null)
            return dot;

        await equipment.set_active(Dm.truthy(equipment.active) ? FALSE : TRUE);
        await equipment.handle_ui_act("toggle");
        chassis.balloon_alert(owner, I18n.T("[] []!", equipment.name, Dm.truthy(equipment.active) ? "on" : "off"));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:207
    public virtual async DmTask set_equipment(obj_item_mecha_parts_mecha_equipment passed_equipment = null)
    {
        equipment = passed_equipment;
        name = I18n.T("Toggle []", equipment.name);
        desc = equipment.desc;
        target = equipment;
        if (target != null)
            await AddComponent(typeof(datum_component_action_item_overlay), equipment);

        await build_button_icon();
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:217
public partial class datum_action_vehicle_sealed_mecha_equipment_cargo_module : datum_action_vehicle_sealed_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:220
    public override async DmTask set_equipment(obj_item_mecha_parts_mecha_equipment passed_equipment = null)
    {
        await base.set_equipment(passed_equipment);
        name = Dm.text("[]", equipment.name);
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:224
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        //We are snowflaked from parent
        if (!chassis_holds_owner() || equipment == null)
            return null;
        if (!istype(equipment, typeof(obj_item_mecha_parts_mecha_equipment_ejector)))
            return null;

        obj_item_mecha_parts_mecha_equipment_ejector cargo_hold = equipment as obj_item_mecha_parts_mecha_equipment_ejector;

        // Right click - show radial menu
        if (Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_SECONDARY_ACTION)))
        {
            DmList cargo_radial = Dm.list();
            foreach (atom_movable cargo_item in Dm.each<atom_movable>(cargo_hold.contents))
                cargo_radial[cargo_item] = cargo_item.appearance;

            if (!Dm.truthy(length(cargo_radial)))
            {
                chassis.balloon_alert(owner, I18n.T("cargo hold empty!"));
                return null;
            }

            atom_movable picked_item = await show_radial_menu(owner, chassis, cargo_radial, require_near: TRUE) as atom_movable;
            if (picked_item == null || !Dm.@in(picked_item, cargo_hold.contents))
                return null;

            to_chat(chassis.occupants, span_notice(I18n.T("You unload [].", picked_item)));
            await picked_item.forceMove(cargo_hold.drop_location() as atom);
            if (picked_item == chassis.ore_box)
                chassis.ore_box = null;
            playsound(chassis, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
            cargo_hold.log_message(Dm.text("Unloaded []. Cargo compartment capacity: []", picked_item, cargo_hold.cargo_capacity - cargo_hold.contents.len), LOG_MECHA);
            return null;
        }

        // Left click - dispose first item
        if (cargo_hold.contents.Count != 0)
        {
            atom_movable first_item = cargo_hold.contents[1] as atom_movable;
            to_chat(chassis.occupants, span_notice(I18n.T("You unload [].", first_item)));
            await first_item.forceMove(cargo_hold.drop_location() as atom);
            if (first_item == chassis.ore_box)
                chassis.ore_box = null;
            playsound(chassis, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
            cargo_hold.log_message(Dm.text("Unloaded []. Cargo compartment capacity: []", first_item, cargo_hold.cargo_capacity - cargo_hold.contents.len), LOG_MECHA);
        }
        else
            chassis.balloon_alert(owner, I18n.T("cargo hold empty!"));
        return null;
    }
}

// tg: code/modules/vehicles/mecha/mecha_actions.dm:267
public partial class datum_action_vehicle_sealed_mecha_equipment_extinguisher_action : datum_action_vehicle_sealed_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/mecha_actions.dm:270
    public override async DmTask set_equipment(obj_item_mecha_parts_mecha_equipment passed_equipment = null)
    {
        await base.set_equipment(passed_equipment);
        name = Dm.text("[]", equipment.name);
    }

    // tg: code/modules/vehicles/mecha/mecha_actions.dm:274
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        //We are snowflaked from parent
        if (!chassis_holds_owner() || equipment == null)
            return null;
        if (!istype(equipment, typeof(obj_item_mecha_parts_mecha_equipment_extinguisher)))
            return null;

        obj_item_mecha_parts_mecha_equipment_extinguisher extinguisher = equipment as obj_item_mecha_parts_mecha_equipment_extinguisher;

        // Right click - refill
        if (Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_SECONDARY_ACTION)))
        {
            await extinguisher.attempt_refill(owner);
            return null;
        }

        // Left click - spray
        if (Dm.num((extinguisher.reagents as datum_reagents).total_volume) < Dm.num(extinguisher.required_amount))
        {
            chassis.balloon_alert(owner, I18n.T("not enough water!"));
            return null;
        }

        await extinguisher.spray_extinguisher(owner);
        return null;
    }
}
