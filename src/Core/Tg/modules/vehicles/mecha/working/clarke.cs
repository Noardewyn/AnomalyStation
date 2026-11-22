// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/working/clarke.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Lavaproof, fireproof, fast mech with low armor and higher energy consumption and has an internal ore box.
// tg: code/modules/vehicles/mecha/working/clarke.dm:2
public partial class obj_vehicle_sealed_mecha_clarke : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/working/clarke.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ore_box = Dm.@new<obj_structure_ore_box>(this);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/clarke.dm:43
    public override void generate_actions()
    {
        base.generate_actions();
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_search_ruins));
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_clarke_scoop_body));
    }
}

// Ore Box Controls
/// Special equipment for the Clarke mech, handles moving ore without giving the mech a hydraulic clamp and cargo compartment.
// tg: code/modules/vehicles/mecha/working/clarke.dm:50
public partial class obj_item_mecha_parts_mecha_equipment_orebox_manager : obj_item_mecha_parts_mecha_equipment
{
    // tg: code/modules/vehicles/mecha/working/clarke.dm:57
    public override async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        await base.attach(new_mecha, attach_right);
        ADD_TRAIT(chassis, TRAIT_OREBOX_FUNCTIONAL, TRAIT_MECH_EQUIPMENT(type));
    }

    // tg: code/modules/vehicles/mecha/working/clarke.dm:61
    public override async DmTask detach(atom moveto = null)
    {
        REMOVE_TRAIT(chassis, TRAIT_OREBOX_FUNCTIONAL, TRAIT_MECH_EQUIPMENT(type));
        await base.detach(moveto);
    }

    // tg: code/modules/vehicles/mecha/working/clarke.dm:65
    public override DmList get_snowflake_data()
    {
        DmList box_contents = chassis.ore_box?.contents;
        DmList contents_grouped = Dm.list();
        foreach (atom_movable item in Dm.each<atom_movable>(box_contents))
        {
            float amount = 1;
            if (isstack(item))
                amount = Dm.num((item as obj_item_stack).amount);
            if (contents_grouped[item.icon_state] == null)
            {
                DmList ore_data = Dm.list();
                ore_data["name"] = item.name;
                ore_data["icon"] = item.icon_state;
                ore_data["amount"] = amount;
                contents_grouped[item.icon_state] = ore_data;
            }
            else
            {
                DmList ore_data = contents_grouped[item.icon_state] as DmList;
                ore_data["amount"] = Dm.num(ore_data["amount"]) + amount;
            }
        }
        return Dm.list(
            Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_OREBOX_MANAGER),
            Dm.kv("contents", contents_grouped));
    }

    // tg: code/modules/vehicles/mecha/working/clarke.dm:87
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        if (Dm.eq(action, "dump"))
        {
            obj_structure_ore_box cached_ore_box = chassis.ore_box;
            if (cached_ore_box == null)
                return FALSE;
            await cached_ore_box.dump_box_contents();
            playsound(chassis, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
            log_message(Dm.text("Dumped [].", cached_ore_box), LOG_MECHA);
            return TRUE;
        }
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/working/clarke.dm:104
public partial class datum_action_vehicle_sealed_mecha_clarke_scoop_body : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/working/clarke.dm:110
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_mecha_parts_mecha_equipment_sleeper_clarke sleeper = Dm.locate<obj_item_mecha_parts_mecha_equipment_sleeper_clarke>(chassis.contents);
        mob_living_carbon_human human_target = null;
        foreach (mob_living_carbon_human body in Dm.each<mob_living_carbon_human>(range(1, chassis)))
        {
            if (Dm.truthy(chassis.is_driver(body)) || !ishuman(body) || !Dm.truthy(await chassis.Adjacent(body)))
                continue;
            human_target = body; //Non-driver, human, and adjacent
            break;
        }
        await sleeper.action(pick(chassis.return_drivers()) as mob, human_target); //This will probably break if anyone allows multiple drivers of the Clarke mech
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/working/clarke.dm:125
public partial class datum_action_vehicle_sealed_mecha_mech_search_ruins : datum_action_vehicle_sealed_mecha
{
    private const float SEARCH_COOLDOWN = 1 * MINUTES;

    // tg: code/modules/vehicles/mecha/working/clarke.dm:130
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (chassis == null || !Dm.@in(owner, chassis.occupants))
            return dot;
        if (!(search_cooldown <= world.time))
        {
            chassis.balloon_alert(owner, I18n.T("on cooldown!"));
            return dot;
        }
        if (!isliving(owner))
            return dot;
        mob_living living_owner = owner as mob_living;
        button_icon_state = "mech_search_ruins_cooldown";
        await build_all_button_icons();
        search_cooldown = world.time + SEARCH_COOLDOWN;
        _addtimer(Dm.@new<datum_callback>(this, nameof(search_ready)), SEARCH_COOLDOWN, file: "code/modules/vehicles/mecha/working/clarke.dm", line: 145);
        obj pinpointed_ruin = null;
        foreach (obj_effect_landmark_ruin ruin_landmark in Dm.each<obj_effect_landmark_ruin>(GLOB.ruin_landmarks))
        {
            if (!Dm.eq(ruin_landmark.z, chassis.z))
                continue;
            if (pinpointed_ruin == null || Dm.num(get_dist(ruin_landmark, chassis)) < Dm.num(get_dist(pinpointed_ruin, chassis)))
                pinpointed_ruin = ruin_landmark;
        }
        if (pinpointed_ruin == null)
        {
            chassis.balloon_alert(living_owner, I18n.T("no ruins!"));
            return dot;
        }
        datum_status_effect_agent_pinpointer ruin_pinpointer = await living_owner.apply_status_effect(typeof(datum_status_effect_agent_pinpointer_ruin)) as datum_status_effect_agent_pinpointer;
        ruin_pinpointer.RegisterSignal(living_owner, COMSIG_MOVABLE_MOVED, nameof(datum_status_effect_agent_pinpointer_ruin.cancel_self));
        ruin_pinpointer.scan_target = pinpointed_ruin;
        chassis.balloon_alert(living_owner, I18n.T("pinpointing nearest ruin"));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/working/clarke.dm:145  the two timers, VARSET_CALLBACK and build_all_button_icons
    public virtual async DmTask search_ready()
    {
        button_icon_state = "mech_search_ruins";
        await build_all_button_icons();
    }
}

// tg: code/modules/vehicles/mecha/working/clarke.dm:161
public partial class datum_status_effect_agent_pinpointer_ruin : datum_status_effect_agent_pinpointer
{
    // tg: code/modules/vehicles/mecha/working/clarke.dm:170
    public override void scan_for_target()
    {
    }

    // tg: code/modules/vehicles/mecha/working/clarke.dm:173
    public virtual void cancel_self(datum source = null, atom old_loc = null) => qdel(this);
}
