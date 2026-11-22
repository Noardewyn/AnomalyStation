// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/dismemberment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P2: can_attach_limb, try_attach_limb and its head and arm overrides; M6 slice 4: dismembering, dropping,
// replacing and regrowing limbs). A reattached limb's mood events since gaps MS1; the dismembered mob's memory since gaps
// package G4.

// tg: code/modules/surgery/bodyparts/dismemberment.dm:2
public partial class obj_item_bodypart : obj_item
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:2
    public virtual object can_dismember__proc(obj_item item = null)
    {
        if (Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_UNREMOVABLE)) || (owner != null && HAS_TRAIT(owner, TRAIT_NODISMEMBER)))
            return FALSE;
        return TRUE;
    }

    ///Remove target limb from its owner, with side effects.
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:8
    public virtual async DmTask<object> dismember(string dam_type = null, float? silent = null, object wounding_type = null)
    {
        dam_type ??= BRUTE;
        silent ??= TRUE;
        if (owner == null || Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_UNREMOVABLE)))
            return FALSE;
        mob_living_carbon limb_owner = owner;
        if (HAS_TRAIT(limb_owner, TRAIT_GODMODE) || HAS_TRAIT(limb_owner, TRAIT_NODISMEMBER))
            return FALSE;

        obj_item_bodypart affecting = limb_owner.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
        await affecting.receive_damage(clamp(brute_dam / 2 * affecting.body_damage_coeff, 15, 50), clamp(burn_dam / 2 * affecting.body_damage_coeff, 0, 50), wound_bonus: CANT_WOUND); //Damage the chest based on limb's existing damage
        if (!Dm.truthy(silent))
            limb_owner.visible_message(span_danger(I18n.T("<B>[]'s [] is violently dismembered!</B>", limb_owner, name)));
        Waiting.Detach(limb_owner.emote("scream")); // INVOKE_ASYNC
        playsound(limb_owner, Dm.resource("sound/effects/dismember.ogg"), 80, TRUE);
        await limb_owner.add_mood_event(Dm.text("dismembered_[]", body_zone), typeof(datum_mood_event_dismembered), this);
        limb_owner.add_mob_memory(typeof(datum_memory_was_dismembered), Dm.named("lost_limb", this));

        if (Dm.truthy(wounding_type))
            LAZYSET(ref limb_owner.body_zone_dismembered_by, body_zone, wounding_type);

        await drop_limb(dismembered: TRUE);
        await limb_owner.update_equipment_speed_mods(); // Update in case speed affecting item unequipped by dismemberment

        if (QDELETED(this)) //Could have dropped into lava/explosion/chasm/whatever
            return TRUE;
        if (dam_type == BURN)
        {
            await burn__proc();
            return TRUE;
        }

        // Assume we had our limb sliced/punched off by this point
        if (Dm.truthy(can_bleed()))
            await limb_owner.bleed(rand(20, 40));

        object direction = pick(GLOB.cardinals);
        float t_range = rand(2, Dm.num(Dm.max(throw_range / 2, 2)));
        turf target_turf = get_turf(this);
        for (int i = 1; i <= t_range - 1; i++)
        {
            turf new_turf = get_step(target_turf, Dm.num(direction));
            if (new_turf == null)
                break;
            target_turf = new_turf;
            if (Dm.truthy(new_turf.density))
                break;
        }
        await throw_at(target_turf, throw_range, throw_speed);

        return TRUE;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:54 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_chest : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:54
    public override async DmTask<object> dismember(string dam_type = null, float? silent = null, object wounding_type = null)
    {
        dam_type ??= BRUTE;
        silent ??= TRUE;
        if (owner == null)
            return FALSE;
        mob_living_carbon chest_owner = owner;
        if (Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_UNREMOVABLE)))
            return FALSE;
        if (HAS_TRAIT(chest_owner, TRAIT_NODISMEMBER))
            return FALSE;

        DmList dot = Dm.list();
        if (!Dm.eq(wounding_type, WOUND_BURN) && isturf(chest_owner.loc) && Dm.truthy(can_bleed()))
            await chest_owner.add_splatter_floor(chest_owner.loc as turf);
        playsound(get_turf(chest_owner), Dm.resource("sound/misc/splort.ogg"), 80, TRUE);

        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(contents))
        {
            object org_zone = check_zone(organ.zone);
            if (!Dm.eq(org_zone, BODY_ZONE_CHEST))
                continue;
            await organ.Remove(chest_owner);
            if (chest_owner.loc != null)
                await organ.forceMove(chest_owner.loc);
            Dm.append(ref dot, organ);
        }

        if (cavity_item != null)
        {
            await cavity_item.forceMove(chest_owner.loc);
            Dm.append(ref dot, cavity_item);
            cavity_item = null;
        }
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:83 (interleaved types, rules 1.9)
public partial class obj_item_bodypart : obj_item
{
    ///limb removal. The "special" argument is used for swapping a limb with a new one without the effects of losing a limb kicking in.
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:83
    public virtual async DmTask<object> drop_limb(object special = null, object dismembered = null, float? move_to_floor = null)
    {
        move_to_floor ??= TRUE;
        if (owner == null)
            return null;
        atom drop_loc = owner.drop_location() as atom;

        // SEND_SIGNAL(owner, COMSIG_CARBON_REMOVE_LIMB, src, special, dismembered) and SEND_SIGNAL(src, COMSIG_BODYPART_REMOVED,
        // owner, special, dismembered) (rules 11.1 (e), statements)
        if (Dm.truthy(owner._listen_lookup?[COMSIG_CARBON_REMOVE_LIMB]))
            owner._SendSignal(COMSIG_CARBON_REMOVE_LIMB, Dm.list(owner, this, special, dismembered));
        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_REMOVED]))
            this._SendSignal(COMSIG_BODYPART_REMOVED, Dm.list(this, owner, special, dismembered));
        Dm.mask(ref bodypart_flags, Dm.bitnot(BODYPART_IMPLANTED)); //limb is out and about, it can't really be considered an implant
        await add_mob_blood(owner);
        await owner.remove_bodypart(this, special);

        foreach (datum_scar scar in Dm.each_cast<datum_scar>(scars))
        {
            scar.victim = null;
            LAZYREMOVE(ref owner.all_scars, scar);
        }

        mob_living_carbon phantom_owner = await update_owner(null) as mob_living_carbon; // so we can still refer to the guy who lost their limb after said limb forgets 'em
        await update_limb(dropping_limb: TRUE);

        foreach (datum_wound wound in Dm.each_cast<datum_wound>(wounds))
            await wound.remove_wound(TRUE);

        if (!Dm.truthy(phantom_owner.has_embedded_objects()))
        {
            phantom_owner.clear_alert(ALERT_EMBEDDED_OBJECT);
            phantom_owner.clear_mood_event("embedded");
        }

        if (!Dm.truthy(special))
        {
            if (phantom_owner.dna != null)
            {
                foreach (datum_mutation mutation in Dm.each_cast<datum_mutation>(phantom_owner.dna.mutations)) //some mutations require having specific limbs to be kept.
                {
                    if (Dm.truthy(mutation.limb_req) && Dm.eq(mutation.limb_req, body_zone))
                    {
                        to_chat(phantom_owner, span_warning(I18n.T("You feel your [] deactivating from the loss of your []!", mutation, body_zone)));
                        await phantom_owner.dna.remove_mutation(mutation, mutation.sources);
                    }
                }
            }
        }

        await update_icon_dropped();
        phantom_owner.update_health_hud(); //update the healthdoll
        await phantom_owner.update_body();
        if (!Dm.truthy(special))
            phantom_owner.hud_used?.update_locked_slots();

        if (Dm.truthy(Dm.bitand(bodypart_flags, Dm.bitor(BODYPART_PSEUDOPART, BODYPART_STUMP))))
        {
            await drop_organs(phantom_owner); //Psuedoparts shouldn't have organs, but just in case
            if (!QDELING(this)) // we might be removed as a part of something qdeling us
                qdel(this);
            return null;
        }

        if (Dm.truthy(move_to_floor))
        {
            if (drop_loc == null) // drop_loc = null happens when a "dummy human" used for rendering icons on prefs screen gets its limbs replaced.
            {
                qdel(this);
                return null;
            }
            await forceMove(drop_loc);
        }

        // SEND_SIGNAL(phantom_owner, COMSIG_CARBON_POST_REMOVE_LIMB, src, special, dismembered) (rules 11.1 (e), a statement)
        if (Dm.truthy(phantom_owner._listen_lookup?[COMSIG_CARBON_POST_REMOVE_LIMB]))
            phantom_owner._SendSignal(COMSIG_CARBON_POST_REMOVE_LIMB, Dm.list(phantom_owner, this, special, dismembered));
        return null;
    }

    /**
     * try_dismember() is used, once we've confirmed that a flesh and bone bodypart has both the skin and bone mangled, to actually roll for it
     *
     * Mangling is calculated in update_wounds(), this simply makes the roll for whether we actually dismember or not
     * using how damaged the limb already is, and how much damage this blow was for. If we have a critical bone wound instead of just a severe, we add +10% to the roll.
     * Lastly, we choose which kind of dismember we want based on the wounding type we hit with. Note we don't care about all the normal mods or armor for this
     *
     * Arguments:
     * * wounding_type: Either WOUND_BLUNT, WOUND_SLASH, or WOUND_PIERCE, basically only matters for the dismember message
     * * wounding_dmg: The damage of the strike that prompted this roll, higher damage = higher chance
     * * wound_bonus: Not actually used right now, but maybe someday
     * * exposed_wound_bonus: ditto above
     */
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:148
    public virtual async DmTask<object> try_dismember(object wounding_type = null, object wounding_dmg = null, object wound_bonus = null, object exposed_wound_bonus = null)
    {
        if (!Dm.truthy(can_dismember__proc()))
            return null;

        if (Dm.num(wounding_dmg) < DISMEMBER_MINIMUM_DAMAGE)
            return null;

        float base_chance = Dm.num(wounding_dmg);
        base_chance += (Dm.num(get_damage()) / max_damage * 50); // how much damage we dealt with this blow, + 50% of the damage percentage we already had on this bodypart

        foreach (datum_wound iterated_wound in Dm.each_cast<datum_wound>(wounds))
            base_chance += Dm.num(iterated_wound.get_dismember_chance_bonus(base_chance));

        if (prob(base_chance))
        {
            datum_wound_loss dismembering = Dm.@new<datum_wound_loss>();
            return await dismembering.apply_dismember(this, wounding_type as string);
        }
        return null;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:165 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_chest : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:165
    public override async DmTask<object> drop_limb(object special = null, object dismembered = null, float? move_to_floor = null)
    {
        move_to_floor ??= TRUE;
        if (Dm.truthy(special))
            return await base.drop_limb(special, dismembered, move_to_floor);
        //if this is not a special drop, this is a mistake
        return FALSE;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:171 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_arm : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:171
    public override async DmTask<object> drop_limb(object special = null, object dismembered = null, float? move_to_floor = null)
    {
        move_to_floor ??= TRUE;
        object dot = null;
        mob_living_carbon arm_owner = owner;
        if (Dm.truthy(special) || arm_owner == null)
            return await base.drop_limb(special, dismembered, move_to_floor);
        if (arm_owner.hand_bodyparts[held_index] == this)
        {
            // We only want to do this if the limb being removed is the active hand part.
            // This catches situations where limbs are "hot-swapped" such as augmentations and roundstart prosthetics.
            await arm_owner.dropItemToGround(arm_owner.get_item_for_held_index(held_index) as obj_item, 1);
        }
        dot = await base.drop_limb(special, dismembered, move_to_floor);
        if (arm_owner.handcuffed != null)
        {
            obj_item lost_cuffs = arm_owner.handcuffed;
            await arm_owner.set_handcuffed(null);
            await arm_owner.dropItemToGround(lost_cuffs, force: TRUE);
        }
        arm_owner.hud_used?.update_inventory_slot(ITEM_SLOT_HANDS, held_index);
        if (arm_owner.num_hands == 0)
            await arm_owner.dropItemToGround(arm_owner.get_item_by_slot(ITEM_SLOT_GLOVES) as obj_item, force: TRUE);
        await arm_owner.update_worn_gloves(); //to remove the bloody hands overlay
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:189 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_leg : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:189
    public override async DmTask<object> drop_limb(object special = null, object dismembered = null, float? move_to_floor = null)
    {
        move_to_floor ??= TRUE;
        object dot = null;
        mob_living_carbon leg_owner = owner;
        dot = await base.drop_limb(special, dismembered, move_to_floor);
        if (Dm.truthy(special) || leg_owner == null)
            return dot;
        await leg_owner.dropItemToGround(leg_owner.legcuffed, force: TRUE);
        await leg_owner.dropItemToGround(leg_owner.get_item_by_slot(ITEM_SLOT_FEET) as obj_item, force: TRUE);
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:197 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:197
    public override async DmTask<object> drop_limb(object special = null, object dismembered = null, float? move_to_floor = null)
    {
        move_to_floor ??= TRUE;
        if (!Dm.truthy(special))
        {
            //Drop all worn head items
            foreach (obj_item head_item in Dm.each_cast<obj_item>(owner.get_items_by_slots(Dm.bitor(Dm.bitor(ITEM_SLOT_EYES, ITEM_SLOT_EARS), Dm.bitor(ITEM_SLOT_MASK, ITEM_SLOT_HEAD)))))
                await owner.dropItemToGround(head_item, force: TRUE);
        }

        //Handle dental implants
        foreach (datum_action_item_action_activate_pill pill_action in Dm.each<datum_action_item_action_activate_pill>(owner.actions))
        {
            await pill_action.Remove(owner);
            obj pill = pill_action.target as obj;
            if (pill != null)
                await pill.forceMove(this);
        }

        name = I18n.T("[]'s head", owner.real_name);

        /// If our owner loses their head, update their name as their face ~cannot be seen~ does not exist anymore
        if (ishuman(owner))
        {
            mob_living_carbon_human as_human = owner as mob_living_carbon_human;
            as_human.update_visible_name();
        }

        return await base.drop_limb(special, dismembered, move_to_floor);
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:220 (interleaved types, rules 1.9)
public partial class obj_item_bodypart : obj_item
{
    ///Try to attach this bodypart to a mob, while replacing one if it exists, does nothing if it fails. Returns TRUE on success, FALSE on failure.
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:220
    public virtual async DmTask<object> replace_limb(mob_living_carbon limb_owner = null)
    {
        if (limb_owner == null)
            return FALSE;
        obj_item_bodypart old_limb = limb_owner.get_bodypart(body_zone as string) as obj_item_bodypart;
        if (old_limb != null)
            await old_limb.drop_limb(TRUE);

        if (!Dm.truthy(await try_attach_limb(limb_owner, TRUE))) //If it failed to replace, re-attach their old limb as if nothing happened.
        {
            if (old_limb != null)
                await old_limb.try_attach_limb(limb_owner, TRUE);
            return FALSE;
        }
        return TRUE;
    }

    ///Checks if a limb qualifies as a BODYPART_IMPLANTED
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:232
    public virtual object check_for_frankenstein(mob_living_carbon_human monster = null)
    {
        if (monster == null)
            return FALSE;
        Type original_type = ((datum_species)monster.dna.species).bodypart_overrides[body_zone] as Type;
        if (original_type == null || !Dm.eq(limb_id, Dm.initial(original_type, (obj_item_bodypart p) => p.limb_id)))
            return TRUE;
        return FALSE;
    }

    ///Checks if you can attach a limb, returns TRUE if you can.
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:241
    public virtual object can_attach_limb(mob_living_carbon new_limb_owner = null, object special = null)
    {
        // SEND_SIGNAL(new_limb_owner, COMSIG_ATTEMPT_CARBON_ATTACH_LIMB, src, special) (rules 11.1 (e), a value)
        if (Dm.truthy(Dm.bitand((!Dm.truthy(new_limb_owner._listen_lookup?[COMSIG_ATTEMPT_CARBON_ATTACH_LIMB]) ? NONE : new_limb_owner._SendSignal(COMSIG_ATTEMPT_CARBON_ATTACH_LIMB, Dm.list(new_limb_owner, this, special))), COMPONENT_NO_ATTACH)))
            return FALSE;

        obj_item_bodypart_chest mob_chest = new_limb_owner.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        if (mob_chest != null && !Dm.truthy(Dm.bitand(mob_chest.acceptable_bodytype, bodytype)) && !Dm.truthy(Dm.bitand(mob_chest.acceptable_bodyshape, bodyshape)) && !Dm.truthy(special))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/dismemberment.dm:256
    public virtual async DmTask<object> try_attach_limb(mob_living_carbon new_limb_owner = null, object special = null, object lazy = null)
    {
        if (!Dm.truthy(can_attach_limb(new_limb_owner, special)))
            return FALSE;

        obj_item_bodypart existing_limb = new_limb_owner.get_bodypart(body_zone as string, include_stumps: TRUE) as obj_item_bodypart;
        if (existing_limb != null)
        {
            if (existing_limb.type != stump_typepath)
                stack_trace(Dm.text("Attempted to attach a limb to [] in zone [] where they already have a non-stump limb", new_limb_owner, body_zone), "code/modules/surgery/bodyparts/dismemberment.dm", 263);
            await existing_limb.drop_limb(special: TRUE);
            qdel(existing_limb);
        }

        // SEND_SIGNAL(new_limb_owner, COMSIG_CARBON_ATTACH_LIMB, src, special, lazy) and SEND_SIGNAL(src,
        // COMSIG_BODYPART_ATTACHED, new_limb_owner, special, lazy) (rules 11.1 (e), statements)
        if (Dm.truthy(new_limb_owner._listen_lookup?[COMSIG_CARBON_ATTACH_LIMB]))
            new_limb_owner._SendSignal(COMSIG_CARBON_ATTACH_LIMB, Dm.list(new_limb_owner, this, special, lazy));
        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_ATTACHED]))
            this._SendSignal(COMSIG_BODYPART_ATTACHED, Dm.list(this, new_limb_owner, special, lazy));
        await new_limb_owner.add_bodypart(this, special, lazy);

        if (!Dm.truthy(lazy))
        {
            LAZYREMOVE(ref new_limb_owner.body_zone_dismembered_by, body_zone);

            if (Dm.truthy(special)) //non conventional limb attachment
            {
                foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(new_limb_owner.organs))
                {
                    if (!Dm.eq(deprecise_zone(organ.zone), body_zone))
                        continue;
                    if (organ.bodypart_owner == this) // someone manually updated the organs already
                        continue;
                    await organ.bodypart_insert(this);
                }
            }

            foreach (datum_wound wound in Dm.each_cast<datum_wound>(wounds))
            {
                // we have to remove the wound from the limb wound list first, so that we can reapply it fresh with the new person
                // otherwise the wound thinks it's trying to replace an existing wound of the same type (itself) and fails/deletes itself
                LAZYREMOVE(ref wounds, wound);
                await wound.apply_wound(this, TRUE, wound_source: wound.wound_source);
            }

            if (Dm.truthy(new_limb_owner.mob_mood?.has_mood_of_category(Dm.text("dismembered_[]", body_zone))) && !Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_STUMP)))
            {
                new_limb_owner.clear_mood_event(Dm.text("dismembered_[]", body_zone));
                await new_limb_owner.add_mood_event(Dm.text("phantom_pain_[]", body_zone), typeof(datum_mood_event_reattachment), this);
            }
        }

        foreach (datum_scar scar in Dm.each_cast<datum_scar>(scars))
        {
            if (Dm.@in(scar, new_limb_owner.all_scars)) // prevent double scars from happening for whatever reason
                continue;
            scar.victim = new_limb_owner;
            LAZYADD(ref new_limb_owner.all_scars, scar);
        }

        update_bodypart_damage_state();
        if (Dm.truthy(can_be_disabled))
            await update_disabled();

        if (!Dm.truthy(lazy))
        {
            // Bodyparts need to be sorted for leg masking to be done properly. It also will allow for some predictable
            // behavior within said bodyparts list. We sort it here, as it's the only place we make changes to bodyparts.
            new_limb_owner.bodyparts = sort_list(new_limb_owner.bodyparts, "/proc/" + nameof(GlobalProcs.cmp_bodypart_by_body_part_asc)) as DmList;
            await new_limb_owner.updatehealth();
            await new_limb_owner.update_body();
            await new_limb_owner.update_damage_overlays();
            if (!Dm.truthy(special))
                new_limb_owner.hud_used?.update_locked_slots();
        }

        // SEND_SIGNAL(new_limb_owner, COMSIG_CARBON_POST_ATTACH_LIMB, src, special, lazy) (rules 11.1 (e), a statement)
        if (Dm.truthy(new_limb_owner._listen_lookup?[COMSIG_CARBON_POST_ATTACH_LIMB]))
            new_limb_owner._SendSignal(COMSIG_CARBON_POST_ATTACH_LIMB, Dm.list(new_limb_owner, this, special, lazy));
        return TRUE;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:315 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:315
    [DmParams("new_head_owner", "special", "lazy")]
    public override async DmTask<object> try_attach_limb(mob_living_carbon new_limb_owner = null, object special = null, object lazy = null)
    {
        mob_living_carbon new_head_owner = new_limb_owner;
        object dot = null;
        // These are stored before calling super. This is so that if the head is from a different body, it persists its appearance.
        string old_real_name = real_name;
        dot = await base.try_attach_limb(new_head_owner, special, lazy);
        if (!Dm.truthy(dot) || Dm.truthy(lazy))
            return dot;

        if (Dm.truthy(old_real_name))
            new_head_owner.real_name = old_real_name;
        real_name = Dm.str(new_head_owner.real_name);

        /// Update our owner's name with ours
        if (ishuman(owner))
        {
            mob_living_carbon_human as_human = owner as mob_living_carbon_human;
            as_human.update_visible_name();
        }

        //Handle dental implants
        foreach (obj_item_reagent_containers_applicator_pill pill in Dm.each<obj_item_reagent_containers_applicator_pill>(this))
        {
            foreach (datum_action_item_action_activate_pill pill_action in Dm.each<datum_action_item_action_activate_pill>(pill.actions))
            {
                await pill.forceMove(new_head_owner);
                await pill_action.Grant(new_head_owner);
                break;
            }
        }

        ///Transfer existing hair properties to the new human.
        if (!Dm.truthy(special) && ishuman(new_head_owner))
        {
            mob_living_carbon_human sexy_chad = new_head_owner as mob_living_carbon_human;
            sexy_chad.hairstyle = hairstyle;
            sexy_chad.hair_color = hair_color;
            sexy_chad.facial_hairstyle = facial_hairstyle;
            sexy_chad.facial_hair_color = facial_hair_color;
            sexy_chad.grad_style = LAZYCOPY(gradient_styles);
            sexy_chad.grad_color = LAZYCOPY(gradient_colors);
            sexy_chad.lip_style = lip_style;
            sexy_chad.lip_color = Dm.str(lip_color);
        }

        await new_head_owner.updatehealth();
        await new_head_owner.update_body(); // updates lips + hair + eyes
        await new_head_owner.update_damage_overlays();
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:354 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_arm : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:354
    [DmParams("new_arm_owner", "special", "lazy")]
    public override async DmTask<object> try_attach_limb(mob_living_carbon new_limb_owner = null, object special = null, object lazy = null)
    {
        mob_living_carbon new_arm_owner = new_limb_owner;
        object dot = null;
        dot = await base.try_attach_limb(new_arm_owner, special, lazy);
        if (Dm.truthy(dot) && !Dm.truthy(lazy))
        {
            await new_arm_owner.update_worn_gloves(); // To apply bloody hands overlay
        }
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/dismemberment.dm:359
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/bodyparts/dismemberment.dm:359
    public virtual async DmTask regenerate_limbs(DmList excluded_zones = null)
    {
        excluded_zones ??= Dm.list();
        // SEND_SIGNAL(src, COMSIG_CARBON_REGENERATE_LIMBS, excluded_zones) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_REGENERATE_LIMBS]))
            this._SendSignal(COMSIG_CARBON_REGENERATE_LIMBS, Dm.list(this, excluded_zones));
        DmList zone_list = get_all_limbs() as DmList;

        DmList dismembered_by_copy = body_zone_dismembered_by?.Copy();

        if (Dm.truthy(length(excluded_zones)))
            Dm.remove(ref zone_list, excluded_zones);
        if (HAS_TRAIT(this, TRAIT_BLOCK_ATTACHING_LEGS))
            Dm.remove(ref zone_list, GLOB.leg_zones);
        foreach (object limb_zone in Dm.each(zone_list))
            await regenerate_limb(limb_zone, dismembered_by_copy);
    }

    // tg: code/modules/surgery/bodyparts/dismemberment.dm:372
    public virtual async DmTask<object> regenerate_limb(object limb_zone = null, DmList dismembered_by_copy = null)
    {
        dismembered_by_copy ??= body_zone_dismembered_by?.Copy();
        obj_item_bodypart limb = null;
        if (Dm.truthy(get_bodypart(limb_zone as string)))
            return FALSE;
        limb = await newBodyPart(limb_zone) as obj_item_bodypart; // tg passes newBodyPart(limb_zone, 0, 0): its one parameter is the zone
        if (limb == null)
            return null;
        if (!Dm.truthy(await limb.try_attach_limb(this, TRUE)))
        {
            qdel(limb);
            return FALSE;
        }
        await limb.update_limb(is_creating: TRUE);
        if (Dm.truthy(LAZYLEN(dismembered_by_copy)))
        {
            datum_scar scaries = Dm.@new<datum_scar>();
            datum_wound_loss phantom_loss = Dm.@new<datum_wound_loss>(); // stolen valor, really
            phantom_loss.loss_wounding_type = dismembered_by_copy?[limb_zone];
            if (Dm.truthy(phantom_loss.loss_wounding_type))
            {
                scaries.generate(limb, phantom_loss);
                LAZYREMOVE(ref dismembered_by_copy, limb_zone); // in case we're using a passed list
            }
            else
            {
                qdel(scaries);
                qdel(phantom_loss);
            }
        }

        //Copied from /datum/species/proc/on_species_gain()
        foreach (Type organ_path in Dm.each_cast<Type>(((datum_species)dna.species).mutant_organs))
        {
            //Load a persons preferences from DNA
            object zone = Dm.initial(organ_path, (obj_item_organ p) => p.zone);
            if (!Dm.eq(zone, limb_zone))
                continue;
            obj_item_organ new_organ = await SSwardrobe.provide_type(organ_path) as obj_item_organ;
            await new_organ.Insert(this);
        }

        await update_body_parts();
        return TRUE;
    }
}
