// Anomaly Station code, not a translation of tg: the radial's Climb in (DV639).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV639): in tg a player gets into a sleeper, a scanner, a disposal bin, a netpod, an exosuit and the rest by dragging his own
// sprite onto the thing, which runs its mouse_drop_receive; first person never shows the own body under the pointer, so the radial offers
// Climb in (or Climb on, for a thing he ends up on top of) where that drag would take him now. Each receiver answers here, next to the
// line of its tg receiver, with the conditions that receiver's drop of the user himself checks, asked quietly: the hover runs every tick,
// and the receiver's own messages are said on the choice, which sends tg's drag (the radial's DropSelf) and runs every check again.
// A closet takes no drop of its user in tg (closets.dm:991 hands it to the buckle handling); a player gets into one by stepping onto
// its open tile and closing it from there, so its Climb in runs those two tg actions (DV640).

public partial class atom_movable
{
    /// <summary>
    /// The radial's label for a drop of <paramref name="user"/>'s own mob onto this thing when this thing's own tg <c>mouse_drop_receive</c>
    /// would take him into it or onto it now (Climb in, Climb on), null when it would not or the thing has no such receiver. Quiet: no
    /// message, no change. The drop's own checks (reach, usability) are <see cref="ClimbIn.DropPasses"/>'s.
    /// </summary>
    public virtual string dm_climb_in_label(mob_living user) => null;
}

public static class ClimbIn
{
    /// <summary>"Climb in": the drop puts him inside the thing.</summary>
    public static string Into => I18n.T("Climb in");

    /// <summary>"Climb on": the drop puts him on top of the thing or on its tile.</summary>
    public static string Onto => I18n.T("Climb on");

    /// <summary>
    /// Whether tg's <c>base_mouse_drop_handler</c> (drag_drop.dm:27) of <paramref name="user"/>'s own mob onto <paramref name="thing"/> would
    /// reach the thing's <c>mouse_drop_receive</c>: no climbable element takes the drop first (climbable.dm:132 cancels it for a climber),
    /// both are within reach and the user can act (<c>can_perform_action</c> with both sides' mouse drop flags), asked without the messages.
    /// </summary>
    public static bool DropPasses(mob_living user, atom_movable thing)
    {
        if (LocomotionSeam.ListenerOf<datum_element_climbable>(thing, COMSIG_MOUSEDROPPED_ONTO) != null
            && (HAS_TRAIT(user, TRAIT_FENCE_CLIMBER) || HAS_TRAIT(user, TRAIT_CAN_HOLD_ITEMS)))
            return false;
        object combined_atom_flags = Dm.bitor(user.interaction_flags_atom, thing.interaction_flags_atom);
        if (Dm.truthy(Dm.bitand(combined_atom_flags, INTERACT_ATOM_MOUSEDROP_IGNORE_CHECKS)))
            return true;
        if (!Dm.truthy(Dm.bitand(combined_atom_flags, INTERACT_ATOM_MOUSEDROP_IGNORE_ADJACENT))
            && (!Dm.truthy(Waiting.NoSleep(user.IsReachableBy(user))) || !Dm.truthy(Waiting.NoSleep(thing.IsReachableBy(user)))))
            return false;
        if (Dm.truthy(Dm.bitand(combined_atom_flags, INTERACT_ATOM_MOUSEDROP_IGNORE_USABILITY)))
            return true;
        return CanAct(user, Dm.bitor(Dm.bitor(user.interaction_flags_mouse_drop, thing.interaction_flags_mouse_drop), BYPASS_ADJACENCY));
    }

    // tg's /mob/living/can_perform_action (living.dm:1361) of the user on himself with BYPASS_ADJACENCY, without its messages; the
    // cyborg's lockdown and low power first (robot.dm:612).
    private static bool CanAct(mob_living user, object action_bitflags)
    {
        if (user is mob_living_silicon_robot borg && (Dm.truthy(borg.lockcharge) || Dm.truthy(borg.low_power_mode)))
            return false;
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return false;
        if (!Dm.truthy(Dm.bitand(user.interaction_flags_atom, INTERACT_ATOM_IGNORE_INCAPACITATED)))
        {
            float ignore_flags = NONE;
            if (Dm.truthy(Dm.bitand(user.interaction_flags_atom, INTERACT_ATOM_IGNORE_RESTRAINED)))
                ignore_flags = Dm.bitor(ignore_flags, INCAPABLE_RESTRAINTS);
            if (!Dm.truthy(Dm.bitand(user.interaction_flags_atom, INTERACT_ATOM_CHECK_GRAB)))
                ignore_flags = Dm.bitor(ignore_flags, INCAPABLE_GRAB);
            if (Dm.truthy(INCAPACITATED_IGNORING(user, ignore_flags)))
                return false;
        }
        if (!Dm.truthy(Dm.bitand(user.mobility_flags, MOBILITY_UI)) && !Dm.truthy(Dm.bitand(action_bitflags, ALLOW_RESTING)))
            return false;
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_HANDS)) && (HAS_TRAIT(user, TRAIT_HANDS_BLOCKED) || !Dm.truthy(user.can_hold_items(null))))
            return false;
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_VENTCRAWL)) && !HAS_TRAIT(user, TRAIT_VENTCRAWLER_NUDE) && !HAS_TRAIT(user, TRAIT_VENTCRAWLER_ALWAYS))
            return false;
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_DEXTERITY)) && !ISADVANCEDTOOLUSER(user))
            return false;
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_LITERACY)) && !Dm.truthy(user.is_literate()))
            return false;
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_LIGHT)) && !Dm.truthy(user.has_light_nearby()) && !Dm.truthy(user.has_nightvision()))
            return false;
        if (Dm.truthy(Dm.bitand(action_bitflags, NEED_GRAVITY)) && !Dm.truthy(user.has_gravity()))
            return false;
        return true;
    }

    /// <summary>Whether a machine's <c>close_machine</c> (_machinery.dm:443) would put <paramref name="user"/> inside: he is not buckled and carries no one.</summary>
    public static bool Enclosable(mob_living user) => user.buckled == null && !Dm.truthy(user.has_buckled_mobs());

    /// <summary>
    /// DV640: a closet's Climb in, the radial's pick (<see cref="RadialExtras.Pick"/>): <paramref name="user"/> steps onto the open closet's
    /// tile (tg's <c>Move</c>) and closes it from there with his hand (closets.dm:1036 <c>attack_hand</c>, whose <c>close</c> takes in what
    /// stands on its tile). Nothing more when the step fails.
    /// </summary>
    public static async DmTask StepInto(obj_structure_closet closet, mob_living user)
    {
        if (closet.loc is not turf tile)
            return;
        if (user.loc != tile && !Dm.truthy(await user.Move(tile, get_dir(user, tile))))
            return;
        if (user.loc != tile || !Dm.truthy(closet.opened))
            return;
        await closet.attack_hand(user, Dm.list());
    }
}

// tg: code/game/objects/structures/crates_lockers/closets.dm:987 (DV640: no drop; the step onto its tile and the close)
public partial class obj_structure_closet
{
    public override string dm_climb_in_label(mob_living user)
    {
        // close (closets.dm:609): open, can_close with him on its tile; insertion_allowed (closets.dm:569) of him, without its stop_pulling
        if (!Dm.truthy(opened) || !isturf(loc) || Dm.truthy(user.anchored) || user.buckled != null || Dm.truthy(user.incorporeal_move) || Dm.truthy(user.has_buckled_mobs()))
            return null;
        if (Dm.num(user.mob_size) > MOB_SIZE_TINY)
        {
            if (Dm.truthy(horizontal) && Dm.truthy(user.density) || Dm.num(user.mob_size) > max_mob_size)
                return null;
            float mobs_stored = 0;
            foreach (mob_living _ in Dm.each<mob_living>(contents))
            {
                if (++mobs_stored >= mob_storage_capacity)
                    return null;
            }
        }
        if (!Dm.truthy(can_close(null)))
            return null;
        // the step onto its tile: nothing dense there but the mobs, which tg's Move swaps or bumps
        turf tile = (turf)loc;
        if (user.loc != tile && Dm.truthy(Waiting.NoSleep(tile.is_blocked_turf(TRUE, user, Dm.list(this)))))
            return null;
        return ClimbIn.Into;
    }
}

// tg: code/game/objects/structures/morgue.dm:581
public partial class obj_structure_tray
{
    public override string dm_climb_in_label(mob_living user)
        => Dm.truthy(user.anchored) || user.buckled != null ? null : ClimbIn.Onto;
}

// tg: code/game/objects/items/implants/implantchair.dm:145 (close_machine, :150: open)
public partial class obj_machinery_implantchair
{
    public override string dm_climb_in_label(mob_living user)
        => Dm.truthy(state_open) && ClimbIn.Enclosable(user) ? ClimbIn.Into : null;
}

// tg: code/game/machinery/hypnochair.dm:199 (the base close_machine takes him even into a closed chair; offered only into an open, empty one)
public partial class obj_machinery_hypnochair
{
    public override string dm_climb_in_label(mob_living user)
        => Dm.truthy(state_open) && occupant == null && ClimbIn.Enclosable(user) ? ClimbIn.Into : null;
}

// tg: code/game/machinery/dna_scanner.dm:141 (close_machine, :98: open)
public partial class obj_machinery_dna_scannernew
{
    public override string dm_climb_in_label(mob_living user)
        => user is mob_living_carbon && Dm.truthy(state_open) && ClimbIn.Enclosable(user) ? ClimbIn.Into : null;
}

// tg: code/game/machinery/suit_storage_unit.dm:450
public partial class obj_machinery_suit_storage_unit
{
    public override string dm_climb_in_label(mob_living user)
        => Dm.truthy(state_open) && Dm.truthy(is_operational) && !Dm.truthy(occupant) && !Dm.truthy(helmet_value) && !Dm.truthy(suit_value) && !Dm.truthy(storage)
            && ClimbIn.Enclosable(user) ? ClimbIn.Into : null;
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:645 (only an incapacitated carbon gets himself in; close_machine,
// :510: open, its panel shut)
public partial class obj_machinery_cryo_cell
{
    public override string dm_climb_in_label(mob_living user)
        => user is mob_living_carbon && Dm.truthy(user.incapacitated) && Dm.truthy(state_open) && !Dm.truthy(panel_open) && ClimbIn.Enclosable(user) ? ClimbIn.Into : null;
}

// tg: code/modules/bitrunning/netpod/_netpod.dm:132 (close_machine, netpod/container.dm:28)
public partial class obj_machinery_netpod
{
    public override string dm_climb_in_label(mob_living user)
        => user is mob_living_carbon && Dm.truthy(is_operational) && Dm.truthy(state_open) && !Dm.truthy(panel_open) && user.buckled == null ? ClimbIn.Into : null;
}

// tg: code/modules/recycling/disposal/bin.dm:196 (stuff_mob_in, :203)
public partial class obj_machinery_disposal
{
    public override string dm_climb_in_label(mob_living user)
    {
        bool ventcrawler = HAS_TRAIT(user, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(user, TRAIT_VENTCRAWLER_NUDE);
        if (user is not mob_living_carbon && !ventcrawler && !(user is mob_living_silicon_robot borg && borg.model != null && Dm.truthy(borg.model.canDispose)))
            return null;
        if (!isturf(user.loc) || Dm.truthy(user.buckled) || Dm.truthy(user.has_buckled_mobs()) || Dm.num(user.mob_size) > MOB_SIZE_HUMAN)
            return null;
        return ClimbIn.Into;
    }
}

// tg: code/modules/paperwork/photocopier.dm:796
public partial class obj_machinery_photocopier
{
    public override string dm_climb_in_label(mob_living user)
        => Dm.truthy(user.anchored) || user.buckled != null || user == ass || Dm.truthy(copier_blocked()) ? null : ClimbIn.Onto;
}

// tg: code/modules/vehicles/sealed.dm:26 (mob_try_enter): a free seat he is not in; the vehicle's own enter_checks speak, so they run on the choice
public partial class obj_vehicle_sealed
{
    public override string dm_climb_in_label(mob_living user)
        => !is_occupant(user) && occupant_amount() < Dm.num(max_occupants) ? ClimbIn.Into : null;
}

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot_delivery.dm:23 (load, :46: a mob only when its load check wire is cut; load_mob buckles him)
public partial class mob_living_basic_bot_mulebot
{
    public override string dm_climb_in_label(mob_living user)
    {
        if (load != null || Dm.truthy(user.anchored) || !isturf(user.loc) || user.buckled != null || Dm.truthy(user.has_buckled_mobs()))
            return null;
        return wires is datum_wires cables && Dm.truthy(cables.is_cut(WIRE_LOADCHECK)) ? ClimbIn.Onto : null;
    }
}
