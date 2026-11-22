// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/elevator/elev_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file.

// tg: code/modules/transport/elevator/elev_controller.dm:1
public partial class obj_machinery_button_elevator : obj_machinery_button
{
    // tg: code/modules/transport/elevator/elev_controller.dm:12
    [DmParams("mapload", "ndir", "built")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Kind of a cop-out
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("lmb_text", I18n.T("Call Elevator"))));
        return dot;
    }
}

// tg: code/modules/transport/elevator/elev_controller.dm:19
public partial class obj_item_assembly_control_elevator : obj_item_assembly_control
{
    // tg: code/modules/transport/elevator/elev_controller.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
            return INITIALIZE_HINT_LATELOAD;

        datum_transport_controller_linear lift = get_lift() as datum_transport_controller_linear;
        if (lift == null)
            return dot;

        lift_weakref = WEAKREF(lift) as datum_weakref;
        return dot;
    }

    // tg: code/modules/transport/elevator/elev_controller.dm:38
    public override void LateInitialize(params object[] __extra)
    {
        datum_transport_controller_linear lift = get_lift() as datum_transport_controller_linear;
        if (lift == null)
        {
            log_mapping(Dm.text("Elevator call button at [] found no associated elevator to link with, this may be a mapping error.", AREACOORD(this)));
            return;
        }

        lift_weakref = WEAKREF(lift) as datum_weakref;
    }

    // Emagging elevator buttons will disable safeties
    // tg: code/modules/transport/elevator/elev_controller.dm:47
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
            return FALSE;

        foreach (obj_structure_transport_linear lift_platform in Dm.each<obj_structure_transport_linear>(lift.transport_modules))
        {
            lift_platform.violent_landing = TRUE;
            lift_platform.warns_on_down_movement = FALSE;
            lift_platform.elevator_vertical_speed = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.elevator_vertical_speed))) * 0.5f;
        }

        foreach (obj_machinery_door elevator_door in Dm.each<obj_machinery_door>(GLOB.elevator_doors))
        {
            if (!Dm.eq(elevator_door.transport_linked_id, lift.specific_transport_id))
                continue;
            if (Dm.truthy(Dm.bitand(elevator_door.obj_flags, EMAGGED)))
                continue;
            elevator_door.elevator_status = LIFT_PLATFORM_UNLOCKED;
            Waiting.Detach(elevator_door.open(BYPASS_DOOR_CHECKS));
            elevator_door.obj_flags = Dm.num(Dm.bitor(elevator_door.obj_flags, EMAGGED));
        }

        // Note that we can either be emagged by having the button we are inside swiped,
        // or by someone emagging the assembly directly after removing it (to be cheeky)
        atom balloon_alert_loc = (get(this, typeof(obj_machinery_button)) as atom) ?? this;
        balloon_alert_loc.balloon_alert(user, I18n.T("safeties overridden"));
        return TRUE;
    }

    // Multitooling emagged elevator buttons will fix the safeties
    // tg: code/modules/transport/elevator/elev_controller.dm:76
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return await base.multitool_act(user, tool);

        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
            return await base.multitool_act(user, tool);

        foreach (obj_structure_transport_linear lift_platform in Dm.each<obj_structure_transport_linear>(lift.transport_modules))
        {
            lift_platform.violent_landing = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.violent_landing)));
            lift_platform.warns_on_down_movement = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.warns_on_down_movement)));
            lift_platform.elevator_vertical_speed = Dm.num(Dm.initial(lift_platform, nameof(lift_platform.elevator_vertical_speed)));
        }

        foreach (obj_machinery_door elevator_door in Dm.each<obj_machinery_door>(GLOB.elevator_doors))
        {
            if (!Dm.eq(elevator_door.transport_linked_id, lift.specific_transport_id))
                continue;
            if (!Dm.truthy(Dm.bitand(elevator_door.obj_flags, EMAGGED)))
                continue;
            elevator_door.obj_flags = Dm.num(Dm.bitand(elevator_door.obj_flags, ~EMAGGED));
            Waiting.Detach(elevator_door.close());
        }

        // We can only be multitooled directly so just throw up the balloon alert
        balloon_alert(user, I18n.T("safeties reset"));
        obj_flags = Dm.num(Dm.bitand(obj_flags, ~EMAGGED));
        return null;
    }

    // tg: code/modules/transport/elevator/elev_controller.dm:104
    public override async DmTask<object> activate(mob activator = null)
    {
        // COOLDOWN_FINISHED(src, elevator_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(elevator_cooldown <= world.time))
            return null;

        // Actually try to call the elevator - this sleeps.
        // If we failed to call it, play a buzz sound.
        if (!Dm.truthy(await call_elevator(activator)))
            playsound(loc, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, TRUE);

        // Finally, give people a chance to get off after it's done before going back off cooldown
        // COOLDOWN_START(src, elevator_cooldown, 2 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        elevator_cooldown = world.time + 2 * SECONDS;
        return null;
    }

    /// Actually calls the elevator.
    /// Returns FALSE if we failed to setup the move.
    /// Returns TRUE if the move setup was a success, EVEN IF the move itself fails afterwards
    // tg: code/modules/transport/elevator/elev_controller.dm:119
    public virtual async DmTask<object> call_elevator(mob activator = null)
    {
        // We can't call an elevator that doesn't exist
        datum_transport_controller_linear lift = lift_weakref?.resolve() as datum_transport_controller_linear;
        if (lift == null)
        {
            loc.balloon_alert(activator, I18n.T("no elevator connected!"));
            return FALSE;
        }

        // We can't call an elevator that's moving. You may say "you totally can do that", but that's not modelled
        if (Dm.truthy(Dm.bitand(lift.controller_status, CONTROLS_LOCKED)))
        {
            loc.balloon_alert(activator, I18n.T("elevator is moving!"));
            return FALSE;
        }

        // If the elevator is already here, open the doors.
        obj_structure_transport_linear prime_lift = lift.return_closest_platform_to_z(loc.z) as obj_structure_transport_linear;
        if (Dm.eq(prime_lift.z, loc.z))
        {
            Waiting.DetachSync(() => lift.open_lift_doors_callback());
            loc.balloon_alert(activator, I18n.T("elevator is here!"));
            return TRUE;
        }

        // At this point, we can start moving.

        // Give the user, if supplied, a balloon alert.
        if (activator != null)
            loc.balloon_alert(activator, I18n.T("elevator called"));

        // Actually try to move the lift. This will sleep.
        if (!Dm.truthy(await lift.move_to_zlevel(loc.z, Dm.@new<datum_callback>(this, nameof(check_button)))))
        {
            loc.balloon_alert(activator, I18n.T("elevator out of service!"));
            return FALSE;
        }

        // From here on all returns are TRUE, as we successfully moved the lift, even if we maybe didn't reach our floor

        // Our button was destroyed mid transit.
        if (!Dm.truthy(check_button()))
            return TRUE;

        // Our lift platform survived, but it didn't reach our landing z.
        if (!QDELETED(prime_lift) && !Dm.eq(prime_lift.z, loc.z))
        {
            if (!QDELETED(activator))
                loc.balloon_alert(activator, I18n.T("elevator out of service!"));
            playsound(loc, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
            return TRUE;
        }

        // Everything went according to plan
        if (!QDELETED(activator))
            loc.balloon_alert(activator, I18n.T("elevator arrived"));

        return TRUE;
    }

    /// Callback for move_to_zlevel / general proc to check if we're still in a button
    // tg: code/modules/transport/elevator/elev_controller.dm:165
    public virtual object check_button()
    {
        if (QDELETED(this) || loc is not obj_machinery_button)
            return FALSE;
        return TRUE;
    }

    /// Gets the elevator associated with our assembly / button
    // tg: code/modules/transport/elevator/elev_controller.dm:172
    public virtual object get_lift()
    {
        foreach (datum_transport_controller_linear possible_match in Dm.each<datum_transport_controller_linear>(SStransport.transports_by_type[TRANSPORT_TYPE_ELEVATOR]))
        {
            if (!Dm.eq(possible_match.specific_transport_id, id))
                continue;

            return possible_match;
        }

        return null;
    }
}
