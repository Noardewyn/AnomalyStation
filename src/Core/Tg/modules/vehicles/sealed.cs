// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/sealed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/sealed.dm:1
[DmPath("/obj/vehicle/sealed")]
public partial class obj_vehicle_sealed : obj_vehicle
{
    // tg: code/modules/vehicles/sealed.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_SUPERMATTER_CONSUMED, nameof(on_entered_supermatter));
        return dot;
    }

    // tg: code/modules/vehicles/sealed.dm:15
    public override void generate_actions()
    {
        base.generate_actions();
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_climb_out));
    }

    // tg: code/modules/vehicles/sealed.dm:19
    public override object generate_action_type(Type actiontype = null)
    {
        datum_action_vehicle_sealed E = base.generate_action_type(actiontype) as datum_action_vehicle_sealed;
        if (E != null)
            E.vehicle_entered_target = this;
        return E;
    }

    // tg: code/modules/vehicles/sealed.dm:25
    public override async DmTask<object> mouse_drop_receive(atom dropping = null, mob user = null, object @params = null)
    {
        if (dropping == null || !istype(user, typeof(mob_living)))
            return await base.mouse_drop_receive(dropping, user, @params);
        if (user == dropping)
            await mob_try_enter(user);
        return await base.mouse_drop_receive(dropping, user, @params);
    }

    // tg: code/modules/vehicles/sealed.dm:32
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (ismob(gone))
            await remove_occupant(gone as mob);
        return dot;
    }

    // so that we can check the access of the vehicle's occupants. Ridden vehicles do this in the riding component, but these don't have that
    // tg: code/modules/vehicles/sealed.dm:38
    public override async DmTask<object> Bump(atom A = null)
    {
        object dot = await base.Bump(A);
        if (istype(A, typeof(obj_machinery_door)))
        {
            obj_machinery_door conditionalwall = A as obj_machinery_door;
            foreach (mob occupant in Dm.each<mob>(return_controllers_with_flag(access_provider_flags)))
            {
                if (Dm.truthy(await conditionalwall.try_safety_unlock(occupant)) || !Dm.truthy(conditionalwall.can_open_with_hands))
                    return dot;
                await conditionalwall.bumpopen(occupant);
            }
        }
        return dot;
    }

    // tg: code/modules/vehicles/sealed.dm:47
    public override async DmTask after_add_occupant(mob M = null)
    {
        await base.after_add_occupant(M);
        ADD_TRAIT(M, TRAIT_HANDS_BLOCKED, VEHICLE_TRAIT);
    }

    // tg: code/modules/vehicles/sealed.dm:52
    public override void after_remove_occupant(mob M = null)
    {
        base.after_remove_occupant(M);
        REMOVE_TRAIT(M, TRAIT_HANDS_BLOCKED, VEHICLE_TRAIT);
    }

    // tg: code/modules/vehicles/sealed.dm:57
    public virtual async DmTask<object> mob_try_enter(mob rider = null)
    {
        if (!istype(rider, typeof(mob)))
            return FALSE;
        object enter_delay__1 = get_enter_delay(rider);
        if (Dm.eq(enter_delay__1, 0))
        {
            if (Dm.truthy(enter_checks(rider)))
            {
                await mob_enter(rider);
                return TRUE;
            }
            return FALSE;
        }
        if (Dm.truthy(await do_after(rider, enter_delay__1, this, timed_action_flags: IGNORE_HELD_ITEM, extra_checks: Dm.@new<datum_callback>(this, nameof(enter_checks), rider))))
        {
            await mob_enter(rider);
            return TRUE;
        }
        return FALSE;
    }

    /// returns enter do_after delay for the given mob in ticks
    // tg: code/modules/vehicles/sealed.dm:72
    public virtual object get_enter_delay(mob M = null) => enter_delay;

    ///Extra checks to perform during the do_after to enter the vehicle
    // tg: code/modules/vehicles/sealed.dm:76
    public virtual object enter_checks(mob M = null) => occupant_amount() < max_occupants ? TRUE : FALSE;

    // tg: code/modules/vehicles/sealed.dm:79
    public virtual async DmTask<object> mob_enter(mob M = null, object silent = null)
    {
        silent ??= FALSE;
        if (!istype(M, typeof(mob)))
            return FALSE;
        if (!Dm.truthy(silent))
            M.visible_message(span_notice(I18n.T("[] climbs into \\the []!", M, this)));
        await M.forceMove(this);
        await add_occupant(M);
        return TRUE;
    }

    // tg: code/modules/vehicles/sealed.dm:87
    public virtual async DmTask mob_try_exit(mob M = null, mob user = null, object silent = null, object randomstep = null)
    {
        silent ??= FALSE;
        randomstep ??= FALSE;
        await mob_exit(M, silent, randomstep);
    }

    // tg: code/modules/vehicles/sealed.dm:90
    public virtual async DmTask<object> mob_exit(mob M = null, object silent = null, object randomstep = null, object forced = null)
    {
        silent ??= FALSE;
        randomstep ??= FALSE;
        if (!istype(M, typeof(mob)))
            return FALSE;
        await remove_occupant(M);
        if (!isAI(M))//This is the ONE mob we don't want to be moved to the vehicle that should be handled when used
            await M.forceMove(exit_location(M) as atom);
        else
            return TRUE;
        if (Dm.truthy(randomstep))
        {
            turf target_turf = get_step(exit_location(M) as atom, Dm.num(pick(GLOB.cardinals))) as turf;
            await M.throw_at(target_turf, 5, 10);
        }

        if (!Dm.truthy(silent))
            M.visible_message(span_notice(I18n.T("[] drops out of \\the []!", M, this)));
        return TRUE;
    }

    // tg: code/modules/vehicles/sealed.dm:105
    public virtual object exit_location(object M = null) => drop_location();

    // tg: code/modules/vehicles/sealed.dm:108
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (key_type == null || is_key(inserted_key) || !is_key(tool))
            return NONE;

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] seems to be stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You insert [] into [].", tool, this)));
        if (inserted_key != null) // Just in case there's an invalid key
            await inserted_key.forceMove(drop_location() as atom);
        inserted_key = tool;
        await inserted_key.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/vehicles/sealed.dm:122
    public virtual async DmTask remove_key(mob user = null)
    {
        if (inserted_key == null)
        {
            to_chat(user, span_warning(I18n.T("There is no key in []!", this)));
            return;
        }
        if (!is_occupant(user) || !Dm.truthy(Dm.bitand(occupants[user], VEHICLE_CONTROL_DRIVE)))
        {
            to_chat(user, span_warning(I18n.T("You must be driving [] to remove []'s key!", this, this)));
            return;
        }
        to_chat(user, span_notice(I18n.T("You remove [] from [].", inserted_key, this)));
        if (!HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
            await user.put_in_hands(inserted_key);
        else
            await inserted_key.equip_to_best_slot(user);
        inserted_key = null;
    }

    // tg: code/modules/vehicles/sealed.dm:136
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(dump_mobs());
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/sealed.dm:140
    public virtual async DmTask dump_mobs(object randomstep = null)
    {
        randomstep ??= TRUE;
        foreach (object i in Dm.each<object>(occupants?.Copy()))
        {
            await mob_exit(i as mob, randomstep: randomstep);
            if (iscarbon(i))
            {
                mob_living_carbon Carbon = i as mob_living_carbon;
                await Carbon.Paralyze(40);
            }
        }
    }

    // tg: code/modules/vehicles/sealed.dm:147
    public virtual async DmTask dump_specific_mobs(object flag = null, object randomstep = null)
    {
        randomstep ??= TRUE;
        foreach (object i in Dm.each<object>(occupants?.Copy()))
        {
            if (!Dm.truthy(Dm.bitand(occupants[i], flag)))
                continue;
            await mob_exit(i as mob, randomstep: randomstep);
            if (iscarbon(i))
            {
                mob_living_carbon C = i as mob_living_carbon;
                await C.Paralyze(40);
            }
        }
    }

    // tg: code/modules/vehicles/sealed.dm:157
    public override object AllowDrop() => FALSE;

    // tg: code/modules/vehicles/sealed.dm:160
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(canmove))
            await vehicle_move(direction);
        return TRUE;
    }

    /// Sinced sealed vehicles (cars and mechs) don't have riding components, the actual movement is handled here from [/obj/vehicle/sealed/proc/relaymove]
    // tg: code/modules/vehicles/sealed.dm:166
    public virtual DmTask<object> vehicle_move(object direction = null, object forcerotate = null) => DmTask<object>.FromResult(FALSE);

    /// When we touch a crystal, kill everything inside us
    // tg: code/modules/vehicles/sealed.dm:170
    public virtual void on_entered_supermatter(atom_movable vehicle = null, atom_movable supermatter = null)
    {
        foreach (mob passenger in Dm.each<mob>(occupants))
        {
            if (!isAI(passenger))
                Waiting.Detach(passenger.Bump(supermatter));
        }
    }
}
