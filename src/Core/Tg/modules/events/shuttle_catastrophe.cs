// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/shuttle_catastrophe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E3 (D327): the whole file but its admin options (the warning and the admin's pick of the shuttle, /datum/event_admin_setup/*: the
// admins' Trigger Event panel, D275): the shuttle catastrophe (the emergency shuttle is replaced by another purchasable one, or the
// shuttle insurance pays the cargo budget). The replacement is picked from SSmapping's purchasable templates, which are the shuttle maps
// the host mounted (DV188). The var blocks are generated (D111).

// tg: code/modules/events/shuttle_catastrophe.dm:1
public partial class datum_round_event_control_shuttle_catastrophe : datum_round_event_control
{
    // tg: code/modules/events/shuttle_catastrophe.dm:10
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.eq(SSshuttle.shuttle_purchased, SHUTTLEPURCHASE_FORCED))
            return FALSE; //don't do it if its already been done
        if (SSshuttle.emergency is obj_docking_port_mobile_emergency_shuttle_build)
            return FALSE; //don't undo manual player engineering, it also would unload people and ghost them, there's just a lot of problems
        if (datum_controller_subsystem_shuttle.EMERGENCY_AT_LEAST_DOCKED())
            return FALSE; //don't remove all players when its already on station or going to centcom
        return TRUE;
    }
}

// tg: code/modules/events/shuttle_catastrophe.dm:23
public partial class datum_round_event_shuttle_catastrophe : datum_round_event
{
    // tg: code/modules/events/shuttle_catastrophe.dm:26
    public override async DmTask announce__proc(object fake = null)
    {
        object cause = pick(Dm.text("was attacked by [] Operatives", syndicate_name()), "mysteriously teleported away", "had its refuelling crew mutiny",
            "was found with its engines stolen", "[REDACTED]", "flew into the sunset, and melted", "learned something from a very wise cow, and left on its own",
            "had cloning devices on it", "had its shuttle inspector put the shuttle in reverse instead of park, causing the shuttle to crash into the hangar");
        string message = Dm.text("Your emergency shuttle []. ", cause);

        if (Dm.truthy(SSshuttle.shuttle_insurance))
        {
            message += "Luckily, your shuttle insurance has covered the costs of repair!";
            if (SSeconomy.get_dep_account(ACCOUNT_CAR) != null)
                message += Dm.text(" You have been awarded a bonus from [] for smart spending.", command_name());
        }
        else
        {
            message += Dm.text("Your replacement shuttle will be the [] until further notice.", new_shuttle.name);
        }
        priority_announce(message, Dm.text("[] Spacecraft Engineering", command_name()));
    }

    // tg: code/modules/events/shuttle_catastrophe.dm:40
    public override async DmTask<object> setup()
    {
        if (Dm.truthy(SSshuttle.shuttle_insurance) || new_shuttle != null) //If an admin has overridden it don't re-roll it
            return null;
        DmList valid_shuttle_templates = Dm.list();
        foreach (object shuttle_id in Dm.each(SSmapping.shuttle_templates))
        {
            datum_map_template_shuttle template = SSmapping.shuttle_templates[shuttle_id] as datum_map_template_shuttle;
            if (template.who_can_purchase != null && template.credit_cost < INFINITY) //if we could get it from the communications console, it's cool for us to get it here
                Dm.append(ref valid_shuttle_templates, template);
        }
        new_shuttle = pick(valid_shuttle_templates) as datum_map_template_shuttle;
        return null;
    }

    // tg: code/modules/events/shuttle_catastrophe.dm:50
    public override async DmTask<object> start()
    {
        if (Dm.truthy(SSshuttle.shuttle_insurance))
        {
            datum_bank_account station_balance = SSeconomy.get_dep_account(ACCOUNT_CAR);
            station_balance?.adjust_money(8000);
            return null;
        }
        SSshuttle.shuttle_purchased = SHUTTLEPURCHASE_FORCED;
        await SSshuttle.unload_preview();
        // We need to move our docking port back in case a crashlanding shuttle has been purchased previously
        foreach (obj_docking_port_stationary port in Dm.each_cast<obj_docking_port_stationary>(SSshuttle.stationary_docking_ports))
        {
            if (!Dm.eq(port.shuttle_id, "emergency_home"))
                continue;
            turf initial_loc = locate(port.initial_x, port.initial_y, port.initial_z) as turf;
            await port.forceMove(initial_loc);
            break;
        }
        SSshuttle.existing_shuttle = SSshuttle.emergency;
        await SSshuttle.action_load(new_shuttle, replace: TRUE);
        log_shuttle(Dm.text("Shuttle Catastrophe set a new shuttle, [].", new_shuttle.name));
        return null;
    }
}
