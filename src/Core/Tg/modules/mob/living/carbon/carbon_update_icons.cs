// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P3, scope 2e "with billboards", 2j A.2): the carbon's overlay layers (overlays_standing,
// apply_overlay/remove_overlay), the body parts' icons with the limb icon cache, the damage and bleeding overlays, the held
// items and cuffs, the multi-z plane procs.

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:2
public partial class mob_living_carbon : mob_living
{
    /// Updates features and clothing attached to a specific limb with limb-specific offsets
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:2
    public virtual async DmTask update_features(object feature_key = null)
    {
        object sw__1 = feature_key;
        if (Dm.eq(sw__1, OFFSET_UNIFORM))
            await update_worn_undersuit();
        else if (Dm.eq(sw__1, OFFSET_ID))
            await update_worn_id();
        else if (Dm.eq(sw__1, OFFSET_GLOVES))
            await update_worn_gloves();
        else if (Dm.eq(sw__1, OFFSET_GLASSES))
            await update_worn_glasses();
        else if (Dm.eq(sw__1, OFFSET_EARS))
            await update_worn_ears();
        else if (Dm.eq(sw__1, OFFSET_SHOES))
            await update_worn_shoes();
        else if (Dm.eq(sw__1, OFFSET_S_STORE))
            await update_suit_storage();
        else if (Dm.eq(sw__1, OFFSET_FACEMASK))
            await update_worn_mask();
        else if (Dm.eq(sw__1, OFFSET_HEAD))
            await update_worn_head();
        else if (Dm.eq(sw__1, OFFSET_FACE))
        {
            update_face_offset(); // updates eye and lipstick icon
            await update_worn_mask();
        }
        else if (Dm.eq(sw__1, OFFSET_BELT))
            await update_worn_belt();
        else if (Dm.eq(sw__1, OFFSET_BACK))
            await update_worn_back();
        else if (Dm.eq(sw__1, OFFSET_SUIT))
            await update_worn_oversuit();
        else if (Dm.eq(sw__1, OFFSET_NECK))
            await update_worn_neck();
        else if (Dm.eq(sw__1, OFFSET_HELD))
            await update_held_items();
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:37  var/list/overlays_standing[TOTAL_LAYERS]
    // GAP(P2, carbon_defines.cs): rules 10.2 put this list default into the type's dm_init_vars, which carbon_defines.cs
    // (P2's var block) holds; a second override cannot be declared here, so the list is made by the field initializer (a
    // DmList touches no World; nothing reads initial(overlays_standing)). Request: add
    // `overlays_standing = Dm.list_n(TOTAL_LAYERS);` to mob_living_carbon.dm_init_vars and drop this initializer.
    public DmList overlays_standing = Dm.list_n(TOTAL_LAYERS);

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:39
    public virtual object apply_overlay(object cache_index = null)
    {
        object dot = null;
        if (Dm.truthy(dot = overlays_standing[cache_index]))
            add_overlay(dot);
        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_APPLY_OVERLAY]))
            this._SendSignal(COMSIG_CARBON_APPLY_OVERLAY, Dm.list(this, cache_index, dot));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:44
    public virtual void remove_overlay(object cache_index = null)
    {
        object I = overlays_standing[cache_index];
        if (Dm.truthy(I))
        {
            cut_overlay(I);
            overlays_standing[cache_index] = null;
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_REMOVE_OVERLAY]))
            this._SendSignal(COMSIG_CARBON_REMOVE_OVERLAY, Dm.list(this, cache_index, I));
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:51
    [DmParams("is_creating")]
    public override async DmTask update_body(params object[] __extra)
    {
        float? is_creating = Dm.arg<float?>(__extra, 0);
        is_creating ??= FALSE;
        await update_body_parts(is_creating);
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:54
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        update_z_overlays(GET_TURF_PLANE_OFFSET(new_turf), TRUE);
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:60
    public virtual async DmTask refresh_loop(object iter_cnt = null, float? rebuild = null)
    {
        rebuild ??= FALSE;
        for (float i = 1, i__end = Dm.num(iter_cnt); i <= i__end; i++)
        {
            update_z_overlays(1, rebuild);
            await sleep(0.3f * SECONDS);
            update_z_overlays(0, rebuild);
            await sleep(0.3f * SECONDS);
        }
    }

    // Rebuilding is a hack. We should really store a list of indexes into our existing overlay list or SOMETHING
    // IDK. will work for now though, which is a lot better then not working at all
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:161
    public virtual void update_z_overlays(object new_offset = null, float? rebuild = null)
    {
        rebuild ??= FALSE;
        // Null entries will be filtered here
        for (float i = 1, i__end = length(overlays_standing); i <= i__end; i++)
        {
            object cache_grouping = overlays_standing[i];
            if (Dm.truthy(cache_grouping) && !islist(cache_grouping))
                cache_grouping = Dm.list(cache_grouping);
            // Need this so we can have an index, could build index into the list if we need to tho, check
            if (!Dm.truthy(length(cache_grouping)))
                continue;
            overlays_standing[i] = update_appearance_planes((DmList)cache_grouping, new_offset);
        }
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:232
    public override async DmTask<object> regenerate_icons()
    {
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;
        icon_render_keys = Dm.list(); //Clear this bad larry out
        await update_held_items();
        update_worn_handcuffs();
        update_worn_legcuffs();
        await update_body();
        update_appearance(UPDATE_OVERLAYS);
        return null;
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:242
    public override async DmTask update_held_items()
    {
        await base.update_held_items();
        remove_overlay(HANDS_LAYER);
        if (Dm.truthy(handcuffed))
        {
            await drop_all_held_items();
            return;
        }

        overlays_standing[HANDS_LAYER] = await get_held_overlays();
        apply_overlay(HANDS_LAYER);
    }

    /// Generate held item overlays
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:253
    public virtual async DmTask<object> get_held_overlays()
    {
        DmList hands = Dm.list();
        foreach (obj_item I in Dm.each<obj_item>(held_items))
        {
            object icon_file = I.lefthand_file;
            if (IS_RIGHT_INDEX(Dm.num(get_held_index_of_item(I))))
                icon_file = I.righthand_file;

            Dm.append(ref hands, await I.build_worn_icon(default_layer: HANDS_LAYER, default_icon_file: icon_file, isinhands: TRUE, bodyshape: bodyshape));
        }
        return hands;
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:263
    public virtual object get_fire_icon_state(object stacks = null, object on_fire = null)
    {
        return Dm.text("[]_[]", ((dna == null ? null : ((datum_species)dna.species).fire_overlay) is var or__1 && Dm.truthy(or__1) ? or__1 : "human"), (Dm.num(stacks) > MOB_BIG_FIRE_STACK_THRESHOLD ? "big_fire" : "small_fire"));
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:266
    public override object get_fire_overlay(object stacks = null, object on_fire = null)
    {
        object fire_icon = get_fire_icon_state(stacks, on_fire);
        DmList overrides = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_GET_FIRE_OVERLAY]))
            this._SendSignal(COMSIG_CARBON_GET_FIRE_OVERLAY, Dm.list(this, stacks, on_fire, fire_icon, overrides));
        if (Dm.truthy(length(overrides)))
            return overrides[1];

        if (Dm.truthy(GLOB.fire_appearances[fire_icon]))
            return GLOB.fire_appearances[fire_icon];

        GLOB.fire_appearances[fire_icon] = mutable_appearance(
            Dm.resource("icons/mob/effects/onfire.dmi"),
            fire_icon as string,
            -HIGHEST_LAYER,
            appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)
        );
        return GLOB.fire_appearances[fire_icon];
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:284
    public override async DmTask update_damage_overlays()
    {
        remove_overlay(DAMAGE_LAYER);

        mutable_appearance damage_overlay = null;
        foreach (obj_item_bodypart iter_part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            object part_overlays = await iter_part.get_bodypart_damage_state();
            if (!Dm.truthy(LAZYLEN(part_overlays)))
                continue;

            if (!Dm.truthy(damage_overlay))
                damage_overlay = mutable_appearance(layer: -DAMAGE_LAYER) as mutable_appearance;
            Dm.append(damage_overlay.overlays, part_overlays);
        }

        if (damage_overlay == null)
            return;

        apply_height(damage_overlay, ENTIRE_BODY);
        overlays_standing[DAMAGE_LAYER] = damage_overlay;
        apply_overlay(DAMAGE_LAYER);
    }

    /// Handles bleeding overlays
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:304
    public virtual async DmTask update_wound_overlays()
    {
        remove_overlay(WOUND_LAYER);

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (!Dm.truthy(blood_type) || !Dm.truthy(can_bleed()))
            return;

        mutable_appearance wound_overlay = null;
        foreach (obj_item_bodypart iter_part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(iter_part.bleed_overlay_icon))
            {
                mutable_appearance blood_overlay = mutable_appearance(Dm.resource("icons/mob/effects/bleed_overlays.dmi"), "blank", -WOUND_LAYER, appearance_flags: KEEP_TOGETHER) as mutable_appearance;
                blood_overlay.color = await blood_type.get_wound_color(this);
                if (!Dm.truthy(wound_overlay))
                    wound_overlay = blood_overlay;
                // wound_overlay.add_overlay(iter_part.bleed_overlay_icon): /image/proc/add_overlay(x) is `overlays |= x`
                // (controllers/subsystem/overlays.dm:113), which is not translated yet; its one statement is written here.
                // GAP(lead, controllers/subsystem/overlays.cs): translate the /image overlay procs (overlays.dm:113-131).
                Dm.combine(ref wound_overlay.overlays, iter_part.bleed_overlay_icon);
            }
        }

        if (wound_overlay == null)
            return;

        apply_height(wound_overlay, ENTIRE_BODY);
        overlays_standing[WOUND_LAYER] = wound_overlay;
        apply_overlay(WOUND_LAYER);
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:326
    public override void update_worn_legcuffs()
    {
        remove_overlay(LEGCUFF_LAYER);
        clear_alert("legcuffed");
        if (!Dm.truthy(legcuffed))
            return;
        if (Dm.truthy((legcuffed as obj_item_restraints_legcuffs)?.legcuff_state))
        {
            obj_item_restraints_legcuffs cuffs = legcuffed as obj_item_restraints_legcuffs;
            overlays_standing[LEGCUFF_LAYER] = mutable_appearance(Dm.resource("icons/mob/simple/mob.dmi"), cuffs.legcuff_state, -LEGCUFF_LAYER);
        }
        apply_overlay(LEGCUFF_LAYER);
        throw_alert("legcuffed", typeof(atom_movable_screen_alert_restrained_legcuffed), new_master: legcuffed);
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:337
    public override void update_worn_handcuffs()
    {
        remove_overlay(HANDCUFF_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_HANDS);
        if (Dm.truthy(handcuffed))
        {
            mutable_appearance handcuff_overlay = mutable_appearance(Dm.resource("icons/mob/simple/mob.dmi"), "handcuff1", -HANDCUFF_LAYER) as mutable_appearance;
            if (handcuffed.blocks_emissive != EMISSIVE_BLOCK_NONE)
            {
                // DEVIATION(DV12): emissive blockers are not created (lighting planes are not ported)
                Dm.Deferred("code/modules/mob/living/carbon/carbon_update_icons.dm:343", "update_worn_handcuffs: the emissive blocker of the handcuff overlay (DV12)");
            }
            apply_height(handcuff_overlay, LOWER_BODY); // low hanging
            overlays_standing[HANDCUFF_LAYER] = handcuff_overlay;
            apply_overlay(HANDCUFF_LAYER);
        }
    }

    ///Checks to see if any bodyparts need to be redrawn, then does so. update_limb_data = TRUE redraws the limbs to conform to the owner.
    ///Returns an integer representing the number of limbs that were updated.
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:372
    public virtual async DmTask<object> update_body_parts(object update_limb_data = null)
    {
        object dot = null;
        await update_damage_overlays();
        await update_wound_overlays();
        float limb_count_update = 0;
        float head_update = FALSE;
        DmList new_limbs = Dm.list();
        foreach ((object body_zone, object limb_untyped) in Dm.pairs(get_bodyparts_by_zones()))
        {
            obj_item_bodypart limb = limb_untyped as obj_item_bodypart;
            // IS_STUMP(limb) (__DEFINES/surgery.dm:67): no member yet, expanded inline (rules 11.1)
            if (limb == null || Dm.truthy(Dm.bitand(limb.bodypart_flags, BODYPART_STUMP)))
            {
                if (Dm.eq(body_zone, BODY_ZONE_HEAD))
                    head_update = TRUE;
                if (Dm.truthy(icon_render_keys[body_zone]))
                {
                    Dm.remove(ref icon_render_keys, body_zone);
                    limb_count_update += 1;
                }
                continue;
            }

            // Update limb actually doesn't do much, get_limb_icon is the cpu eater.
            await limb.update_limb(is_creating: Dm.num_or_null(update_limb_data));

            object old_key = icon_render_keys[limb.body_zone];
            object new_key = await limb.get_cache_key();

            if (Dm.eq(new_key, old_key))
                Dm.append(ref new_limbs, limb_icon_cache[new_key]);
            else
            {
                if (Dm.eq(body_zone, BODY_ZONE_HEAD))
                    head_update = TRUE;
                if (!Dm.truthy(limb_icon_cache[new_key]))
                    limb_icon_cache[new_key] = await limb.get_limb_icon(dropped: FALSE);
                Dm.append(ref new_limbs, limb_icon_cache[new_key]);
                icon_render_keys[limb.body_zone] = new_key;
                limb_count_update += 1;
            }
        }

        dot = limb_count_update;
        if (!Dm.truthy(dot))
            return dot;

        remove_overlay(BODYPARTS_LAYER);

        if (Dm.truthy(new_limbs.len))
            overlays_standing[BODYPARTS_LAYER] = new_limbs;

        apply_overlay(BODYPARTS_LAYER);
        // for legacy support, head changes triggers an eye/hair update
        // also run hair/eyes update on mob creation and other forced data updates to apply any custom changes made
        if (Dm.truthy(head_update) || Dm.truthy(update_limb_data))
        {
            await update_eyes();
            update_hair();
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:420
    public virtual void update_face_offset()
    {
        return;
    }
}

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:73 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:67  #define NEXT_PARENT_COMMAND "next_parent" (file-local, #undef at :230)
    internal const string NEXT_PARENT_COMMAND = "next_parent";

    /// Takes a list of mutable appearances
    /// Returns a list in the form:
    /// 1 - a list of all mutable appearances that would need to be updated to change planes in the event of a z layer change, alnongside the commands required
    /// 	to properly track parents to update
    /// 2 - a list of all parents that will require updating
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:73
    public static object build_planeed_apperance_queue(object appearances = null)
    {
        DmList queue;
        if (appearances is DmList list)
            queue = list.Copy();
        else
            queue = Dm.list(appearances);
        float queue_index = 0;
        DmList parent_queue = Dm.list();

        // We are essentially going to unroll apperance overlays into a flattened list here, so we can filter out floating planes laster
        // It will look like "overlay overlay overlay (change overlay parent), overlay overlay etc"
        // We can use this list to dynamically update these non floating planes, later
        while (queue_index < Dm.length(queue))
        {
            queue_index++;
            // If it's not a command, we assert that it's an appearance
            object appearance = queue[queue_index];
            if (appearance == null || Dm.eq(appearance, NEXT_PARENT_COMMAND)) // Who fucking adds nulls to their sublists god you people are the worst
                continue;

            mutable_appearance new_appearance = Dm.@new<mutable_appearance>();
            new_appearance.appearance = appearance;
            // Now check its children
            DmList children = (appearance as image)?.overlays;
            if (Dm.truthy(Dm.length(children)))
            {
                Dm.append(ref queue, NEXT_PARENT_COMMAND);
                Dm.append(ref parent_queue, appearance);
                foreach (object child_appearance in Dm.each(children))
                    Dm.append(ref queue, child_appearance);
            }
        }

        // Now we have a flattened list of parents and their children
        // Setup such that walking the list backwards will allow us to properly update overlays
        // (keeping in mind that overlays only update if an apperance is removed and added, and this pattern applies in a nested fashion)

        // If we found no results, return null
        if (!Dm.truthy(Dm.length(queue)))
            return null;

        // ALRIGHT MOTHERFUCKER
        // SO
        // DID YOU KNOW THAT OVERLAY RENDERING BEHAVIOR DEPENDS PARTIALLY ON THE ORDER IN WHICH OVERLAYS ARE ADDED?
        // WHAT WE'RE DOING HERE ENDS UP REVERSING THE OVERLAYS ADDITION ORDER (when it's walked back to front)
        // SO GUESS WHAT I'VE GOTTA DO, I'VE GOTTA SWAP ALLLL THE MEMBERS OF THE SUBLISTS
        // I HATE IT HERE
        float lower_parent = 0;
        float upper_parent = 0;
        float queue_size = Dm.length(queue);
        while (lower_parent <= queue_size)
        {
            // Let's reorder our "lists" (spaces between parent changes)
            // We've got a delta index, and we're gonna essentially use it to get "swap" positions from the top and bottom
            // We only need to loop over half the deltas to swap all the entries, any more and it'd be redundant
            // We floor so as to avoid over flipping, and ending up flipping "back" a delta
            // etc etc
            float target = FLOOR((upper_parent - lower_parent) / 2, 1);
            for (float delta_index = 1; delta_index <= target; delta_index++)
            {
                object old_lower = queue[lower_parent + delta_index];
                queue[lower_parent + delta_index] = queue[upper_parent - delta_index];
                queue[upper_parent - delta_index] = old_lower;
            }

            // lower bound moves to the old upper, upper bound finds a new home
            // Note that the end of the list is a valid upper bound
            lower_parent = upper_parent; // our old upper bound is now our lower bound
            while (upper_parent <= queue_size)
            {
                upper_parent += 1;
                if (Dm.length(queue) < upper_parent) // Parent found
                    break;
                if (Dm.eq(queue[upper_parent], NEXT_PARENT_COMMAND)) // We found em lads
                    break;
            }
        }

        // One more thing to do
        // It's much more convinient for the parent queue to be a list of indexes pointing at queue locations
        // Rather then a list of copied appearances
        // Let's turn what we have now into that yeah?
        // This'll require a loop over both queues
        // We're using an assoc list here rather then several find()s because I feel like that's more sane
        DmList apperance_to_position = Dm.list();
        for (float i = 1; i <= Dm.length(queue); i++)
            apperance_to_position[queue[i]] = i;

        DmList parent_indexes = Dm.list();
        foreach (object parent in Dm.each(parent_queue))
            Dm.append(ref parent_indexes, apperance_to_position[parent]);

        // Alright. We should now have two queues, a command/appearances one, and a parents queue, which contain no fluff
        // And when walked backwards allow for proper plane updating
        DmList return_pack = Dm.list(queue, parent_indexes);
        return return_pack;
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:548
    public static object get_default_icon_by_slot(object slot_flag = null)
    {
        object sw__1 = slot_flag;
        if (Dm.eq(sw__1, ITEM_SLOT_HEAD))
            return Dm.resource("icons/mob/clothing/head/default.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_EYES))
            return Dm.resource("icons/mob/clothing/eyes.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_EARS))
            return Dm.resource("icons/mob/clothing/ears.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_MASK))
            return Dm.resource("icons/mob/clothing/mask.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_NECK))
            return Dm.resource("icons/mob/clothing/neck.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_BACK))
            return Dm.resource("icons/mob/clothing/back.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_BELT))
            return Dm.resource("icons/mob/clothing/belt.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_ID))
            return Dm.resource("icons/mob/clothing/id.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_ICLOTHING))
            return Dm.resource("icons/mob/clothing/under/default.dmi"); // DEFAULT_UNIFORM_FILE (__DEFINES/species_clothing_paths.dm:3, a resource: inline)
        else if (Dm.eq(sw__1, ITEM_SLOT_OCLOTHING))
            return Dm.resource("icons/mob/clothing/suits/default.dmi"); // DEFAULT_SUIT_FILE (:7)
        else if (Dm.eq(sw__1, ITEM_SLOT_GLOVES))
            return Dm.resource("icons/mob/clothing/hands.dmi");
        else if (Dm.eq(sw__1, ITEM_SLOT_FEET))
            return Dm.resource("icons/mob/clothing/feet.dmi"); // DEFAULT_SHOES_FILE (:5)
        return null;
    }

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:575
    public static object get_default_layer_by_slot(object slot_flag = null)
    {
        float? sw__1 = text2num(slot_flag);
        if (Dm.eq(sw__1, ITEM_SLOT_HEAD))
            return HEAD_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_EYES))
            return GLASSES_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_EARS))
            return EARS_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_MASK))
            return FACEMASK_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_NECK))
            return NECK_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_BACK))
            return BACK_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_BELT))
            return BELT_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_ID))
            return ID_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_ICLOTHING))
            return UNIFORM_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_OCLOTHING))
            return SUIT_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_GLOVES))
            return GLOVES_LAYER;
        else if (Dm.eq(sw__1, ITEM_SLOT_FEET))
            return SHOES_LAYER;
        return null;
    }
}

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:172
public partial class atom : datum
{
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:172
    public virtual object update_appearance_planes(DmList appearances = null, object new_offset = null)
    {
        DmList build_list = build_planeed_apperance_queue(appearances) as DmList;

        if (!Dm.truthy(length(build_list)))
            return appearances;

        // hand_back contains a new copy of the passed in list, with updated values
        DmList hand_back = Dm.list();

        DmList processing_queue = build_list[1] as DmList;
        DmList parents_queue = build_list[2] as DmList;
        // Now that we have our queues, we're going to walk them forwards to remove, and backwards to add
        // Note, we need to do this separately because you can only remove a mutable appearance when it
        // Exactly matches the appearance it had when it was first "made static" (by being added to the overlays list)
        float parents_index = 0;
        foreach (object item in Dm.each(processing_queue))
        {
            if (Dm.eq(item, NEXT_PARENT_COMMAND))
            {
                parents_index++;
                continue;
            }
            image iter_apper = item as image;
            if (Dm.truthy(parents_index))
            {
                object parent_src_index = parents_queue[parents_index];
                image parent = processing_queue[parent_src_index] as image;
                Dm.remove(ref parent.overlays, iter_apper.appearance);
            }
            else // Otherwise, we're at the end of the list, and our parent is the mob
                cut_overlay(iter_apper);
        }

        // Now the back to front stuff, to readd the updated appearances
        float queue_index = Dm.length(processing_queue);
        parents_index = Dm.length(parents_queue);
        while (queue_index >= 1)
        {
            object item = processing_queue[queue_index];
            if (Dm.eq(item, NEXT_PARENT_COMMAND))
            {
                parents_index--;
                queue_index--;
                continue;
            }
            mutable_appearance new_iter = Dm.@new<mutable_appearance>();
            new_iter.appearance = item;
            if (new_iter.plane != FLOAT_PLANE)
            {
                // Here, finally, is where we actually update the plane offsets
                new_iter.plane = GET_NEW_PLANE(Dm.num(PLANE_TO_TRUE(new_iter.plane)), Dm.num(new_offset)); // SET_PLANE_W_SCALAR
            }
            if (Dm.truthy(parents_index))
            {
                object parent_src_index = parents_queue[parents_index];
                image parent = processing_queue[parent_src_index] as image;
                Dm.append(ref parent.overlays, new_iter.appearance);
            }
            else
            {
                add_overlay(new_iter);
                // chant a protective overlays.Copy to prevent appearance theft and overlay sticking
                // I'm not joking without this overlays can corrupt and be replaced by other appearances
                // the compiler might call it useless but I swear it works
                // we conjure the spirits of the computer with our spells, we conjur- (Hey lemon make a damn issue report already)
                DmList does_nothing = new_iter.overlays.Copy();
                Dm.append(ref hand_back, new_iter);
            }

            queue_index--;
        }
        return hand_back;
    }
}

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:354
public partial class obj_item : obj
{
    //mob HUD updates for items in our inventory

    /// Overlays for the worn overlay so you can overlay while you overlay
    /// eg: ammo counters, primed grenade flashing, etc.
    /// "icon_file" is used automatically for inhands etc. to make sure it gets the right inhand file
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:354
    [ShouldCallParent]
    public virtual object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = Dm.list();
        if (blocks_emissive != EMISSIVE_BLOCK_NONE)
        {
            // DEVIATION(DV12): emissive blockers are not created (lighting planes are not ported)
            Dm.Deferred("code/modules/mob/living/carbon/carbon_update_icons.dm:360", "worn_overlays: the emissive blocker of a worn item (DV12)");
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_GET_WORN_OVERLAYS]))
            this._SendSignal(COMSIG_ITEM_GET_WORN_OVERLAYS, Dm.list(this, dot, standing, isinhands, icon_file, bodyshape));
        return dot;
    }

    /// worn_overlays to use when you'd want to use KEEP_APART. Don't use KEEP_APART neither there nor here, as it would break floating overlays
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:364
    [ShouldCallParent]
    public virtual async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS]))
            this._SendSignal(COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, Dm.list(this, dot, standing, draw_target, isinhands, icon_file, bodyshape));
        return dot;
    }
}

/////////////////////////
// Limb Icon Cache 2.0 //
/////////////////////////

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:428
public partial class obj_item_bodypart : obj_item
{
    /// Returns a string representing the bodyparts icon cache key
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:428
    public virtual async DmTask<object> get_cache_key()
    {
        if (Dm.truthy(is_husked))
            return jointext(await generate_husk_key(), "-");
        return jointext(generate_icon_key(), "-");
    }

    /**
     * Called from update_body_parts() these procs handle the limb icon cache.
     * the limb icon cache adds an icon_render_key to a human mob, it represents:
     * - Gender, if applicable
     * - The ID of the limb
     * - Draw color, if applicable
     * These procs only store limbs as to increase the number of matching icon_render_keys
     * This cache exists because drawing 6/7 icons for humans constantly is quite a waste
     * See RemieRichards on irc.rizon.net #coderbus (RIP remie :sob:)
    **/
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:443
    public virtual object generate_icon_key()
    {
        DmList dot = null;
        dot = Dm.list();
        if (Dm.truthy(is_dimorphic))
            Dm.append(ref dot, limb_gender);
        Dm.append(ref dot, limb_id);
        Dm.append(ref dot, body_zone);
        if (Dm.truthy(should_draw_greyscale) && Dm.truthy(draw_color))
            Dm.append(ref dot, draw_color);
        if (Dm.truthy(is_invisible))
            Dm.append(ref dot, "invisible");
        foreach (datum_bodypart_overlay overlay in Dm.each<datum_bodypart_overlay>(bodypart_overlays))
        {
            if (Dm.truthy(overlay.can_draw_on_bodypart(this, owner)))
                Dm.append(ref dot, overlay.icon_render_key(this));
        }
        foreach (object texture in Dm.each(bodypart_textures))
        {
            // GAP(P2, datums/bodypart_overlays/texture_bodypart_overlay.cs): can_texture_bodypart and icon_render_key of
            // /datum/bodypart_texture (texture_bodypart_overlay.dm:24, :27) are not declared; no limb is textured in M2.
            Dm.NotTranslated("code/modules/mob/living/carbon/carbon_update_icons.dm:457", "generate_icon_key: the bodypart textures' render keys (M10+)");
        }
        // isdummy(owner) (__DEFINES/is_helpers.dm:127): no GlobalProcs member yet, expanded inline (rules 11.1);
        // GAP(lead, __DEFINES/is_helpers.cs): add isdummy.
        if (owner is mob_living_carbon_human_dummy) // dummies always cache as default height because they have optimizations
            Dm.append(ref dot, Dm.text("[]", Dm.initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.mob_height)));
        else if (ishuman(owner)) // otherwise cache height because we apply height filters to bodypart images
        {
            mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
            Dm.append(ref dot, Dm.text("[]", human_owner.mob_height));
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_GENERATE_ICON_KEY]))
            this._SendSignal(COMSIG_BODYPART_GENERATE_ICON_KEY, Dm.list(this, dot));
        return dot;
    }

    ///Generates a cache key specifically for husks
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:475
    public virtual async DmTask<object> generate_husk_key()
    {
        DmList dot = null;
        dot = Dm.list();
        if (Dm.truthy(is_dimorphic))
            Dm.append(ref dot, limb_gender);
        Dm.append(ref dot, limb_id);
        Dm.append(ref dot, husk_type);
        Dm.append(ref dot, is_husked == HUSKED_ZOMBIE ? "zombie_husk" : "burn_husk");
        Dm.append(ref dot, body_zone);
        if (Dm.truthy(is_invisible))
            Dm.append(ref dot, "invisible");
        foreach (datum_bodypart_overlay overlay in Dm.each<datum_bodypart_overlay>(bodypart_overlays))
        {
            if (Dm.truthy(overlay.can_draw_on_bodypart(this, owner)))
                Dm.append(ref dot, overlay.icon_render_key(this));
        }
        Dm.append(ref dot, Dm.text("[]", (Dm.truthy(LAZYLEN(blood_dna_info)) ? await get_color_from_blood_list(blood_dna_info) : BLOOD_COLOR_RED)));
        // isdummy(owner) (__DEFINES/is_helpers.dm:127): no GlobalProcs member yet, expanded inline (rules 11.1);
        // GAP(lead, __DEFINES/is_helpers.cs): add isdummy.
        if (owner is mob_living_carbon_human_dummy) // dummies always cache as default height because they have optimizations
            Dm.append(ref dot, Dm.text("[]", Dm.initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.mob_height)));
        else if (ishuman(owner)) // otherwise cache height because we apply height filters to bodypart images
        {
            mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
            Dm.append(ref dot, Dm.text("[]", human_owner.mob_height));
        }
        return dot;
    }

    /**
     * This proc serves as a way to ensure that legs layer properly on a mob.
     * To do this, two separate images are created - A low layer one, and a normal layer one.
     * Each of the image will appropriately crop out dirs that are not used on that given layer.
     *
     * Arguments:
     * * limb_overlay - The limb image being masked, not necessarily the original limb image as it could be an overlay on top of it
     * Returns the list of masked images, or `null` if the limb_overlay didn't exist
     */
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:508
    [DmProtected]
    public virtual object handle_masking(image limb_overlay = null)
    {
        return null;
    }
}

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:468
[DmPath("/obj/item/bodypart/head")]
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:468
    public override object generate_icon_key()
    {
        object dot = null;
        dot = base.generate_icon_key();
        if (Dm.truthy(lip_style))
        {
            Dm.append(ref dot, lip_color);
            Dm.append(ref dot, lip_style);
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:512
[DmPath("/obj/item/bodypart/leg")]
public partial class obj_item_bodypart_leg : obj_item_bodypart
{
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:512
    public override object handle_masking(image limb_overlay = null)
    {
        if (!Dm.truthy(limb_overlay))
            return null;
        DmList dot = Dm.list();

        // M6 slice 7b (D156, DV20 retired): the server keeps an /icon's Blend as operations the client draws (protocol.md 34.3)
        string icon_cache_key = Dm.text("[]-[]-[]", limb_overlay.icon, limb_overlay.icon_state, body_zone);
        icon new_leg_icon;
        icon new_leg_icon_lower;

        //in case we do not have a cached version of the two cropped icons for this key, we have to create it
        if (!Dm.truthy(GLOB.masked_leg_icons_cache[icon_cache_key]))
        {
            icon leg_crop_mask = Dm.eq(body_zone, BODY_ZONE_R_LEG) ? Dm.icon(Dm.resource("icons/mob/leg_masks.dmi"), "right_leg") : Dm.icon(Dm.resource("icons/mob/leg_masks.dmi"), "left_leg");
            icon leg_crop_mask_lower = Dm.eq(body_zone, BODY_ZONE_R_LEG) ? Dm.icon(Dm.resource("icons/mob/leg_masks.dmi"), "right_leg_lower") : Dm.icon(Dm.resource("icons/mob/leg_masks.dmi"), "left_leg_lower");

            new_leg_icon = Dm.icon(limb_overlay.icon, limb_overlay.icon_state);
            new_leg_icon.Blend(leg_crop_mask, Dm.ICON_MULTIPLY);

            new_leg_icon_lower = Dm.icon(limb_overlay.icon, limb_overlay.icon_state);
            new_leg_icon_lower.Blend(leg_crop_mask_lower, Dm.ICON_MULTIPLY);

            GLOB.masked_leg_icons_cache[icon_cache_key] = Dm.list(new_leg_icon, new_leg_icon_lower);
        }
        new_leg_icon = ((DmList)GLOB.masked_leg_icons_cache[icon_cache_key])[1] as icon;
        new_leg_icon_lower = ((DmList)GLOB.masked_leg_icons_cache[icon_cache_key])[2] as icon;

        //this could break layering in oddjob cases, but i'm sure it will work fine most of the time... right?
        // new(limb_overlay): BYOND's /image/New takes image()'s arguments, an image copying its appearance (the shim's image())
        image new_leg_appearance = Dm.image(limb_overlay);
        new_leg_appearance.icon = new_leg_icon;
        new_leg_appearance.layer = -BODYPARTS_LAYER;
        Dm.append(ref dot, new_leg_appearance);
        image new_leg_appearance_lower = Dm.image(limb_overlay);
        new_leg_appearance_lower.icon = new_leg_icon_lower;
        new_leg_appearance_lower.layer = -BODYPARTS_LOW_LAYER;
        Dm.append(ref dot, new_leg_appearance_lower);
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/carbon_update_icons.dm:497  GLOBAL_LIST_EMPTY(masked_leg_icons_cache), expanded inline (rules 10.6)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:497
    public DmList masked_leg_icons_cache;

    // tg: code/modules/mob/living/carbon/carbon_update_icons.dm:497
    public virtual void InitGlobalmasked_leg_icons_cache()
    {
        masked_leg_icons_cache = Dm.list();
        Dm.append(ref gvars_datum_init_order, "masked_leg_icons_cache");
    }
}
