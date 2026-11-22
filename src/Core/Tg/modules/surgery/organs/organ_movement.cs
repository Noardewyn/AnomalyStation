// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/organ_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P2, scope.md 2e "organs"; the surgical procs on_surgical_removal, on_surgical_insertion, pre_surgical_insertion,
// swap_zone and get_organ_blood_color with the surgery operations, gaps P1).

// There are two kinds of organ movement: mob movement and limb movement
// If you pull someones brain out, you remove it from the mob and the limb
// If you take someones head off, you remove it from the mob but not the limb
// If you remove the brain from an already decapitated head, you remove it from the limb but not the mob

// Keep the seperation of limb removal and mob removal absolute

// tg: code/modules/surgery/organs/organ_movement.dm:15
public partial class obj_item_organ : obj_item
{
    // tg: code/modules/surgery/organs/organ_movement.dm:15
    [ShouldCallParent]
    public virtual async DmTask<object> Insert(mob_living_carbon receiver = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;

        if (!Dm.truthy(await mob_insert(receiver, special, movement_flags)))
            return FALSE;
        if (bodypart_owner != null && loc == bodypart_owner && receiver == bodypart_owner.owner)
            // ok this is a bit confusing but essentially, thanks to some EXTREME shenanigans
            // (tl;dr mob_insert -> set_species -> replace_limb -> bodypart_insert)
            // mob_insert can result in bodypart_insert being handled already
            // to avoid double insertion, and potential bugs, we'll stop here
            return TRUE;

        await bodypart_insert(limb_owner: receiver, movement_flags: movement_flags);

        if (!Dm.truthy(special) && !Dm.truthy(Dm.bitand(receiver.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await receiver.update_body_parts();

        return TRUE;
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:40
    [ShouldCallParent]
    public virtual async DmTask Remove(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;

        await mob_remove(organ_owner, special, movement_flags);
        await bodypart_remove(limb_owner: organ_owner, movement_flags: movement_flags);

        if (!Dm.truthy(special) && !Dm.truthy(Dm.bitand(organ_owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await organ_owner.update_body_parts();
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:56
    [ShouldNotOverride]
    public virtual async DmTask<object> mob_insert(object receiver = null, object special = null, object movement_flags = null)
    {
        if (!iscarbon(receiver))
        {
            //We try to insert the organ in a corgi when running the test, expecting it to return FALSE.
            if (!Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(organ_sanity): FALSE in production (rules 1.7)
                stack_trace(Dm.text("Tried to insert organ into non-carbon: []", ((datum)receiver).type), "code/modules/surgery/organs/organ_movement.dm", 63);
            return FALSE;
        }
        mob_living_carbon receiver__carbon = receiver as mob_living_carbon;

        if (owner == receiver__carbon)
        {
            stack_trace("Organ receiver is already organ owner", "code/modules/surgery/organs/organ_movement.dm", 67);
            return FALSE;
        }

        obj_item_organ replaced = receiver__carbon.get_organ_slot(slot) as obj_item_organ;
        if (replaced != null)
        {
            await replaced.Remove(receiver__carbon, special: TRUE);
            if (Dm.truthy(Dm.bitand(movement_flags, DELETE_IF_REPLACED)))
                qdel(replaced);
            else
                await replaced.forceMove(get_turf(receiver__carbon));
        }

        if (!Dm.truthy(IS_ROBOTIC_ORGAN(this)) && Dm.truthy(Dm.bitand(organ_flags, ORGAN_VIRGIN)))
        {
            blood_dna_info = receiver__carbon.get_blood_dna_list() as DmList;
            // need to remove the synethic blood DNA that is initialized
            // wash also adds the blood dna again
            // tg: code/modules/surgery/organs/organ_movement.dm:83
            await wash(CLEAN_TYPE_BLOOD);
            Dm.mask(ref organ_flags, Dm.bitnot(ORGAN_VIRGIN));
        }

        Dm.combine(ref receiver__carbon.organs, this);
        receiver__carbon.organs_slot[slot] = this;
        owner = receiver__carbon;

        await on_mob_insert(receiver__carbon, Dm.num_or_null(special), movement_flags);

        return TRUE;
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:95
    [ShouldCallParent]
    public virtual async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;

        foreach (object trait in Dm.each(organ_traits))
            ADD_TRAIT(organ_owner, trait, REF(this));

        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await action.Grant(organ_owner); // M4c slice 3: the action buttons

        foreach (Type effect in Dm.each_cast<Type>(organ_effects))
            await organ_owner.apply_status_effect(effect, type);

        if (!Dm.truthy(special))
            organ_owner.hud_used?.update_locked_slots();
        // SEND_SIGNAL(src, COMSIG_ORGAN_IMPLANTED, organ_owner) and SEND_SIGNAL(organ_owner, COMSIG_CARBON_GAIN_ORGAN, src,
        // special) (rules 11.1 (e), statements)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_IMPLANTED]))
            this._SendSignal(COMSIG_ORGAN_IMPLANTED, Dm.list(this, organ_owner));
        if (Dm.truthy(organ_owner._listen_lookup?[COMSIG_CARBON_GAIN_ORGAN]))
            organ_owner._SendSignal(COMSIG_CARBON_GAIN_ORGAN, Dm.list(organ_owner, this, special));

        // organs_slot must ALWAYS be ordered in the same way as organ_process_order
        // Otherwise life processing breaks down
        sortTim(owner.organs_slot, "/proc/" + nameof(GlobalProcs.cmp_organ_slot_asc));

        STOP_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:120
    [ShouldCallParent]
    public virtual async DmTask<object> bodypart_insert(obj_item_bodypart bodypart = null, mob_living_carbon limb_owner = null, object movement_flags = null)
    {
        if (limb_owner != null)
            bodypart = limb_owner.get_bodypart(deprecise_zone(zone) as string) as obj_item_bodypart;

        if (bodypart_owner == bodypart)
            stack_trace("Organ bodypart_insert called when organ is already owned by that bodypart", "code/modules/surgery/organs/organ_movement.dm", 128);
        else if (bodypart_owner != null)
            stack_trace("Organ bodypart_insert called when organ is already owned by a different bodypart", "code/modules/surgery/organs/organ_movement.dm", 130);

        // In the event that we're already in the bodypart, DO NOT MOVE IT! otherwise it triggers forced_removal
        if (loc != bodypart)
            await forceMove(bodypart); // The true movement

        // Don't re-register if we are already owned
        if (bodypart_owner != bodypart)
        {
            bodypart_owner = bodypart;
            RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(forced_removal));
            // Apply unique side-effects. Return value does not matter.
            await on_bodypart_insert(bodypart);
        }

        return TRUE;
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:145
    [ShouldCallParent]
    public virtual async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        Dm.combine(ref item_flags, ABSTRACT);
        ADD_TRAIT(this, TRAIT_NODROP, ORGAN_INSIDE_BODY_TRAIT);
        Dm.mask(ref interaction_flags_item, Dm.bitnot(INTERACT_ITEM_ATTACK_HAND_PICKUP));

        if (Dm.truthy(external_bodytypes))
            limb.owner?.synchronize_bodytypes();
        if (Dm.truthy(external_bodyshapes))
            limb.owner?.synchronize_bodyshapes();

        if (Dm.truthy(bodypart_overlay))
            await limb.add_bodypart_overlay(bodypart_overlay);

        // SEND_SIGNAL(src, COMSIG_ORGAN_BODYPART_INSERTED, limb) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_BODYPART_INSERTED]))
            this._SendSignal(COMSIG_ORGAN_BODYPART_INSERTED, Dm.list(this, limb));
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:168
    [ShouldNotOverride]
    public virtual async DmTask<object> mob_remove(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;

        if (organ_owner != null)
        {
            if (Dm.eq(organ_owner.organs_slot[slot], this))
                organ_owner.organs_slot.Remove(slot);
            Dm.remove(ref organ_owner.organs, this);
        }

        owner = null;
        await on_mob_remove(organ_owner, special, movement_flags);
        return TRUE;
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:183
    [ShouldCallParent]
    public virtual async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        // the slot types organ_owner as object (slots.tsv: a type test of 3.4 reads it on the failing path)
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;

        if (!iscarbon(organ_owner))
            stack_trace(Dm.text("Organ removal should not be happening on non carbon mobs: []", organ_owner), "code/modules/surgery/organs/organ_movement.dm", 184);

        foreach (object trait in Dm.each(organ_traits))
            REMOVE_TRAIT(organ_owner, trait, REF(this));

        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await action.Remove(organ_owner as mob); // M4c slice 3: the action buttons

        foreach (Type effect in Dm.each_cast<Type>(organ_effects))
            organ_owner__carbon.remove_status_effect(effect, type);

        // SEND_SIGNAL(src, COMSIG_ORGAN_REMOVED, organ_owner) and SEND_SIGNAL(organ_owner, COMSIG_CARBON_LOSE_ORGAN, src, special)
        // (rules 11.1 (e), statements)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_REMOVED]))
            this._SendSignal(COMSIG_ORGAN_REMOVED, Dm.list(this, organ_owner));
        if (Dm.truthy(((datum)organ_owner)._listen_lookup?[COMSIG_CARBON_LOSE_ORGAN]))
            ((datum)organ_owner)._SendSignal(COMSIG_CARBON_LOSE_ORGAN, Dm.list(organ_owner, this, special));
        if (organ_owner__carbon.mind != null)
            ADD_TRAIT(this, TRAIT_ORGAN_USED_BY_PLAYER, ORGAN_TRAIT);

        if (!Dm.truthy(special))
            organ_owner__carbon.hud_used?.update_locked_slots();

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_VITAL)) && !Dm.truthy(special) && !HAS_TRAIT(organ_owner, TRAIT_GODMODE))
        {
            if (organ_owner__carbon.stat != DEAD)
            {
                organ_owner__carbon.investigate_log(Dm.text("has been killed by losing a vital organ ([]).", this), INVESTIGATE_DEATHS);
            }
            await organ_owner__carbon.death();
        }

        START_PROCESSING(SSobj, this);

        DmList diseases = await organ_owner__carbon.get_static_viruses() as DmList;
        if (!Dm.truthy(LAZYLEN(diseases)))
            return;

        DmList diseases_to_add = Dm.list(); // gaps VI1 and OG1: the organ carries the owner's diseases
        foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
        {
            // robotic organs are immune to disease unless 'inorganic biology' symptom is present
            if (Dm.truthy(IS_ROBOTIC_ORGAN(this)) && !Dm.truthy(Dm.bitand(disease.infectable_biotypes, MOB_ROBOTIC)))
                continue;

            // admin or special viruses that should not be reproduced
            if (Dm.truthy(Dm.bitand(disease.spread_flags, DISEASE_SPREAD_SPECIAL | DISEASE_SPREAD_NON_CONTAGIOUS)))
                continue;

            Dm.append(diseases_to_add, disease);
        }

        if (Dm.truthy(LAZYLEN(diseases_to_add)))
            await AddComponent(typeof(datum_component_infective), diseases_to_add);
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:234
    [ShouldCallParent]
    public virtual async DmTask<object> bodypart_remove(obj_item_bodypart limb = null, mob_living_carbon limb_owner = null, object movement_flags = null)
    {
        if (limb_owner != null)
            limb = limb_owner.get_bodypart(deprecise_zone(zone) as string) as obj_item_bodypart;

        UnregisterSignal(this, COMSIG_MOVABLE_MOVED); //DONT MOVE THIS!!!! we moves the organ right after, so we unregister before we move them physically

        // The true movement is here
        await moveToNullspace();
        bodypart_owner = null;

        if (limb != null)
            await on_bodypart_remove(limb);

        return TRUE;
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:252
    [ShouldCallParent]
    public virtual async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        if (!Dm.truthy(IS_ROBOTIC_ORGAN(this)) && !Dm.truthy(Dm.bitand(item_flags, NO_BLOOD_ON_ITEM)) && !QDELING(this))
        {
            object blood_color = await get_color_from_blood_list(blood_dna_info);
            if (Dm.truthy(blood_color))
            {
                // tg: code/modules/surgery/organs/organ_movement.dm:256
                await AddElement(typeof(datum_element_decal_blood), Dm.named("_color", blood_color));
            }
        }

        Dm.mask(ref item_flags, Dm.bitnot(ABSTRACT));
        REMOVE_TRAIT(this, TRAIT_NODROP, ORGAN_INSIDE_BODY_TRAIT);
        Dm.combine(ref interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP);

        limb.owner?.synchronize_bodytypes();
        limb.owner?.synchronize_bodyshapes();

        // SEND_SIGNAL(src, COMSIG_ORGAN_BODYPART_REMOVED, limb) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_BODYPART_REMOVED]))
            this._SendSignal(COMSIG_ORGAN_BODYPART_REMOVED, Dm.list(this, limb));

        if (!Dm.truthy(bodypart_overlay))
            return;

        await limb.remove_bodypart_overlay(bodypart_overlay);

        if (Dm.truthy(use_mob_sprite_as_obj_sprite))
            update_appearance(UPDATE_OVERLAYS);

        if (Dm.truthy(greyscale_config))
            await get_greyscale_color_from_draw_color();
        else
            color = (bodypart_overlay as datum_bodypart_overlay_mutant).draw_color; // so a pink felinid doesn't drop a gray tail, read by name
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:283
    public virtual async DmTask get_greyscale_color_from_draw_color()
    {
        color = (bodypart_overlay as datum_bodypart_overlay_mutant).draw_color; //Defaults to the legacy behaviour of applying the color to the item.
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:287
    [SignalHandler]
    public virtual void forced_removal(datum source = null, atom old_loc = null, params object[] __extra)
    {
        if (owner != null)
        {
            if (loc?.loc == owner) // loc = some bodypart, loc.loc = some bodypart's owner
                stack_trace(Dm.text("Forced removal triggered on [] ([]) moving into the same mob [] ([])!", this, type, owner, owner.type), "code/modules/surgery/organs/organ_movement.dm", 295);
            else
                Waiting.NoSleep(Remove(owner));
        }
        else if (bodypart_owner != null)
        {
            if (loc == bodypart_owner)
                stack_trace(Dm.text("Forced removal triggered on [] ([]) moving into the same bodypart [] ([])!", this, type, bodypart_owner, bodypart_owner.type), "code/modules/surgery/organs/organ_movement.dm", 300);
            else
                Waiting.NoSleep(bodypart_remove(bodypart_owner));
        }
        else
            stack_trace("Force removed an already removed organ!", "code/modules/surgery/organs/organ_movement.dm", 304);
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:306
    [ShouldCallParent]
    public virtual async DmTask on_surgical_removal(mob_living user = null, obj_item_bodypart limb = null, obj_item tool = null)
    {
        // SEND_SIGNAL(src, COMSIG_ORGAN_SURGICALLY_REMOVED, user, limb.owner, limb.body_zone, tool) (rules 11.1 (e))
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_SURGICALLY_REMOVED]))
            this._SendSignal(COMSIG_ORGAN_SURGICALLY_REMOVED, Dm.list(this, user, limb.owner, limb.body_zone, tool));
        RemoveElement(typeof(datum_element_decal_blood), Dm.named("_color", await get_organ_blood_color(limb) is var or__1 && Dm.truthy(or__1) ? or__1 : BLOOD_COLOR_RED));
    }

    /**
     * Proc that gets called when the organ is surgically inserted by someone. Seem familiar?
     */
    // tg: code/modules/surgery/organs/organ_movement.dm:314
    [ShouldCallParent]
    public virtual void on_surgical_insertion(mob_living user = null, obj_item_bodypart limb = null)
    {
        // SEND_SIGNAL(src, COMSIG_ORGAN_SURGICALLY_INSERTED, user, limb.owner, limb.body_zone)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ORGAN_SURGICALLY_INSERTED]))
            this._SendSignal(COMSIG_ORGAN_SURGICALLY_INSERTED, Dm.list(this, user, limb.owner, limb.body_zone));
    }

    /// Proc that gets called when someone starts surgically inserting the organ
    // tg: code/modules/surgery/organs/organ_movement.dm:319
    public virtual async DmTask pre_surgical_insertion(mob_living user = null, mob_living_carbon new_owner = null, object target_zone = null)
    {
        if (Dm.truthy(valid_zones))
            await swap_zone(target_zone);
    }

    /// Readjusts the organ to fit into a different body zone/slot
    // tg: code/modules/surgery/organs/organ_movement.dm:324
    public virtual async DmTask swap_zone(object target_zone = null)
    {
        if (!Dm.truthy(valid_zones[target_zone]))
            throw Dm.CRASH(Dm.text("[]'s ([]) swap_zone was called with invalid zone []", this, type, target_zone), "code/modules/surgery/organs/organ_movement.dm", 326);
        zone = (string)target_zone;
        slot = valid_zones[zone];
    }

    // tg: code/modules/surgery/organs/organ_movement.dm:330
    public virtual async DmTask<object> get_organ_blood_color(obj_item_bodypart limb = null)
    {
        object blood_color = owner?.get_bloodtype() is datum_blood_type owner_blood ? await owner_blood.get_color() : null;
        if (limb == null)
            return blood_color;
        if (!Dm.truthy(blood_color))
            blood_color = limb.owner?.get_bloodtype() is datum_blood_type limb_blood ? await limb_blood.get_color() : null;
        if (!Dm.truthy(blood_color) && Dm.truthy(length(limb.blood_dna_info)))
            blood_color = await get_color_from_blood_list(limb.blood_dna_info);
        return blood_color;
    }
}
