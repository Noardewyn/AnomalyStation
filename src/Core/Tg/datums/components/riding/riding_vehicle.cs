// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/riding/riding_vehicle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps H4, the janitor): the ridden vehicle's riding base (the key check, the rider's condition, the move and its cooldown)
// and the janicart's, the secway's (gaps SE1). The other vehicles' variants (the ATV, scooters and skateboards, the wheelchairs, the speedbike,
// the lavaboat, the bone bike, the stake...) come with their vehicles. The var blocks are generated (D111).

// For any /obj/vehicle's that can be ridden
// tg: code/datums/components/riding/riding_vehicle.dm:1
public partial class datum_component_riding_vehicle : datum_component_riding
{
    // tg: code/datums/components/riding/riding_vehicle.dm:3
    [DmParams("riding_mob", "force", "ride_check_flags")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isvehicle(parent))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/riding/riding_vehicle.dm:8
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_RIDDEN_DRIVER_MOVE, nameof(driver_move));
    }

    // tg: code/datums/components/riding/riding_vehicle.dm:12
    public override object riding_can_z_move(atom_movable movable_parent = null, object direction = null, turf start = null, turf destination = null, object z_move_flags = null, mob_living rider = null)
    {
        if (!Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_CAN_FLY_CHECKS)))
            return COMPONENT_RIDDEN_ALLOW_Z_MOVE;

        if (!Dm.truthy(keycheck(rider)))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat(rider, span_warning(I18n.T("[] has no key inserted!", movable_parent)));
            return COMPONENT_RIDDEN_STOP_Z_MOVE;
        }
        if (HAS_TRAIT(rider, TRAIT_INCAPACITATED))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat(rider, span_warning(I18n.T("You cannot operate [] right now!", movable_parent)));
            return COMPONENT_RIDDEN_STOP_Z_MOVE;
        }
        if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_LEGS)) && HAS_TRAIT(rider, TRAIT_FLOORED))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat(rider, span_warning(I18n.T("You can't seem to manage that while unable to stand up enough to move []...", movable_parent)));
            return COMPONENT_RIDDEN_STOP_Z_MOVE;
        }
        if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_ARMS)) && HAS_TRAIT(rider, TRAIT_HANDS_BLOCKED))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat(rider, span_warning(I18n.T("You can't seem to hold onto [] to move it...", movable_parent)));
            return COMPONENT_RIDDEN_STOP_Z_MOVE;
        }

        return COMPONENT_RIDDEN_ALLOW_Z_MOVE;
    }

    // tg: code/datums/components/riding/riding_vehicle.dm:35
    [SignalHandler]
    public override object driver_move(atom_movable movable_parent = null, mob_living user = null, object direction = null)
    {
        if (!(vehicle_move_cooldown <= world.time)) // COOLDOWN_FINISHED(src, vehicle_move_cooldown), inline (rules 11.1 (a))
            return COMPONENT_DRIVER_BLOCK_MOVE;
        var vehicle_parent = parent as obj_vehicle;

        if (!Dm.truthy(keycheck(user)))
        {
            if (message_cooldown <= world.time)
            {
                to_chat(user, span_warning(I18n.T("[] has no key inserted!", vehicle_parent)));
                message_cooldown = world.time + 5 * SECONDS; // COOLDOWN_START(src, message_cooldown, 5 SECONDS)
            }
            return COMPONENT_DRIVER_BLOCK_MOVE;
        }

        if (HAS_TRAIT(user, TRAIT_INCAPACITATED))
        {
            if (Dm.truthy(Dm.bitand(ride_check_flags, UNBUCKLE_DISABLED_RIDER)))
            {
                Waiting.NoSleep(vehicle_parent.unbuckle_mob(user, TRUE));
                user.visible_message(span_danger(I18n.T("[] falls off \\the [].", user, vehicle_parent)),
                    span_danger(I18n.T("You slip off \\the [] as your body slumps!", vehicle_parent)));
                Waiting.NoSleep(user.Stun(3 * SECONDS));
            }

            if (message_cooldown <= world.time)
            {
                to_chat(user, span_warning(I18n.T("You cannot operate \\the [] right now!", vehicle_parent)));
                message_cooldown = world.time + 5 * SECONDS;
            }
            return COMPONENT_DRIVER_BLOCK_MOVE;
        }

        if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_LEGS)) && HAS_TRAIT(user, TRAIT_FLOORED))
        {
            if (Dm.truthy(Dm.bitand(ride_check_flags, UNBUCKLE_DISABLED_RIDER)))
            {
                Waiting.NoSleep(vehicle_parent.unbuckle_mob(user, TRUE));
                user.visible_message(span_danger(I18n.T("[] falls off \\the [].", user, vehicle_parent)),
                    span_danger(I18n.T("You fall off \\the [] while trying to operate it while unable to stand!", vehicle_parent)));
                Waiting.NoSleep(user.Stun(3 * SECONDS));
            }

            if (message_cooldown <= world.time)
            {
                to_chat(user, span_warning(I18n.T("You can't seem to manage that while unable to stand up enough to move \\the []...", vehicle_parent)));
                message_cooldown = world.time + 5 * SECONDS;
            }
            return COMPONENT_DRIVER_BLOCK_MOVE;
        }

        if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_ARMS)) && HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
        {
            if (Dm.truthy(Dm.bitand(ride_check_flags, UNBUCKLE_DISABLED_RIDER)))
            {
                Waiting.NoSleep(vehicle_parent.unbuckle_mob(user, TRUE));
                user.visible_message(span_danger(I18n.T("[] falls off \\the [].", user, vehicle_parent)),
                    span_danger(I18n.T("You fall off \\the [] while trying to operate it without being able to hold on!", vehicle_parent)));
                Waiting.NoSleep(user.Stun(3 * SECONDS));
            }

            if (message_cooldown <= world.time)
            {
                to_chat(user, span_warning(I18n.T("You can't seem to hold onto \\the [] to move it...", vehicle_parent)));
                message_cooldown = world.time + 5 * SECONDS;
            }
            return COMPONENT_DRIVER_BLOCK_MOVE;
        }

        Waiting.NoSleep(handle_ride(user, direction));
        return base.driver_move(movable_parent, user, direction);
    }

    /// This handles the actual movement for vehicles once [/datum/component/riding/vehicle/proc/driver_move] has given us the green light
    // tg: code/datums/components/riding/riding_vehicle.dm:86
    public virtual async DmTask<object> handle_ride(mob user = null, object direction = null)
    {
        var movable_parent = parent as atom_movable;

        var next = get_step(movable_parent, Dm.num(direction)) as turf;
        var current = get_turf(movable_parent);
        if (next == null || current == null)
            return null; //not happening.
        if (!Dm.truthy(turf_check(next, current)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] can not go onto []!", movable_parent, next)));
            return null;
        }
        if (!Dm.truthy(Process_Spacemove(direction)) || !isturf(movable_parent.loc))
            return null;

        await step(movable_parent, Dm.num(direction));
        float move_delay = vehicle_move_delay;
        if (Dm.truthy(NSCOMPONENT(Dm.num(direction))) && Dm.truthy(EWCOMPONENT(Dm.num(direction))))
            move_delay = FLOOR(move_delay * Dm.sqrt(2), world.tick_lag);
        vehicle_move_cooldown = world.time + move_delay; // COOLDOWN_START(src, vehicle_move_cooldown, move_delay)

        if (QDELETED(this))
            return null;
        update_parent_layer_and_offsets(movable_parent.dir);
        return TRUE;
    }
}

// tg: code/datums/components/riding/riding_vehicle.dm:173
public partial class datum_component_riding_vehicle_janicart : datum_component_riding_vehicle
{
    // tg: code/datums/components/riding/riding_vehicle.dm:176
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 7f)),
            Dm.kv(TEXT_EAST, Dm.list(-12f, 7f)),
            Dm.kv(TEXT_WEST, Dm.list(12f, 7f)));
    }
}

// tg: code/datums/components/riding/riding_vehicle.dm:355  (gaps SE1)
public partial class datum_component_riding_vehicle_secway : datum_component_riding_vehicle
{
    // tg: code/datums/components/riding/riding_vehicle.dm:360
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_SOUTH, Dm.list(0f, 4f)),
            Dm.kv(TEXT_EAST, Dm.list(0f, 4f)),
            Dm.kv(TEXT_WEST, Dm.list(0f, 4f)));
    }

    // tg: code/datums/components/riding/riding_vehicle.dm:368
    [SignalHandler]
    public override object driver_move(atom_movable movable_parent = null, mob_living user = null, object direction = null)
    {
        var the_secway = parent as obj_vehicle_ridden_secway;

        if (Dm.truthy(keycheck(user)) && the_secway.eddie_murphy != null)
        {
            if (message_cooldown <= world.time) // COOLDOWN_FINISHED(src, message_cooldown)
            {
                the_secway.visible_message(span_warning(I18n.T("[] sputters and refuses to move!", the_secway)));
                playsound(get_turf(the_secway), Dm.resource("sound/effects/stall.ogg"), 70);
                message_cooldown = world.time + 0.75f * SECONDS; // COOLDOWN_START(src, message_cooldown, 0.75 SECONDS)
            }
            return COMPONENT_DRIVER_BLOCK_MOVE;
        }
        return base.driver_move(movable_parent, user, direction);
    }
}
