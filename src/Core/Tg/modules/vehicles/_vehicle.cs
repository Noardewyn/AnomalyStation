// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/_vehicle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/_vehicle.dm:1
[DmPath("/obj/vehicle")]
public partial class obj_vehicle : obj
{
    // tg: code/modules/vehicles/_vehicle.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        occupants = Dm.list();
        autogrant_actions_passenger = Dm.list();
        autogrant_actions_controller = Dm.list();
        occupant_actions = Dm.list();
        generate_actions();
        ADD_TRAIT(this, TRAIT_CASTABLE_LOC, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/vehicles/_vehicle.dm:59
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref trailer);
        inserted_key = null;
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/_vehicle.dm:64
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == inserted_key)
            inserted_key = null;
        if (Dm.truthy(exit_sound))
            playsound(this, exit_sound, 70, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/vehicles/_vehicle.dm:70  (X3)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, generate_integrity_message());
        return dot;
    }

    /// Returns a readable string of the vehicle's health for examining. Overridden by subtypes who want to be more verbose with their health messages.
    // tg: code/modules/vehicles/_vehicle.dm:75  (X3)
    public virtual object generate_integrity_message()
    {
        object examine_text = "";
        float integrity = Dm.num(atom_integrity) / Dm.num(max_integrity) * 100;
        if (integrity >= 50 && integrity <= 99)
            examine_text = I18n.T("It looks slightly damaged.");
        else if (integrity >= 25 && integrity <= 50)
            examine_text = I18n.T("It appears heavily damaged.");
        else if (integrity >= 0 && integrity <= 25)
            examine_text = span_warning(I18n.T("It's falling apart!"));

        return examine_text;
    }

    // tg: code/modules/vehicles/_vehicle.dm:88
    public virtual bool is_key(obj_item I = null) => istype(I, key_type);

    // tg: code/modules/vehicles/_vehicle.dm:91
    public virtual DmList return_occupants() => occupants;

    // tg: code/modules/vehicles/_vehicle.dm:94
    public virtual float occupant_amount() => Dm.num(LAZYLEN(occupants));

    // tg: code/modules/vehicles/_vehicle.dm:97
    public virtual float return_amount_of_controllers_with_flag(object flag = null)
    {
        float dot = 0;
        foreach (object i in Dm.each<object>(occupants))
        {
            if (Dm.truthy(Dm.bitand(occupants[i], flag)))
                dot++;
        }
        return dot;
    }

    // tg: code/modules/vehicles/_vehicle.dm:103
    public virtual DmList return_controllers_with_flag(object flag = null)
    {
        DmList dot = Dm.list();
        foreach (object i in Dm.each<object>(occupants))
        {
            if (Dm.truthy(Dm.bitand(occupants[i], flag)))
                dot.Add(i);
        }
        return dot;
    }

    // tg: code/modules/vehicles/_vehicle.dm:110
    public virtual DmList return_drivers() => return_controllers_with_flag(VEHICLE_CONTROL_DRIVE);

    // tg: code/modules/vehicles/_vehicle.dm:113
    public virtual float driver_amount() => return_amount_of_controllers_with_flag(VEHICLE_CONTROL_DRIVE);

    // tg: code/modules/vehicles/_vehicle.dm:116
    public virtual bool is_driver(mob M = null) => is_occupant(M) && Dm.truthy(Dm.bitand(occupants[M], VEHICLE_CONTROL_DRIVE));

    // tg: code/modules/vehicles/_vehicle.dm:119
    public virtual bool is_occupant(mob M = null) => LAZYACCESS(occupants, M) != null;

    // tg: code/modules/vehicles/_vehicle.dm:122
    public virtual async DmTask<object> add_occupant(mob M = null, object control_flags = null, object forced = null)
    {
        if (!istype(M, typeof(mob)) || is_occupant(M))
            return FALSE;
        if (Dm.truthy(enter_sound) && !Dm.truthy(forced))
            playsound(this, enter_sound, 70, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        LAZYSET(ref occupants, M, NONE);
        await add_control_flags(M, control_flags);
        await after_add_occupant(M);
        await grant_passenger_actions(M);
        return TRUE;
    }

    // tg: code/modules/vehicles/_vehicle.dm:133
    public virtual async DmTask after_add_occupant(mob M = null) => await auto_assign_occupant_flags(M);

    //override for each type that needs it. Default is assign driver if drivers is not at max.
    // tg: code/modules/vehicles/_vehicle.dm:136
    public virtual async DmTask auto_assign_occupant_flags(mob M = null)
    {
        if (driver_amount() < max_drivers)
            await add_control_flags(M, VEHICLE_CONTROL_DRIVE);
    }

    // tg: code/modules/vehicles/_vehicle.dm:140
    public virtual async DmTask<object> remove_occupant(mob M = null)
    {
        if (!istype(M, typeof(mob)))
            return FALSE;
        remove_control_flags(M, ALL);
        remove_passenger_actions(M);
        LAZYREMOVE(ref occupants, M);
        cleanup_actions_for_mob(M);
        after_remove_occupant(M);
        return TRUE;
    }

    // tg: code/modules/vehicles/_vehicle.dm:152
    public virtual void after_remove_occupant(mob M = null)
    {
    }

    // tg: code/modules/vehicles/_vehicle.dm:154
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.truthy(canmove))
            return FALSE;
        if (is_driver(user))
            return relaydrive(user, direction);
        return FALSE;
    }

    // tg: code/modules/vehicles/_vehicle.dm:161
    public virtual void after_move(object direction = null)
    {
    }

    // tg: code/modules/vehicles/_vehicle.dm:164
    public virtual async DmTask<object> add_control_flags(mob controller = null, object flags = null)
    {
        if (!is_occupant(controller) || !Dm.truthy(flags))
            return FALSE;
        occupants[controller] = Dm.bitor(occupants[controller], flags);
        foreach (object i in Dm.each<object>(GLOB.bitflags))
        {
            if (Dm.truthy(Dm.bitand(flags, i)))
                await grant_controller_actions_by_flag(controller, i);
        }
        return TRUE;
    }

    // tg: code/modules/vehicles/_vehicle.dm:173
    public virtual object remove_control_flags(mob controller = null, object flags = null)
    {
        if (!is_occupant(controller) || !Dm.truthy(flags))
            return FALSE;
        occupants[controller] = Dm.bitand(occupants[controller], Dm.bitnot(flags));
        foreach (object i in Dm.each<object>(GLOB.bitflags))
        {
            if (Dm.truthy(Dm.bitand(flags, i)))
                remove_controller_actions_by_flag(controller, i);
        }
        return TRUE;
    }

    /// To add a trailer to the vehicle in a manner that allows safe qdels
    // tg: code/modules/vehicles/_vehicle.dm:183
    public virtual void add_trailer(obj_vehicle added_vehicle = null)
    {
        trailer = added_vehicle;
        RegisterSignal(trailer, COMSIG_QDELETING, nameof(remove_trailer));
    }

    /// To remove a trailer from the vehicle in a manner that allows safe qdels
    // tg: code/modules/vehicles/_vehicle.dm:188
    public virtual void remove_trailer()
    {
        UnregisterSignal(trailer, COMSIG_QDELETING);
        trailer = null;
    }

    // tg: code/modules/vehicles/_vehicle.dm:193
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        // It is unfortunate, but this is the way to make it not mess up
        atom old_loc = loc;
        // When we do this, it will set the loc to the new loc
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (trailer != null && Dm.truthy(dot))
        {
            object dir_to_move = get_dir(trailer.loc, old_loc);
            await step(trailer, Dm.num(dir_to_move));
        }
        return dot;
    }
}
