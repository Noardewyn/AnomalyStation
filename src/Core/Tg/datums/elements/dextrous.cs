// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/dextrous.dm
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
 * Sets up the attachee to have hands and manages things like dropping items on death and displaying them on examine
 * Actual hand performance is managed by code on /living/ and not encapsulated here, we just enable it
 */
// tg: code/datums/elements/dextrous.dm:7
public partial class datum_element_dextrous : datum_element
{
    // tg: code/datums/elements/dextrous.dm:7
    [DmParams("target", "hands_count", "hud_type", "can_throw")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object hands_count = Dm.arg(__extra, 0) ?? 2f;
        Type hud_type = Dm.arg<Type>(__extra, 1) ?? typeof(datum_hud_dextrous);
        object can_throw = Dm.arg(__extra, 2) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target) || iscarbon(target))
            return ELEMENT_INCOMPATIBLE; // Incompatible with the carbon typepath because that already has its own hand handling and doesn't need hand holding

        mob_living mob_parent = target as mob_living;
        await set_available_hands(mob_parent, hands_count);
        if (Dm.truthy(can_throw))
            ADD_TRAIT(target, TRAIT_CAN_THROW_ITEMS, REF(this)); // need to add before hud setup
        mob_parent.hud_type = hud_type;
        if (mob_parent.hud_used != null)
        {
            await mob_parent.set_hud_used(Dm.@new(hud_type, target) as datum_hud);
            await mob_parent.hud_used.show_hud(mob_parent.hud_used.hud_version);
        }
        ADD_TRAIT(target, TRAIT_CAN_HOLD_ITEMS, REF(this));
        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));
        RegisterSignal(target, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_hand_clicked));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        return dot;
    }

    // tg: code/datums/elements/dextrous.dm:25
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        mob_living mob_parent = source as mob_living;
        Waiting.NoSleep(set_available_hands(mob_parent, initial(mob_parent, "default_num_hands")));
        Type initial_hud = initial(mob_parent, "hud_type") as Type;
        mob_parent.hud_type = initial_hud;
        if (mob_parent.hud_used != null)
        {
            Waiting.NoSleep(mob_parent.set_hud_used(Dm.@new(initial_hud, source) as datum_hud));
            Waiting.NoSleep(mob_parent.hud_used.show_hud(mob_parent.hud_used.hud_version));
        }
        REMOVE_TRAIT(source, TRAIT_CAN_HOLD_ITEMS, REF(this));
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_LIVING_DEATH, COMSIG_LIVING_UNARMED_ATTACK));
        return dot;
    }

    /// Set up how many hands we should have
    // tg: code/datums/elements/dextrous.dm:42
    public virtual async DmTask set_available_hands(mob_living hand_owner = null, object hands_count = null)
    {
        await hand_owner.drop_all_held_items();
        DmList held_items = Dm.list();
        for (int i = 1; i <= Dm.num(hands_count); i++)
            held_items.Add(null);
        hand_owner.held_items = held_items;
        hand_owner.set_num_hands(hands_count);
        hand_owner.set_usable_hands(hands_count);
    }

    /// Drop our shit when we die
    // tg: code/datums/elements/dextrous.dm:52
    [SignalHandler]
    public virtual void on_death(mob_living died = null, object gibbed = null)
    {
        Waiting.NoSleep(died.drop_all_held_items());
    }

    /// Try picking up items
    // tg: code/datums/elements/dextrous.dm:57
    [SignalHandler]
    public virtual object on_hand_clicked(mob_living hand_haver = null, atom target = null, object proximity = null, DmList modifiers = null)
    {
        if (!Dm.truthy(proximity) && target.loc != hand_haver)
        {
            if (target is obj_item obj_item && obj_item.atom_storage == null && !Dm.truthy(Dm.bitand(obj_item.item_flags, IN_STORAGE)))
                return NONE;
        }
        if (!isitem(target) && Dm.truthy(hand_haver.combat_mode))
            return NONE;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            Waiting.Detach(target.attack_hand_secondary(hand_haver, modifiers));
        else
            Waiting.Detach(target.attack_hand(hand_haver, modifiers));
        Waiting.Detach(hand_haver.update_held_items());
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Tell people what we are holding
    // tg: code/datums/elements/dextrous.dm:73
    [SignalHandler]
    public virtual void on_examined(mob_living examined = null, mob user = null, DmList examine_list = null)
    {
        foreach (obj_item held_item in Dm.each<obj_item>(examined.held_items))
        {
            if (Dm.truthy(Dm.bitand(held_item.item_flags, ABSTRACT | HAND_ITEM)) || HAS_TRAIT(held_item, TRAIT_EXAMINE_SKIP))
                continue;
            Dm.append(ref examine_list, span_info(Dm.text("[] [] [] in [] [].", examined.p_They(), examined.p_have(), held_item.examine_title(user), examined.p_their(), examined.get_held_index_name(examined.get_held_index_of_item(held_item)))));
        }
    }
}
