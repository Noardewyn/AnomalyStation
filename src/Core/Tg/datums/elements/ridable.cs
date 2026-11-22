// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ridable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * This element is used to indicate that a movable atom can be mounted by mobs in order to ride it. The movable is considered mounted when a mob is buckled to it,
 * at which point a [riding component][/datum/component/riding] is created on the movable, and that component handles the actual riding behavior.
 */
// tg: code/datums/elements/ridable.dm:10
public partial class datum_element_ridable : datum_element
{
    // tg: code/datums/elements/ridable.dm:17
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        object component_type = Dm.arg(__extra, 0) ?? typeof(datum_component_riding);
        object force_rider_standup = Dm.arg(__extra, 1) ?? TRUE;
        if (!ismovable(target))
            return COMPONENT_INCOMPATIBLE;

        if (Dm.eq(component_type, typeof(datum_component_riding)))
        {
            stack_trace(Dm.text("Tried attaching a ridable element to [] with basic/abstract /datum/component/riding component type. Please designate a specific riding component subtype when adding the ridable element.", target), "code/datums/elements/ridable.dm", 23);
            return COMPONENT_INCOMPATIBLE;
        }

        atom_movable movable = target as atom_movable;
        movable.can_buckle = TRUE;
        riding_component_type = component_type as Type;
        if (Dm.truthy(force_rider_standup))
            movable.buckle_lying = 0;
        RegisterSignal(target, COMSIG_MOVABLE_PREBUCKLE, nameof(check_mounting));
        if (isvehicle(target))
            RegisterSignal(target, COMSIG_SPEED_POTION_APPLIED, nameof(check_potion));
        if (ismob(target))
            RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        return dot;
    }

    // tg: code/datums/elements/ridable.dm:37
    public override object Detach(datum target = null, params object[] __extra)
    {
        atom_movable movable = target as atom_movable;
        movable.buckle_lying = Dm.num(initial(movable, "buckle_lying"));
        movable.can_buckle = Dm.num(initial(movable, "can_buckle"));
        UnregisterSignal(target, Dm.list(COMSIG_MOVABLE_PREBUCKLE, COMSIG_SPEED_POTION_APPLIED, COMSIG_MOB_STATCHANGE));
        return base.Detach(target, __extra);
    }

    /// Someone is buckling to this movable, which is literally the only thing we care about (other than speed potions)
    // tg: code/datums/elements/ridable.dm:44
    [SignalHandler]
    public virtual object check_mounting(atom_movable target_movable = null, mob_living potential_rider = null, object force = null, object ride_check_flags = null)
    {
        force ??= FALSE;
        ride_check_flags ??= NONE;
        if (HAS_TRAIT(potential_rider, TRAIT_CANT_RIDE))
            return NONE;
        int arms_needed = 0;
        if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_ARMS)))
            arms_needed = 2;
        else if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_ARM)))
        {
            arms_needed = 1;
            ride_check_flags = Dm.bitand(ride_check_flags, ~(int)RIDER_NEEDS_ARM);
            ride_check_flags = Dm.bitor(ride_check_flags, RIDER_NEEDS_ARMS);
        }

        if (arms_needed != 0 && !Dm.truthy(equip_buckle_inhands(potential_rider as mob_living_carbon_human, arms_needed, target_movable))) // can be either 1 (cyborg riding) or 2 (human piggybacking) hands
        {
            potential_rider.visible_message(span_warning(Dm.text("[] can't get a grip on [] because [] hands are full!", potential_rider, target_movable, potential_rider.p_their())),
                span_warning(Dm.text("You can't get a grip on [] because your hands are full!", target_movable)));
            return COMPONENT_BLOCK_BUCKLE;
        }

        if (Dm.truthy(Dm.bitand(ride_check_flags, RIDER_NEEDS_LEGS)) && HAS_TRAIT(potential_rider, TRAIT_FLOORED))
        {
            potential_rider.visible_message(span_warning(Dm.text("[] can't get [] footing on []!", potential_rider, potential_rider.p_their(), target_movable)),
                span_warning(Dm.text("You can't get your footing on []!", target_movable)));
            return COMPONENT_BLOCK_BUCKLE;
        }

        mob_living target_living = target_movable as mob_living;

        // need to see if !equip_buckle_inhands() checks are enough to skip any needed incapac/restrain checks
        // CARRIER_NEEDS_ARM shouldn't apply if the ridden isn't even a living mob
        if (Dm.truthy(Dm.bitand(ride_check_flags, CARRIER_NEEDS_ARM)) && !Dm.truthy(equip_buckle_inhands(target_living as mob_living_carbon_human, 1, target_living, potential_rider))) // hardcode 1 hand for now
        {
            target_living.visible_message(span_warning(Dm.text("[] can't get a grip on [] because [] hands are full!", target_living, potential_rider, target_living.p_their())),
                span_warning(Dm.text("You can't get a grip on [] because your hands are full!", potential_rider)));
            return COMPONENT_BLOCK_BUCKLE;
        }

        // tg: target_living.AddComponent(...): its typed local still holds a vehicle there (a DM type is only a hint), which the
        // as downcast above would null (gaps H4: the janicart); the movable is the same object
        Waiting.NoSleep(target_movable.AddComponent(riding_component_type, potential_rider, force, ride_check_flags));
        return null;
    }

    /// Try putting the appropriate number of [riding offhand items][/obj/item/riding_offhand] into the target's hands, return FALSE if we can't
    // tg: code/datums/elements/ridable.dm:79
    public virtual object equip_buckle_inhands(mob_living_carbon_human user = null, object amount_required = null, atom_movable target_movable = null, object riding_target_override = null)
    {
        float required = Dm.num(amount_required ?? 1f);
        atom_movable AM = target_movable;
        int amount_equipped = 0;
        for (float amount_needed = required; amount_needed > 0; amount_needed--)
        {
            obj_item_riding_offhand inhand = Dm.@new<obj_item_riding_offhand>(user);
            if (riding_target_override == null)
                inhand.rider = user;
            else
                inhand.rider = riding_target_override as mob_living_carbon;
            inhand.parent = AM as mob_living;

            foreach (obj_item I in Dm.each<obj_item>(user.held_items)) // delete any hand items like slappers that could still totally be used to grab on
            {
                if (Dm.truthy(Dm.bitand(I.item_flags, HAND_ITEM)))
                    qdel(I);
            }

            // this would be put_in_hands() if it didn't have the chance to sleep, since this proc gets called from a signal handler that relies on what this returns
            bool inserted_successfully = false;
            if (Dm.truthy(Waiting.NoSleep(user.put_in_active_hand(inhand))))
                inserted_successfully = true;
            else
            {
                object hand = user.get_empty_held_index_for_side(LEFT_HANDS);
                if (!Dm.truthy(hand))
                    hand = user.get_empty_held_index_for_side(RIGHT_HANDS);
                if (Dm.truthy(hand) && Dm.truthy(Waiting.NoSleep(user.put_in_hand(inhand, hand))))
                    inserted_successfully = true;
            }

            if (inserted_successfully)
                amount_equipped++;
            else
            {
                qdel(inhand);
                return FALSE;
            }
        }

        if (amount_equipped >= required)
            return TRUE;
        unequip_buckle_inhands(user, target_movable);
        return FALSE;
    }

    /// Checks to see if we've been hit with a red xenobio potion to make us faster. This is only registered if we're a vehicle
    // tg: code/datums/elements/ridable.dm:113
    [SignalHandler]
    public virtual object check_potion(atom_movable ridable_atom = null, obj_item speed_potion = null, mob_living user = null)
    {
        if (HAS_TRAIT(ridable_atom, TRAIT_SPEED_POTIONED))
        {
            to_chat(user, span_warning(Dm.text("[] has already been coated with red, that's as fast as it'll go!", ridable_atom)));
            return SPEED_POTION_STOP;
        }
        if (Dm.truthy(ridable_atom.has_buckled_mobs())) // effect won't take place til the next time someone mounts it, so just prevent that situation
        {
            to_chat(user, span_warning(Dm.text("It's too dangerous to smear [] on [] while it's being ridden!", speed_potion, ridable_atom)));
            return SPEED_POTION_STOP;
        }
        float speed_limit = Dm.round(Dm.num(config.Get(typeof(datum_config_entry_number_movedelay_run_delay))) * 0.85f, 0.01f);
        float theoretical_speed = Dm.num(initial(riding_component_type, "vehicle_move_delay"));
        if (theoretical_speed <= speed_limit) // i say speed but this is actually move delay, so you have to be ABOVE the speed limit to pass
        {
            to_chat(user, span_warning(Dm.text("[] can't be made any faster!", ridable_atom)));
            return SPEED_POTION_STOP;
        }
        return null;
    }

    /// Remove all of the relevant [riding offhand items][/obj/item/riding_offhand] from the target
    // tg: code/datums/elements/ridable.dm:129
    public virtual object unequip_buckle_inhands(mob_living_carbon user = null, atom_movable target_movable = null)
    {
        atom_movable AM = target_movable;
        foreach (obj_item_riding_offhand O in Dm.each<obj_item_riding_offhand>(user.contents))
        {
            if (O.parent != AM)
                throw Dm.CRASH("RIDING OFFHAND ON WRONG MOB");
            if (Dm.truthy(O.selfdeleting))
                continue;
            else
                qdel(O);
        }
        return TRUE;
    }

    /// When a mob's stat changes, it can no longer be ridden if it's dead.
    // tg: code/datums/elements/ridable.dm:140
    [SignalHandler]
    public virtual void on_stat_change(mob source = null)
    {
        // If we're dead, don't let anyone buckle onto us
        if (source.stat == DEAD)
        {
            source.can_buckle = FALSE;
            source.unbuckle_all_mobs();
        }
        // If we're alive, back to being buckle-able
        else
            source.can_buckle = TRUE;
    }
}

// tg: code/datums/elements/ridable.dm:157
public partial class obj_item_riding_offhand : obj_item
{
    // tg: code/datums/elements/ridable.dm:167
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        selfdeleting = TRUE;
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/datums/elements/ridable.dm:171
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        if (loc != rider && loc != parent)
        {
            selfdeleting = TRUE;
            qdel(this);
        }
        await base.equipped__proc(user, slot, initial);
    }

    // tg: code/datums/elements/ridable.dm:177
    public override object Destroy(float? force = null)
    {
        atom_movable AM = parent;
        if (Dm.truthy(selfdeleting))
        {
            if (Dm.@in(rider, AM.buckled_mobs))
                Waiting.NoSleep(AM.unbuckle_mob(rider));
        }
        return base.Destroy(force);
    }

    // tg: code/datums/elements/ridable.dm:183
    public override async DmTask<object> on_thrown(mob_living_carbon user = null, atom target = null)
    {
        if (rider == user)
            return null; //Piggyback user.
        await user.unbuckle_mob(rider);
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_notice(Dm.text("You gently let go of [].", rider)));
            return null;
        }
        return rider;
    }

    // tg: code/datums/elements/ridable.dm:192
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not atom_movable movable || !Dm.truthy(movable.can_buckle))
            return NONE;

        if (rider == user) // Piggyback user
            return ITEM_INTERACT_BLOCKING;

        mob_living former_rider = rider;
        await user.unbuckle_mob(former_rider);
        await former_rider.forceMove(get_turf(interacting_with));
        return Dm.truthy(await movable.mouse_buckle_handling(former_rider, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }
}
