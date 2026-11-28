using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice D (D230): the radial context menu's Buckle, Unbuckle, Pull and Stop pulling, beyond tg's screentips; M8z adds Climb (slice C) and Strip (slice B, D238);
// ui-overhaul.md section 6, slice E adds Offer item (tg's give, which was the give key's). Not tg code: the host's
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

    /// <summary>DV639: oneself into (or onto) the thing whose tg <c>mouse_drop_receive</c> would take him: tg's drag of oneself onto it (an exosuit's <c>mob_try_enter</c>, a netpod's, a scanner's or a chair's <c>close_machine</c>, a disposal bin's <c>stuff_mob_in</c>, the photocopier, a morgue tray; <see cref="atom_movable.dm_climb_in_label"/>).</summary>
    Enter = 7,

    /// <summary>DV503: the table flipped over or back upright (tg's empty hand's right click, which no longer flips: <c>radial_flip</c>).</summary>
    Flip = 8,

    /// <summary>DV640: oneself into an open closet: tg's step onto its tile and the hand's close from there (<see cref="ClimbIn.StepInto"/>).</summary>
    StepIn = 9,

    /// <summary>DV690 (package T, slice T4c): a lying carbon turned face up or face down (not in tg: <c>mob_living.radial_turn_over</c>, a do_after).</summary>
    TurnOver = 10,

    /// <summary>
    /// DV139 (ui-overhaul.md section 6, slice E): the held item offered to the mob, tg's <c>/mob/living/proc/give(offered)</c>
    /// (living_item_handling.dm:130), which was the give key's.
    /// </summary>
    Offer = 11,

    /// <summary>
    /// DV694 (the inventory's right-click wheel): one of tg's action buttons of an item the player holds or wears (tg grants an item's actions
    /// there only: <c>item_action_slot_check</c>), by the action's name; picking it is tg's click on that button (<c>action_button/Click</c>).
    /// </summary>
    ItemAction = 12,
}

public static class RadialExtras
{
    /// <summary>The label of <see cref="RadialExtra.Offer"/>: the words of tg's own screentip for it (carbon_context.dm), which the host leaves out of tg's entries.</summary>
    public static string OfferLabel => I18n.T("Offer item");

    /// <summary>
    /// The entries of D230 and D238 that <paramref name="user"/> can use on <paramref name="target"/> now, with their labels, in this order: Buckle
    /// (a thing he can buckle himself to, as tg's <c>is_user_buckle_possible</c> of a drag), Unbuckle (a thing with someone buckled to it,
    /// his active hand empty), Climb in or Climb on (a thing whose tg drop of him would take him, DV639, or an open closet, DV640), Climb (a climbable tg's drag would start climbing), Pull (a thing tg lets him pull) or Stop pulling (the thing he pulls), and Strip (a human whose strip menu
    /// tg's drag of him onto oneself would open: <c>should_strip</c> and <c>can_perform_action</c>), Offer item (slice E: a living mob tg's
    /// <c>give</c> would offer his held item to, <see cref="CanOffer"/>), and Turn over (DV690: a lying carbon). Only for a movable on a turf next to
    /// him (tg's <c>Adjacent</c>); none for a mob he would buckle to (tg's grabs, M9) or for himself. Called in the tick (the hover).
    /// </summary>
    public static List<(RadialExtra Kind, string Label)> Of(mob user, atom target)
    {
        var entries = new List<(RadialExtra, string)>();
        // DV694: an item of the own inventory offers its action buttons (none for one in a bag, as tg grants none there); the rest is for things on a turf
        if (target is obj_item own)
        {
            foreach ((datum_action action, _) in ItemButtons(user, own))
                entries.Add((RadialExtra.ItemAction, action.name));
        }
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
        // DEVIATION(DV639): the thing's own receiver would take him (asked quietly; it speaks on the choice), and tg's drag reaches it;
        // a type whose receiver is not ported keeps none (the drag would be refused, D132)
        if (thing.dm_climb_in_label(living) is string climb_in && !InertSeam.HasUndeclaredOverride(thing, "mouse_drop_receive"))
        {
            if (thing is obj_structure_closet)
                entries.Add((RadialExtra.StepIn, climb_in)); // DEVIATION(DV640): no drop; the step in and the close
            else if (ClimbIn.DropPasses(living, thing))
                entries.Add((RadialExtra.Enter, climb_in));
        }
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
        if (thing is mob_living taker && CanOffer(living, taker)) // DEVIATION(DV139): slice E's Offer item, the give key's
            entries.Add((RadialExtra.Offer, OfferLabel));
        if (thing is mob_living lying && lying.dm_can_be_turned_over) // DEVIATION(DV690)
            entries.Add((RadialExtra.TurnOver, I18n.T("Turn over")));
        return entries;
    }

    /// <summary>
    /// DV503, DV640: the radial's entry <paramref name="label"/> that is no click (<c>ContextHintAction.Extra</c>: Flip, a closet's Climb in) picked on <paramref name="target"/>: run only
    /// when <see cref="Of"/> offers it for the target now, with usr the player's mob, detached as BYOND runs a verb.
    /// </summary>
    public static void Pick(client player, atom target, string label)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.mob is not mob user || target == null)
            return;
        if (target is obj_item own)
        {
            // DV694: tg's click on the item's action button of that name, as the button's own click (its can_use, cooldown and Trigger)
            foreach ((datum_action action, atom_movable_screen_movable_action_button button) in ItemButtons(user, own))
            {
                if (action.name != label)
                    continue;
                ClientSeam.Click(player, button, ClientSeam.ClickParams(LEFT_CLICK, false, false, false, 0, 0, button.screen_loc));
                return;
            }
        }
        foreach ((RadialExtra kind, string offered) in Of(user, target))
        {
            if (offered != label || !(kind == RadialExtra.Flip && target is obj_structure_table || kind == RadialExtra.StepIn && target is obj_structure_closet && user is mob_living
                || kind is RadialExtra.TurnOver or RadialExtra.Offer && target is mob_living))
                continue;
            World world = World.Current;
            object caller_usr = world.usr;
            try
            {
                world.usr = user;
                if (target is obj_structure_table table)
                    Waiting.Detach(table.radial_flip(user));
                else if (kind == RadialExtra.StepIn)
                    Waiting.Detach(ClimbIn.StepInto((obj_structure_closet)target, (mob_living)user));
                else if (kind == RadialExtra.Offer)
                    Waiting.Detach(((mob_living)user).give((mob_living)target)); // DV139: tg's give, which checks everything again
                else
                    Waiting.Detach(((mob_living)target).radial_turn_over(user)); // DV690
            }
            finally
            {
                world.usr = caller_usr;
            }
            return;
        }
    }

    // DEVIATION(DV139): slice E's Offer item: exactly when tg's give(offered) would go through to the offer (living_item_handling.dm:130-166): the
    // giver not offering already and not incapacitated, an item in his active hand that give takes (not an abstract one, unless a borg's to give, one
    // offered when pulled or a hand item), the taker not incapacitated and able to hold items. The adjacency is Of's (tg's IsReachableBy from beside him).
    private static bool CanOffer(mob_living giver, mob_living taker)
    {
        if (Dm.truthy(giver.has_status_effect(typeof(datum_status_effect_offering))) || Dm.truthy(giver.incapacitated) || Dm.truthy(taker.incapacitated)
            || !HAS_TRAIT(taker, TRAIT_CAN_HOLD_ITEMS))
            return false;
        return giver.get_active_held_item() is obj_item item && (!Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT)) || HAS_TRAIT(item, TRAIT_BORG_GIVE)
            || HAS_TRAIT(item, TRAIT_OFFERED_WHEN_PULLED) || Dm.truthy(Dm.bitand(item.item_flags, HAND_ITEM)));
    }

    // DV694: the action buttons tg shows the user for the item: his own actions whose target it is, each with its button on his HUD
    // (action.viewers[hud], what the action bar draws; the client's screen entry names the same item, ScreenEntry.ActionTarget)
    private static IEnumerable<(datum_action Action, atom_movable_screen_movable_action_button Button)> ItemButtons(mob user, obj_item item)
    {
        if (user == null || item == null || QDELETED(user) || QDELETED(item) || user.hud_used is not datum_hud hud)
            yield break;
        foreach (datum_action action in Dm.each_cast<datum_action>(user.actions))
        {
            if (action != null && action.target == item && action.owner == user
                && action.viewers?[hud] is atom_movable_screen_movable_action_button button && !QDELETED(button))
                yield return (action, button);
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
