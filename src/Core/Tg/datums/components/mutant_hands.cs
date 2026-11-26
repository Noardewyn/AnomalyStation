// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/mutant_hands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the zombie's claws are forced into every hand by this component. The var block is generated.

/**
 * ## Mutant hands component
 *
 * This component applies to humans, and forces them to hold
 * a certain typepath item in every hand no matter what*.
 *
 * For example, zombies being forced to hold "zombie claws" - disallowing them from holding items
 * but giving them powerful weapons to infect people
 *
 * It is suggested that the item path supplied has NODROP (and likely DROPDEL),
 * but nothing's preventing you from not having that.
 *
 * If they lose or gain hands, new mutant hands will be created immediately.
 *
 * Does not override nodrop items that already exist in hand slots.
 * However if those nodrop items are lost, will immediately create a new mutant hand.
 */
// tg: code/datums/components/mutant_hands.dm:20
public partial class datum_component_mutant_hands : datum_component
{
    // tg: code/datums/components/mutant_hands.dm:24
    [DmParams("mutant_hand_path")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ishuman(parent))
            return COMPONENT_INCOMPATIBLE;

        this.mutant_hand_path = Dm.arg(__extra, 0) as Type ?? typeof(obj_item_mutant_hand);
        return null;
    }

    // tg: code/datums/components/mutant_hands.dm:30
    public override async DmTask RegisterWithParent()
    {
        // Give them a hand before registering ANYTHING just so it's clean
        Waiting.Detach(apply_mutant_hands()); // INVOKE_ASYNC

        RegisterSignals(parent, Dm.list(COMSIG_CARBON_POST_ATTACH_LIMB, COMSIG_CARBON_POST_REMOVE_LIMB), nameof(try_reapply_hands));
        RegisterSignal(parent, COMSIG_MOB_EQUIPPED_ITEM, nameof(mob_equipped_item));
        RegisterSignal(parent, COMSIG_MOB_UNEQUIPPED_ITEM, nameof(mob_dropped_item));
    }

    // tg: code/datums/components/mutant_hands.dm:38
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_CARBON_POST_ATTACH_LIMB,
            COMSIG_CARBON_POST_REMOVE_LIMB,
            COMSIG_MOB_EQUIPPED_ITEM,
            COMSIG_MOB_UNEQUIPPED_ITEM));

        // Remove all their hands after unregistering everything so they don't return
        Waiting.Detach(remove_mutant_hands()); // INVOKE_ASYNC
    }

    /**
     * Tries to give the parent mob mutant hands.
     *
     * * If a hand slot is empty, places the mutanthand type into their hand.
     * * If a hand slot is filled with a nodrop item, it will do nothing.
     * * If a hand slot is filled with a non-nodrop item, drops the item to the ground.
     * * If a hand slot is filled with a hand already, does nothing.
     */
    // tg: code/datums/components/mutant_hands.dm:57
    public virtual async DmTask apply_mutant_hands()
    {
        mob_living_carbon_human human_parent = parent as mob_living_carbon_human;
        foreach (obj_item hand_slot in Dm.each(human_parent.held_items.Copy()))
        {
            // This slot is already a mutant hand
            if (istype(hand_slot, mutant_hand_path))
                continue;
            // This slot is not empty
            // Yes the held item lists contains nulls to represent empty hands
            if (hand_slot != null)
            {
                if (HAS_TRAIT(hand_slot, TRAIT_NODROP) || Dm.truthy(Dm.bitand(hand_slot.item_flags, ABSTRACT)))
                {
                    // There's a nodrop / abstract item in the way of putting a mutant hand in
                    // It can stay, for now, but if it gets dropped / unequipped we'll swoop in to replace the slot
                    continue;
                }
                // Drop any existing non-nodrop items to the ground
                await human_parent.dropItemToGround(hand_slot);
            }

            // Put in hands has a sleep somewhere in there
            await human_parent.put_in_hands(Dm.@new(mutant_hand_path) as obj_item, del_on_fail: TRUE);
        }
    }

    /**
     * Removes all mutant idems from the parent's hand slots
     */
    // tg: code/datums/components/mutant_hands.dm:80
    public virtual async DmTask remove_mutant_hands()
    {
        mob_living_carbon_human human_parent = parent as mob_living_carbon_human;
        foreach (obj_item hand_slot in Dm.each(human_parent.held_items.Copy()))
        {
            // Not a mutant hand, don't need to delete it
            if (hand_slot == null || !istype(hand_slot, mutant_hand_path))
                continue;

            // Just send it to the shadow realm, this will handle unequipping and remove it for us
            qdel(hand_slot);
        }
    }

    /**
     * Signal proc for any signals that may result in the number of hands of the parent mob changing
     *
     * Always try to re-insert mutanthands if we gain or lose hands
     */
    // tg: code/datums/components/mutant_hands.dm:95
    [SignalHandler]
    public virtual void try_reapply_hands(datum source = null, params object[] __extra)
    {
        if (QDELING(this) || QDELING(parent))
            return;

        Waiting.Detach(apply_mutant_hands()); // INVOKE_ASYNC
    }

    /**
     * Signal proc for [COMSIG_MOB_EQUIPPED_ITEM]
     *
     * This is a failsafe - the mob managed to pick up something that isn't a mutant hand
     */
    // tg: code/datums/components/mutant_hands.dm:108
    [SignalHandler]
    public virtual void mob_equipped_item(mob_living_carbon_human source = null, obj_item thing = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS))) // Who cares
            return;

        if (istype(thing, mutant_hand_path)) // This is definitely meant to be here
            return;

        if (HAS_TRAIT(thing, TRAIT_NODROP) || Dm.truthy(Dm.bitand(thing.item_flags, ABSTRACT))) // This is meant to be here
            return;

        // We equipped something to hands that wasn't a mutant hand, and wasn't abstract!
        // This means they're meant to have a mutant hand. So help them out.
        Waiting.Detach(apply_mutant_hands()); // INVOKE_ASYNC
    }

    /**
     * Signal proc for [COMSIG_MOB_UNEQUIPPED_ITEM]
     *
     * This is another failsafe - the mob dropped something, maybe from their hands, so try to re-equip
     */
    // tg: code/datums/components/mutant_hands.dm:129
    [SignalHandler]
    public virtual void mob_dropped_item(mob_living_carbon_human source = null, obj_item thing = null)
    {
        if (QDELING(this) || QDELING(parent))
            return;

        if (Dm.@in(null, source.held_items))
            Waiting.Detach(apply_mutant_hands()); // INVOKE_ASYNC
    }
}

/**
 * Generic mutant hand type for use with the mutant hands component
 * (Technically speaking, the component doesn't require you use this type. But it's here for posterity)
 *
 * Implements nothing except changing its icon state between left and right depending on hand slot equipped in
 */
// tg: code/datums/components/mutant_hands.dm:145
public partial class obj_item_mutant_hand : obj_item
{
    // tg: code/datums/components/mutant_hands.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        return dot;
    }

    // tg: code/datums/components/mutant_hands.dm:158
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        object dot = await base.visual_equipped(user, slot, initial);

        if (!Dm.truthy(base_icon_state))
            return dot;

        // We swap it intentionally here,
        // so right icon is shown on the left (Because hands)
        if (IS_LEFT_INDEX(Dm.num(user.get_held_index_of_item(this))))
            icon_state = Dm.text("[]_right", base_icon_state);
        else
            icon_state = Dm.text("[]_left", base_icon_state);
        return dot;
    }
}
