// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/vehicle_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the vehicles' default handling and the action datums the exosuits use translated; the clown car's, the bicycle's and the
// wheelchair's come with those vehicles.

//VEHICLE DEFAULT HANDLING

// tg: code/modules/vehicles/vehicle_actions.dm:1
public partial class obj_vehicle : obj
{
    /**
     * ## generate_actions
     *
     * You override this with initialize_passenger_action_type and initialize_controller_action_type calls
     * To give passengers actions when they enter the vehicle.
     * Read the documentation on the aforementioned procs to learn the difference
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:10
    public virtual void generate_actions()
    {
    }

    /**
     * ## generate_action_type
     *
     * A small proc to properly set up each action path.
     * args:
     * * actiontype: typepath of the action the proc sets up.
     * returns created and set up action instance
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:21
    public virtual object generate_action_type(Type actiontype = null)
    {
        datum_action_vehicle A = Dm.@new(actiontype) as datum_action_vehicle;
        if (A == null)
            return null;
        A.vehicle_target = this;
        return A;
    }

    /**
     * ## initialize_passenger_action_type
     *
     * Gives any passenger that enters the mech this action.
     * They will lose it when they disembark.
     * args:
     * * actiontype: typepath of the action you want to give occupants.
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:36
    public virtual void initialize_passenger_action_type(Type actiontype = null)
    {
        autogrant_actions_passenger.Add(actiontype);
        foreach (mob i in Dm.each<mob>(occupants))
            Waiting.Detach(grant_passenger_actions(i)); //refresh
    }

    /**
     * ## destroy_passenger_action_type
     *
     * Removes this action type from all occupants and stops autogranting it
     * args:
     * * actiontype: typepath of the action you want to remove from occupants and the autogrant list.
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:48
    public virtual void destroy_passenger_action_type(Type actiontype = null)
    {
        Dm.remove(autogrant_actions_passenger, actiontype);
        foreach (mob i in Dm.each<mob>(occupants))
            remove_action_type_from_mob(actiontype, i);
    }

    /**
     * ## initialize_controller_action_type
     *
     * Gives any passenger that enters the vehicle this action... IF they have the correct vehicle control flag.
     * This is used so passengers cannot press buttons only drivers should have, for example.
     * args:
     * * actiontype: typepath of the action you want to give occupants.
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:61
    public virtual void initialize_controller_action_type(Type actiontype = null, object control_flag = null)
    {
        string key = Dm.text("[]", control_flag);
        if (autogrant_actions_controller[key] == null)
            autogrant_actions_controller[key] = Dm.list();
        (autogrant_actions_controller[key] as DmList).Add(actiontype);
        foreach (mob i in Dm.each<mob>(occupants))
            Waiting.Detach(grant_controller_actions(i)); //refresh
    }

    /**
     * ## destroy_controller_action_type
     *
     * As the name implies, removes the actiontype from autogrant and removes it from all occupants
     * args:
     * * actiontype: typepath of the action you want to remove from occupants and autogrant.
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:74
    public virtual void destroy_controller_action_type(Type actiontype = null, object control_flag = null)
    {
        string key = Dm.text("[]", control_flag);
        DmList actions = autogrant_actions_controller[key] as DmList;
        Dm.remove(actions, actiontype);
        if (actions != null && actions.Count == 0)
            autogrant_actions_controller[key] = null; // UNSETEMPTY
        foreach (mob i in Dm.each<mob>(occupants))
            remove_action_type_from_mob(actiontype, i);
    }

    /**
     * ## grant_action_type_to_mob
     *
     * As on the tin, it does all the annoying small stuff and sanity needed
     * to GRANT an action to a mob.
     * args:
     * * actiontype: typepath of the action you want to give to grant_to.
     * * grant_to: the mob we're giving actiontype to
     * returns TRUE if successfully granted
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:90
    public virtual async DmTask<object> grant_action_type_to_mob(Type actiontype = null, mob grant_to = null)
    {
        if (LAZYACCESS(occupants, grant_to) == null || actiontype == null)
            return FALSE;
        if (occupant_actions[grant_to] == null)
            occupant_actions[grant_to] = Dm.list();
        DmList held = occupant_actions[grant_to] as DmList;
        if (Dm.truthy(held[actiontype]))
            return TRUE;
        datum_action action = generate_action_type(actiontype) as datum_action;
        await action.Grant(grant_to);
        held[action.type] = action;
        return TRUE;
    }

    /**
     * ## remove_action_type_from_mob
     *
     * As on the tin, it does all the annoying small stuff and sanity needed
     * to REMOVE an action to a mob.
     * args:
     * * actiontype: typepath of the action you want to give to grant_to.
     * * take_from: the mob we're taking actiontype to
     * returns TRUE if successfully removed
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:109
    public virtual object remove_action_type_from_mob(Type actiontype = null, mob take_from = null)
    {
        if (LAZYACCESS(occupants, take_from) == null || actiontype == null)
            return FALSE;
        if (occupant_actions[take_from] == null)
            occupant_actions[take_from] = Dm.list();
        DmList held = occupant_actions[take_from] as DmList;
        if (Dm.truthy(held[actiontype]))
        {
            datum_action action = held[actiontype] as datum_action;
            // Actions don't dissipate on removal, they just sit around assuming they'll be reusued
            // Gotta qdel
            qdel(action);
            Dm.remove(held, actiontype);
        }
        return TRUE;
    }

    /**
     * ## grant_passenger_actions
     *
     * Called on every passenger that enters the vehicle, goes through the list of actions it needs to give...
     * and does that.
     * args:
     * * grant_to: mob that needs to get every action the vehicle grants
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:129
    public virtual async DmTask grant_passenger_actions(mob grant_to = null)
    {
        foreach (Type v in Dm.each<Type>(autogrant_actions_passenger))
            await grant_action_type_to_mob(v, grant_to);
    }

    /**
     * ## remove_passenger_actions
     *
     * Called on every passenger that exits the vehicle, goes through the list of actions it needs to remove...
     * and does that.
     * args:
     * * take_from: mob that needs to get every action the vehicle grants
     */
    // tg: code/modules/vehicles/vehicle_actions.dm:141
    public virtual void remove_passenger_actions(mob take_from = null)
    {
        foreach (Type v in Dm.each<Type>(autogrant_actions_passenger))
            remove_action_type_from_mob(v, take_from);
    }

    // tg: code/modules/vehicles/vehicle_actions.dm:145
    public virtual async DmTask<object> grant_controller_actions(mob M = null)
    {
        if (!istype(M, typeof(mob)) || LAZYACCESS(occupants, M) == null)
            return FALSE;
        foreach (object i in Dm.each<object>(GLOB.bitflags))
        {
            if (Dm.truthy(Dm.bitand(occupants[M], i)))
                await grant_controller_actions_by_flag(M, i);
        }
        return TRUE;
    }

    // tg: code/modules/vehicles/vehicle_actions.dm:153
    public virtual object remove_controller_actions(mob M = null)
    {
        if (!istype(M, typeof(mob)) || LAZYACCESS(occupants, M) == null)
            return FALSE;
        foreach (object i in Dm.each<object>(GLOB.bitflags))
            remove_controller_actions_by_flag(M, i);
        return TRUE;
    }

    // tg: code/modules/vehicles/vehicle_actions.dm:160
    public virtual async DmTask<object> grant_controller_actions_by_flag(mob M = null, object flag = null)
    {
        if (!istype(M, typeof(mob)))
            return FALSE;
        foreach (Type v in Dm.each<Type>(autogrant_actions_controller[Dm.text("[]", flag)]))
            await grant_action_type_to_mob(v, M);
        return TRUE;
    }

    // tg: code/modules/vehicles/vehicle_actions.dm:166
    public virtual object remove_controller_actions_by_flag(mob M = null, object flag = null)
    {
        if (!istype(M, typeof(mob)))
            return FALSE;
        foreach (Type v in Dm.each<Type>(autogrant_actions_controller[Dm.text("[]", flag)]))
            remove_action_type_from_mob(v, M);
        return TRUE;
    }

    // tg: code/modules/vehicles/vehicle_actions.dm:172
    public virtual object cleanup_actions_for_mob(mob M = null)
    {
        if (!istype(M, typeof(mob)))
            return FALSE;
        DmList held = occupant_actions[M] as DmList;
        foreach (Type path in Dm.each<Type>(held?.Copy()))
        {
            stack_trace(Dm.text("Leftover action type [] in vehicle type [] for mob type [] - THIS SHOULD NOT BE HAPPENING!", path, type, M.type), "code/modules/vehicles/vehicle_actions.dm", 176);
            datum_action action = held[path] as datum_action;
            Waiting.NoSleep(action.Remove(M));
            Dm.remove(held, path);
        }
        Dm.remove(occupant_actions, M);
        return TRUE;
    }
}

/***************** ACTION DATUMS *****************/

// tg: code/modules/vehicles/vehicle_actions.dm:190
public partial class datum_action_vehicle : datum_action
{
    // tg: code/modules/vehicles/vehicle_actions.dm:195
    public override object Destroy(float? force = null)
    {
        vehicle_target = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/vehicles/vehicle_actions.dm:199
public partial class datum_action_vehicle_sealed : datum_action_vehicle
{
    // tg: code/modules/vehicles/vehicle_actions.dm:203
    public override object Destroy(float? force = null)
    {
        vehicle_entered_target = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/vehicles/vehicle_actions.dm:207
public partial class datum_action_vehicle_sealed_climb_out : datum_action_vehicle_sealed
{
    // tg: code/modules/vehicles/vehicle_actions.dm:212
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (Dm.truthy(await base.Trigger(clicker, trigger_flags, __extra)) && vehicle_entered_target != null)
            await vehicle_entered_target.mob_try_exit(owner, owner);
        return null;
    }
}
