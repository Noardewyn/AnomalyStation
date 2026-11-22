using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aClick.P1;

// M4a P1's fixtures: a capsule room (M2 P4's: a booted toy room with the click seam), stub items, a stub machine and a stub
// target human that trace the chain's calls. Items are put in a hand by tg's two assignments (held_items and the loc),
// since put_in_hands is M4a P2's.

/// <summary>A stub item that traces the chain (each override calls tg's base, so the base definitions run).</summary>
[DmPath("/obj/item/test_p1_stub")]
public class obj_item_test_p1_stub : obj_item
{
    public readonly List<string> Trace = new();

    // GAP(P2): tg's /obj/item/get_proxy_attacker_for is `return src` (items.dm:2148); items.cs still has the skeleton.
    public override object get_proxy_attacker_for(atom target = null, mob user = null) => this;

    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        Trace.Add("attack_self");
        return await base.attack_self(user, modifiers, __extra);
    }

    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        Trace.Add("attack_self_secondary");
        return await base.attack_self_secondary(user, modifiers);
    }

    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        Trace.Add("interact_with_atom " + Dm.str(modifiers?["icon-x"]) + "," + Dm.str(modifiers?["icon-y"]));
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        Trace.Add("ranged_interact_with_atom");
        return await base.ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        Trace.Add("pre_attack");
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }

    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        Trace.Add("afterattack");
        await base.afterattack(target, user, modifiers, attack_modifiers);
    }

    // tg's /obj/item/interact (item_attack.dm's attack_self calls it) is in the M3 tree; trace it as the parent's behaviour.
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        Trace.Add("interact");
        return null;
    }
}

/// <summary>A machine stand-in that answers the hand and the modifier clicks (its own procs are declared: no guard).</summary>
[DmPath("/obj/structure/test_p1_machine")]
public class obj_structure_test_p1_machine : obj_structure
{
    public readonly List<string> Trace = new();

    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        Trace.Add("attack_hand " + (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) ? "right" : "left"));
        return TRUE;
    }

    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        Trace.Add("attack_hand_secondary");
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    public override async DmTask<object> click_ctrl(mob user = null)
    {
        Trace.Add("click_ctrl");
        return CLICK_ACTION_SUCCESS;
    }

    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        Trace.Add("click_alt");
        return CLICK_ACTION_SUCCESS;
    }

    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        Trace.Add("click_alt_secondary");
        return CLICK_ACTION_SUCCESS;
    }

    public override async DmTask<object> click_ctrl_shift(mob user = null)
    {
        Trace.Add("click_ctrl_shift");
        return CLICK_ACTION_SUCCESS;
    }

    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        Trace.Add("item_interaction");
        return NONE;
    }

    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        Trace.Add("attacked_by");
        return 0;
    }
}

/// <summary>A human whose attacked_by traces and does no damage (tg's armour check is M4a P2's living_defense gap).</summary>
[DmPath("/mob/living/carbon/human/test_p1_target")]
public class mob_living_carbon_human_test_p1_target : mob_living_carbon_human
{
    public readonly List<string> Trace = new();

    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        Trace.Add("attacked_by " + attacking_item?.name);
        return 0;
    }
}

/// <summary>An abductor baton subtype that declares attack_self: the guard's walk stops at it.</summary>
[DmPath("/obj/item/melee/baton/abductor/test_p1_declares")]
public class obj_item_melee_baton_abductor_test_p1_declares : obj_item_melee_baton_abductor
{
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) => null;
}

/// <summary>An abductor baton subtype that declares nothing: the walk finds the baton's undeclared attack_self above it.</summary>
[DmPath("/obj/item/melee/baton/abductor/test_p1_plain")]
public class obj_item_melee_baton_abductor_test_p1_plain : obj_item_melee_baton_abductor
{
}

/// <summary>
/// The clicking human, with two gaps of other packages stood in: tg's /mob/living/can_perform_action (living.dm:1361, not
/// declared: the /mob base returns null, so no modifier click would reach its target) answers TRUE for an adjacent target, and
/// update_held_items does nothing (the human's held overlays are M4a P2's get_held_overlays gap).
/// </summary>
[DmPath("/mob/living/carbon/human/test_p1_user")]
public class mob_living_carbon_human_test_p1_user : mob_living_carbon_human
{
    public override async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
        => Dm.num(await target.Adjacent(this));

    public override async DmTask update_held_items()
    {
    }
}

internal static class P1
{
    public static (CapsuleRoom Room, mob_living_carbon_human A, RecordingSinks Sinks) Room(ulong seed)
    {
        var room = new CapsuleRoom(seed);
        RecordingSinks sinks = RecordingSinks.Install(room.World);
        mob_living_carbon_human a = room.Join<mob_living_carbon_human_test_p1_user>(5, 5, "Alice");
        room.In(() => ADD_TRAIT(a, TRAIT_ADVANCEDTOOLUSER, "test"));
        return (room, a, sinks);
    }

    public static T At<T>(CapsuleRoom room, int x, int y) where T : atom => room.In(() => Dm.@new<T>(locate(x, y, 1)));

    /// <summary>tg's hand assignment: the item in the active hand's slot and in the mob (put_in_hands is M4a P2's).</summary>
    public static T Hold<T>(CapsuleRoom room, mob_living_carbon_human who) where T : obj_item
    {
        T item = room.In(() => Dm.@new<T>(who));
        room.In(() => who.held_items[who.active_hand_index] = item);
        return item;
    }

    public static void Click(CapsuleRoom room, mob who, atom target, string @params)
    {
        room.Command(() => ClientSeam.Click(who.client, target, @params));
        room.Step();
    }

    public static IReadOnlyList<string> Deferred(CapsuleRoom room) => DmWorldState.Of(room.World).DeferredSites;
}
