using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice D (D230): the radial context menu's Buckle, Unbuckle, Pull and Stop pulling, beyond tg's screentips; M8z adds Climb (slice C) and Strip (slice B, D238). Not tg code: the host's
// screentip sink adds them after tg's own entries (a label tg already gives is not repeated). They are advisory, as tg's are: the choice
// runs tg's own action (the drag of oneself onto the thing, the empty hand's click, the ctrl-click, the stop-pulling keybinding), which
// checks everything again. DEVIATION(DV139).

/// <summary>An entry of the radial beyond tg's context (D230).</summary>
public enum RadialExtra : byte
{
    /// <summary>Oneself buckled to the thing: tg's drag of oneself onto it (<c>mouse_buckle_handling</c>, <c>user_buckle_mob</c>).</summary>
    Buckle = 1,

    /// <summary>The one buckled to the thing unbuckled: tg's empty hand's click (<c>/atom/movable/attack_hand</c>, <c>user_unbuckle_mob</c>).</summary>
    Unbuckle = 2,

    /// <summary>The thing pulled: tg's ctrl-click (<c>base_click_ctrl</c>, <c>start_pulling</c>).</summary>
    Pull = 3,

    /// <summary>The pulled thing let go: tg's stop-pulling keybinding (<c>stop_pulling</c>).</summary>
    StopPulling = 4,

    /// <summary>D238: oneself climbed onto (or over) the thing: tg's drag of oneself onto a climbable (<c>/datum/element/climbable</c>'s <c>mousedrop_receive</c>).</summary>
    Climb = 5,
    /// <summary>A human dragged onto oneself: tg's strip menu (<c>/datum/element/strippable</c>'s <c>mouse_drop_onto</c>, M8z slice B, D238).</summary>
    Strip = 6,

    /// <summary>Oneself climbed into the vehicle (an exosuit, the clown car): tg's drag of oneself onto it (<c>/obj/vehicle/sealed/mouse_drop_receive</c>, <c>mob_try_enter</c>); since gaps BR1 (DV565) also into an open netpod (<c>/obj/machinery/netpod/mouse_drop_receive</c>).</summary>
    Enter = 7,

    /// <summary>DV503: the table flipped over or back upright (tg's empty hand's right click, which no longer flips: <c>radial_flip</c>).</summary>
    Flip = 8,

    /// <summary>DV588 (package T, slice T4c): a lying carbon turned face up or face down (not in tg: <c>mob_living.radial_turn_over</c>, a do_after).</summary>
    TurnOver = 9,
}

public static class RadialExtras
{
    /// <summary>
    /// The entries of D230 and D238 that <paramref name="user"/> can use on <paramref name="target"/> now, with their labels, in this order: Buckle
    /// (a thing he can buckle himself to, as tg's <c>is_user_buckle_possible</c> of a drag), Unbuckle (a thing with someone buckled to it,
    /// his active hand empty), Climb in (a sealed vehicle with a free seat he is not in), Climb (a climbable tg's drag would start climbing), Pull (a thing tg lets him pull) or Stop pulling (the thing he pulls), and Strip (a human whose strip menu
    /// tg's drag of him onto oneself would open: <c>should_strip</c> and <c>can_perform_action</c>), and Turn over (DV588: a lying carbon). Only for a movable on a turf next to
    /// him (tg's <c>Adjacent</c>); none for a mob he would buckle to (tg's grabs, M9) or for himself. Called in the tick (the hover).
    /// </summary>
    public static List<(RadialExtra Kind, string Label)> Of(mob user, atom target)
    {
        var entries = new List<(RadialExtra, string)>();
        if (user is not mob_living living || target is not atom_movable thing || thing == user || QDELETED(thing) || QDELETED(user)
            || !isturf(thing.loc) || !Dm.truthy(Waiting.NoSleep(thing.Adjacent(user))))
            return entries;
        if (Dm.truthy(thing.can_buckle) && thing is not mob)
        {
            if (living.buckled != thing && Dm.truthy(Waiting.NoSleep(thing.is_user_buckle_possible(living, user, check_loc: Dm.FALSE))))
                entries.Add((RadialExtra.Buckle, I18n.T("Buckle")));
            if (Dm.truthy(thing.has_buckled_mobs()) && user.get_active_held_item() == null)
                entries.Add((RadialExtra.Unbuckle, I18n.T("Unbuckle")));
        }
        // Only a free seat: the vehicle's own enter_checks talk to the user, and the hover must not (tg says why on the choice)
        if (thing is obj_vehicle_sealed vehicle && !vehicle.is_occupant(living) && Dm.num(vehicle.occupant_amount()) < Dm.num(vehicle.max_occupants))
            entries.Add((RadialExtra.Enter, I18n.T("Climb in")));
        // DEVIATION(DV565): an open, working netpod takes the carbon who drags himself onto it (netpod/_netpod.dm:132 mouse_drop_receive's own checks)
        if (thing is obj_machinery_netpod pod && user is mob_living_carbon && Dm.truthy(pod.is_operational) && Dm.truthy(pod.state_open) && living.buckled == null)
            entries.Add((RadialExtra.Enter, I18n.T("Climb in")));
        if (CanClimb(living, thing))
            entries.Add((RadialExtra.Climb, I18n.T("Climb")));
        if (thing is obj_structure_table table && Dm.truthy(table.can_flip))
            entries.Add((RadialExtra.Flip, Dm.truthy(table.is_flipped) ? I18n.T("Flip upright") : I18n.T("Flip")));
        if (living.pulling == thing)
            entries.Add((RadialExtra.StopPulling, I18n.T("Stop pulling")));
        else if (Dm.truthy(Dm.bitand(living.mobility_flags, MOBILITY_PULL)) && Dm.truthy(thing.can_be_pulled(user, living.pull_force)))
            entries.Add((RadialExtra.Pull, I18n.T("Pull")));
        if (thing is mob_living_carbon_human victim && Dm.truthy(victim.should_strip(user))
            && Dm.truthy(Waiting.NoSleep(user.can_perform_action(victim, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_RESTING)))))
            entries.Add((RadialExtra.Strip, I18n.T("Strip")));
        if (thing is mob_living lying && lying.dm_can_be_turned_over) // DEVIATION(DV588)
            entries.Add((RadialExtra.TurnOver, I18n.T("Turn over")));
        return entries;
    }

    /// <summary>
    /// DV503: the radial's entry <paramref name="label"/> that is no click (<c>ContextHintAction.Extra</c>) picked on <paramref name="target"/>: run only
    /// when <see cref="Of"/> offers it for the target now, with usr the player's mob, detached as BYOND runs a verb.
    /// </summary>
    public static void Pick(client player, atom target, string label)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.mob is not mob user || target == null)
            return;
        foreach ((RadialExtra kind, string offered) in Of(user, target))
        {
            if (offered != label || !(kind == RadialExtra.Flip && target is obj_structure_table || kind == RadialExtra.TurnOver && target is mob_living))
                continue;
            World world = World.Current;
            object caller_usr = world.usr;
            try
            {
                world.usr = user;
                if (target is obj_structure_table table)
                    Waiting.Detach(table.radial_flip(user));
                else
                    Waiting.Detach(((mob_living)target).radial_turn_over(user)); // DV588
            }
            finally
            {
                world.usr = caller_usr;
            }
            return;
        }
    }

    // DEVIATION(DV156): tg's climbable element would start a climb on the drag (mousedrop_receive's checks and can_climb, asked of the thing's own element).
    private static bool CanClimb(mob_living user, atom_movable thing)
    {
        if (!HAS_TRAIT(thing, TRAIT_CLIMBABLE) || LocomotionSeam.ListenerOf<datum_element_climbable>(thing, COMSIG_MOUSEDROPPED_ONTO) is not { } element)
            return false;
        if (!HAS_TRAIT(user, TRAIT_FENCE_CLIMBER) && !HAS_TRAIT(user, TRAIT_CAN_HOLD_ITEMS))
            return false;
        return Dm.truthy(Dm.bitand(user.mobility_flags, MOBILITY_MOVE)) && Dm.truthy(Waiting.NoSleep(element.can_climb(thing, user)));
    }
}
