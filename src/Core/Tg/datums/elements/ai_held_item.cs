// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ai_held_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * # AI Held Item Element
 *
 * Manages holding an item for a mob which doesn't have hands but needs to for AI purposes.
 */
// tg: code/datums/elements/ai_held_item.dm:8
public partial class datum_element_ai_held_item : datum_element
{
    // tg: code/datums/elements/ai_held_item.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        mob_living living_target = target as mob_living;
        if (!Dm.truthy(living_target.ai_controller))
            return ELEMENT_INCOMPATIBLE; // If there's no blackboard this would need to be a component

        RegisterSignal(target, COMSIG_ATOM_ATTACK_HAND, nameof(on_click));
        RegisterSignal(target, COMSIG_ATOM_EXITED, nameof(atom_exited));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignals(target, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(on_death));
        return dot;
    }

    /// Returns the item held in a mob's blackboard, if it has one
    // tg: code/datums/elements/ai_held_item.dm:22
    public virtual obj_item get_held_item(mob_living source = null)
    {
        return (source.ai_controller as datum_ai_controller).blackboard[BB_SIMPLE_CARRY_ITEM] as obj_item;
    }

    /// Someone's interacting with us by hand, if we have an item and like them we'll hand it over
    // tg: code/datums/elements/ai_held_item.dm:26
    [SignalHandler]
    public virtual void on_click(mob_living source = null, mob_living user = null)
    {
        if (Dm.truthy(user.combat_mode))
            return;

        datum_ai_controller controller = source.ai_controller as datum_ai_controller;
        if (!Dm.@in(user, controller.blackboard[BB_FRIENDS_LIST]))
            return; // We don't care about this bozo
        obj_item carried_item = get_held_item(source);
        if (carried_item == null)
            return;

        source.visible_message(span_danger(Dm.text("[] drops [] at []'s feet!", source, carried_item, user)));
        Waiting.NoSleep(carried_item.forceMove(get_turf(user)));
        controller.clear_blackboard_key(BB_SIMPLE_CARRY_ITEM);
    }

    /// If our held item is removed from our atom then take it off the blackboard
    // tg: code/datums/elements/ai_held_item.dm:43
    [SignalHandler]
    public virtual void atom_exited(mob_living source = null, atom_movable gone = null)
    {
        obj_item carried_item = get_held_item(source);
        if (carried_item == gone)
            (source.ai_controller as datum_ai_controller).clear_blackboard_key(BB_SIMPLE_CARRY_ITEM);
    }

    /// Report that we're holding an item.
    // tg: code/datums/elements/ai_held_item.dm:51
    [SignalHandler]
    public virtual void on_examined(mob_living source = null, mob user = null, DmList examine_text = null)
    {
        obj_item carried_item = get_held_item(source);
        if (carried_item == null)
            return;
        Dm.append(ref examine_text, span_notice(Dm.text("[] [] carrying [].", source.p_They(), source.p_are(), carried_item.examine_title(user))));
    }

    /// If we died, drop anything we were carrying
    // tg: code/datums/elements/ai_held_item.dm:60
    [SignalHandler]
    public virtual void on_death(mob_living ol_yeller = null)
    {
        obj_item carried_item = get_held_item(ol_yeller);
        if (carried_item == null)
            return;

        ol_yeller.visible_message(span_danger(Dm.text("[] drops [] as [] die[].", ol_yeller, carried_item, ol_yeller.p_they(), ol_yeller.p_s())));
        Waiting.NoSleep(carried_item.forceMove(ol_yeller.drop_location() as atom));
        (ol_yeller.ai_controller as datum_ai_controller).clear_blackboard_key(BB_SIMPLE_CARRY_ITEM);
    }
}
