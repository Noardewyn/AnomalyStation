// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/inhaler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD1, D333): the inhaler takes a canister, which shows under it, and puffs its reagents into the lungs of
// a breathing carbon whose mouth is bare (INHALE), its own user's quickly, someone else's after a struggle; the rotary display counts
// the puffs left; a broken canister sprays its contents as smoke. The var blocks are generated (D111).

// tg: code/modules/reagents/reagent_containers/inhaler.dm:2
public partial class obj_item_inhaler : obj_item
{
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ispath(initial_casister_path, typeof(obj_item_reagent_containers_inhaler_canister)))
            Waiting.NoSleep(set_canister(Dm.@new(initial_casister_path) as obj_item_reagent_containers_inhaler_canister));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:28
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref canister);

        return base.Destroy(force);
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:33
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        await base.handle_deconstruct(disassembled);

        if (canister != null)
            await canister.forceMove(drop_location() as atom);
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:38
    public virtual void update_canister_underlay()
    {
        if (canister == null)
        {
            Dm.remove(underlays, canister_underlay);
            canister_underlay = null;
        }
        else if (canister_underlay == null)
        {
            canister_underlay = mutable_appearance(canister.icon, canister.icon_state) as mutable_appearance;
            canister_underlay.pixel_z = canister_underlay_y_offset;
            Dm.append(underlays, canister_underlay);
        }
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (canister == null)
            return dot;

        Dm.append(ref dot, span_blue(I18n.T("It seems to have <b>[]</b> inserted.", canister)));
        if (!Dm.truthy(show_puffs_left))
            return dot;

        object puffs_left = canister.get_puffs_left();
        if (Dm.num(puffs_left) > 0)
            puffs_left = span_blue(Dm.text("[]", puffs_left));
        else
            puffs_left = span_danger(Dm.text("[]", puffs_left));
        Dm.append(ref dot, I18n.T("Its rotary display shows its canister can be used [] more times.", puffs_left));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:64
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);

        if (gone == canister)
            await set_canister(null, move_canister: false);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:71
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return await base.interact_with_atom(interacting_with, user, modifiers); // default behavior
        mob_living target_mob = (mob_living)interacting_with;

        if (!Dm.truthy(can_puff(target_mob, user)))
            return ITEM_INTERACT_BLOCKING;

        float puff_timer = 0;

        object pre_use_visible_message;
        object pre_use_self_message;
        object pre_use_target_message = null;

        object post_use_visible_message;
        object post_use_self_message;
        object post_use_target_message = null;

        if (target_mob == user) // no need for a target message
        {
            puff_timer = canister.self_administer_delay;

            pre_use_visible_message = span_notice(I18n.T("[] puts [] to [] lips, fingers on the canister...", user, this, user.p_their()));
            pre_use_self_message = span_notice(I18n.T("You put [] to your lips and put pressure on the canister...", this));

            post_use_visible_message = span_notice(I18n.T("[] takes a puff of []!", user, this));
            post_use_self_message = span_notice(I18n.T("You take a puff of []!", this));
        }
        else
        {
            puff_timer = canister.other_administer_delay;

            pre_use_visible_message = span_warning(I18n.T("[] tries to force [] between []'s lips...", user, this, target_mob));
            pre_use_self_message = span_notice(I18n.T("You try to put [] to []'s lips...", this, target_mob));
            pre_use_target_message = span_userdanger(I18n.T("[] tries to force [] between your lips!", user, this));

            post_use_visible_message = span_warning(I18n.T("[] forces [] between []'s lips and pushes the canister down!", user, this, target_mob));
            post_use_self_message = span_notice(I18n.T("You force [] between []'s lips and press on the canister!", this, target_mob));
            post_use_target_message = span_userdanger(I18n.T("[] forces [] between your lips and presses on the canister, filling your lungs with aerosol!", user, this));
        }

        if (puff_timer > 0)
        {
            user.visible_message(pre_use_visible_message, ignored_mobs: Dm.list(user, target_mob));
            to_chat(user, pre_use_self_message);
            if (Dm.truthy(pre_use_target_message))
                to_chat(target_mob, pre_use_target_message);
            if (!Dm.truthy(await do_after(user, puff_timer, target_mob)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(can_puff(target_mob, user))) // sanity
                return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(post_use_visible_message, ignored_mobs: Dm.list(user, target_mob));
        to_chat(user, post_use_self_message);
        if (Dm.truthy(post_use_target_message))
            to_chat(target_mob, post_use_target_message);

        await canister.puff(user, target_mob as mob_living_carbon);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:126
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await try_remove_canister(user as mob_living, modifiers);

        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:131
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_reagent_containers_inhaler_canister new_canister)
            return await try_insert_canister(new_canister, user, modifiers);

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    /// Tries to remove the canister, if any is inserted.
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:138
    public virtual async DmTask<object> try_remove_canister(mob_living user = null, object modifiers = null)
    {
        if (canister == null)
        {
            balloon_alert(user, I18n.T("no canister inserted!"));
            return FALSE;
        }

        if (canister.removal_time > 0)
        {
            balloon_alert(user, I18n.T("removing canister..."));
            if (!Dm.truthy(await do_after(user, canister.removal_time, this)))
                return FALSE;
        }

        balloon_alert(user, I18n.T("canister removed"));
        playsound(this, canister.post_insert_sound, canister.post_insert_volume);
        await set_canister(null, user);
        return null;
    }

    // Tries to insert a canister, if none is already inserted.
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:153
    public virtual async DmTask<object> try_insert_canister(obj_item_reagent_containers_inhaler_canister new_canister = null, mob_living user = null, object @params = null)
    {
        if (canister != null)
        {
            balloon_alert(user, I18n.T("remove the existing canister!"));
            return FALSE;
        }

        balloon_alert(user, I18n.T("inserting canister..."));
        playsound(this, new_canister.pre_insert_sound, new_canister.pre_insert_volume);
        if (!Dm.truthy(await do_after(user, new_canister.insertion_time, this)))
            return FALSE;
        playsound(this, new_canister.post_insert_sound, new_canister.post_insert_volume);
        balloon_alert(user, I18n.T("canister inserted"));
        await set_canister(new_canister, user);

        return TRUE;
    }

    /// Setter proc for [canister]. Moves the existing canister out of the inhaler, while moving a new canister inside and registering it.
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:169
    public virtual async DmTask set_canister(obj_item_reagent_containers_inhaler_canister new_canister = null, mob_living user = null, bool move_canister = true)
    {
        if (move_canister && canister != null)
        {
            if (iscarbon(loc))
            {
                mob_living_carbon carbon_loc = (mob_living_carbon)loc;
                Waiting.Detach(carbon_loc.put_in_hands(canister)); // INVOKE_ASYNC(carbon_loc, TYPE_PROC_REF(/mob/living/carbon, put_in_hands), canister)
            }
            else if (loc != null)
                await canister.forceMove(loc);
        }

        canister = new_canister;
        if (canister != null)
            await canister.forceMove(this);
        update_canister_underlay();
    }

    /// Determines if we can be used. Fails on no canister, empty canister, invalid targets, or non-breathing targets.
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:182
    public virtual object can_puff(mob_living target_mob = null, mob_living user = null, bool silent = false)
    {
        if (canister == null)
        {
            if (!silent)
                balloon_alert(user, I18n.T("no canister!"));
            return FALSE;
        }
        if (canister.reagents == null || Dm.num(((datum_reagents)canister.reagents).total_volume) <= 0)
        {
            if (!silent)
                balloon_alert(user, I18n.T("canister is empty!"));
            return FALSE;
        }
        if (!iscarbon(target_mob)) // maybe mix this into a general has mouth check
        {
            if (!silent)
                balloon_alert(user, I18n.T("not breathing!"));
            return FALSE;
        }
        mob_living_carbon carbon_target = (mob_living_carbon)target_mob;
        if (Dm.truthy(carbon_target.is_mouth_covered()))
        {
            if (!silent)
                balloon_alert(user, I18n.T("expose the mouth!"));
            return FALSE;
        }
        if (HAS_TRAIT(carbon_target, TRAIT_NOBREATH))
        {
            if (!silent)
                balloon_alert(user, I18n.T("not breathing!"));
            return FALSE;
        }
        obj_item_organ_lungs lungs = carbon_target.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (lungs == null || lungs.received_pressure_mult <= 0)
        {
            if (!silent)
                balloon_alert(user, I18n.T("not breathing!"));
            return FALSE;
        }

        return TRUE;
    }
}

// tg: code/modules/reagents/reagent_containers/inhaler.dm:213
public partial class obj_item_reagent_containers_inhaler_canister : obj_item_reagent_containers
{
    /// Called when a inhaler we are in is used on someone. Transfers reagents and plays the puff sound.
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:253
    public virtual async DmTask puff(mob_living user = null, mob_living_carbon target = null)
    {
        playsound(this, puff_sound, puff_volume, TRUE, -6);
        await ((datum_reagents)reagents).trans_to(target, amount_per_transfer_from_this, transferred_by: user, methods: INHALE);
    }

    /// Returns a integer approximating how many puffs we can be used for.
    // tg: code/modules/reagents/reagent_containers/inhaler.dm:258
    public virtual object get_puffs_left()
    {
        return ROUND_UP(Dm.num(((datum_reagents)reagents).total_volume) / Dm.num(amount_per_transfer_from_this));
    }

    // tg: code/modules/reagents/reagent_containers/inhaler.dm:261
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        if (!Dm.truthy(((datum_reagents)reagents)?.total_volume))
        {
            visible_message(span_warning(I18n.T("[] breaks open - but is empty!", this)));
            await base.handle_deconstruct(disassembled);
            return;
        }

        await do_chem_smoke(1, this, get_turf(this), carry: (datum_reagents)reagents, log: TRUE);
        visible_message(span_warning(I18n.T("[] breaks open and sprays its aerosilized contents everywhere!", this)));
        await base.handle_deconstruct(disassembled);
    }
}
