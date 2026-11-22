// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P3, scope 2j A.2; M2 F1 the uniform branch of update_worn_undersuit): the human's worn-item layers
// (in M2 only the jumpsuit is worn; every other layer only removes and re-applies itself), build_worn_icon, the body layer (underwear), eyes and hair, the height handling. What needs server-side
// icon rendering (female and bodyshape variants, icon dimensions, height filters) or types M2 does not declare (clothing,
// sprite accessories) is NotTranslated or Deferred at the statement, with its milestone.
//
// Not translated here: /obj/item/clothing/get_general_color (:507) and /obj/item/clothing/shoes/get_general_color (:529)
// (the clothing types are not declared; clothing, M4a). The HUD offsets and cached size (:943-954) are at the end (M8 slice F).

// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:50
public partial class mob_living_carbon_human : mob_living_carbon
{
    // RESOLVE_ICON_STATE(worn_item) (human_update_icons.dm:1, file-local, #undef at :940) reads its argument twice; it is
    // expanded inline at its one use (rules 11.1, 11.4).

    /* --------------------------------------- */
    //For legacy support.
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:50
    public override async DmTask<object> regenerate_icons()
    {
        object dot = null;
        dot = await base.regenerate_icons();
        await update_worn_undersuit();
        await update_worn_id();
        await update_worn_glasses();
        await update_worn_gloves();
        await update_worn_ears();
        await update_worn_shoes();
        await update_suit_storage();
        await update_worn_mask();
        await update_worn_head();
        await update_worn_belt();
        await update_worn_back();
        await update_worn_oversuit();
        update_pockets();
        await update_worn_neck();
        await update_transform();
        await update_damage_overlays();
        // These are done via parent call update_body(), keeping them here for clarity
        // update_hair()
        // update_eyes()
        return dot;
    }

    /* --------------------------------------- */
    //vvvvvv UPDATE_INV PROCS vvvvvv

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:75
    public override async DmTask update_worn_undersuit()
    {
        remove_overlay(UNIFORM_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_ICLOTHING);

        if (w_uniform is obj_item_clothing_under)
        {
            obj_item_clothing_under uniform = w_uniform as obj_item_clothing_under;
            if (HAS_TRAIT(uniform, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEJUMPSUIT)))
                return;

            object target_overlay = uniform.icon_state;
            if (uniform.adjusted == ALT_STYLE)
                target_overlay = Dm.text("[]_d", target_overlay);


            //This is how non-humanoid clothing works. You check if the mob has the right bodyflag, and the clothing has the corresponding clothing flag.
            //handled_by_bodyshape is used to track whether or not we successfully used an alternate sprite. It's set to TRUE to ease up on copy-paste.
            //icon_file MUST be set to null by default, or it causes issues.
            //handled_by_bodyshape MUST be set to FALSE under the if(!icon_exists()) statement, or everything breaks.
            //"override_file = handled_by_bodyshape ? icon_file : null" MUST be added to the arguments of build_worn_icon()
            //Friendly reminder that icon_exists_or_scream(file, state) is your friend when debugging this code.
            float handled_by_bodyshape = TRUE;
            float digi = Dm.bitand(bodyshape, BODYSHAPE_DIGITIGRADE);
            object icon_file = null;
            object woman = null;
            float female_sprite_flags = uniform.female_sprite_flags;
            //BEGIN SPECIES HANDLING
            if (Dm.truthy(digi) && Dm.truthy(Dm.bitand(uniform.supports_variations_flags, CLOTHING_DIGITIGRADE_VARIATION)))
                icon_file = Dm.resource("icons/mob/human/species/misc/digitigrade.dmi"); // DIGITIGRADE_UNIFORM_FILE (__DEFINES/species_clothing_paths.dm:11)
            //Female sprites have lower priority than digitigrade sprites
            object chest_is_dimorphic = null;
            if (Dm.truthy(((datum_species)dna.species).sexes))
            {
                obj_item_bodypart_chest our_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
                chest_is_dimorphic = our_chest.is_dimorphic;
            }
            if (Dm.truthy(chest_is_dimorphic) && Dm.truthy(Dm.bitand(bodyshape, BODYSHAPE_HUMANOID)) && physique == FEMALE && !Dm.truthy(Dm.bitand(female_sprite_flags, NO_FEMALE_UNIFORM))) //Agggggggghhhhh
                woman = TRUE;

            // RESOLVE_ICON_STATE(uniform) (:1, file-local), expanded inline: (worn_item.worn_icon_state || worn_item.icon_state)
            if (!Dm.truthy(icon_exists(icon_file, (uniform.worn_icon_state is var or__1 && Dm.truthy(or__1) ? or__1 : uniform.icon_state))))
            {
                icon_file = Dm.resource("icons/mob/clothing/under/default.dmi"); // DEFAULT_UNIFORM_FILE (__DEFINES/species_clothing_paths.dm:3)
                handled_by_bodyshape = FALSE;
            }

            //END SPECIES HANDLING
            mutable_appearance uniform_overlay = await uniform.build_worn_icon(
                default_layer: UNIFORM_LAYER,
                default_icon_file: icon_file,
                isinhands: FALSE,
                female_uniform: Dm.truthy(woman) ? female_sprite_flags : null,
                override_state: target_overlay,
                override_file: Dm.truthy(handled_by_bodyshape) ? icon_file : null,
                bodyshape: bodyshape
            ) as mutable_appearance;

            apply_height(uniform_overlay, ENTIRE_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_uniform_offset?.apply_offset(uniform_overlay);
            overlays_standing[UNIFORM_LAYER] = uniform_overlay;
        }

        apply_overlay(UNIFORM_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:133
    public override async DmTask update_worn_id()
    {
        remove_overlay(ID_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_ID);

        if (Dm.truthy(wear_id))
        {
            obj_item worn_item = wear_id;

            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/id.dmi");
            mutable_appearance id_overlay = await wear_id.build_worn_icon(default_layer: ID_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;

            apply_height(id_overlay, UPPER_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_id_offset?.apply_offset(id_overlay);
            overlays_standing[ID_LAYER] = id_overlay;
        }

        apply_overlay(ID_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:154
    public override async DmTask update_worn_gloves()
    {
        remove_overlay(GLOVES_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_GLOVES);

        //Bloody hands begin
        if (gloves == null)
        {
            if (!Dm.truthy(blood_in_hands) || !Dm.truthy(num_hands) || !Dm.truthy(GET_ATOM_BLOOD_DECAL_LENGTH(this)))
                return;
            // When byond gives us filters that respect dirs we can just use an alpha mask for this but until then, two icons weeeee
            mutable_appearance hands_combined = mutable_appearance(layer: -GLOVES_LAYER, appearance_flags: KEEP_TOGETHER) as mutable_appearance;
            if (Dm.truthy(has_left_hand(check_disabled: FALSE)))
            {
                mutable_appearance blood_overlay = mutable_appearance(Dm.resource("icons/effects/blood.dmi"), "bloodyhands_left") as mutable_appearance;
                blood_overlay.color = get_blood_dna_color();
                Dm.append(ref hands_combined.overlays, blood_overlay);
            }
            if (Dm.truthy(has_right_hand(check_disabled: FALSE)))
            {
                mutable_appearance blood_overlay = mutable_appearance(Dm.resource("icons/effects/blood.dmi"), "bloodyhands_right") as mutable_appearance;
                blood_overlay.color = get_blood_dna_color();
                Dm.append(ref hands_combined.overlays, blood_overlay);
            }

            apply_height(hands_combined, LOWER_BODY);
            overlays_standing[GLOVES_LAYER] = hands_combined;
            apply_overlay(GLOVES_LAYER);
            return;
        }
        // Bloody hands end

        obj_item worn_item = gloves;
        if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEGLOVES)))
            return;

        object icon_file = Dm.resource("icons/mob/clothing/hands.dmi");

        mutable_appearance gloves_overlay = await gloves.build_worn_icon(default_layer: GLOVES_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;

        float feature_y_offset = 0;
        //needs to be typed, hand_bodyparts can have nulls
        foreach (obj_item_bodypart_arm my_hand in Dm.each<obj_item_bodypart_arm>(hand_bodyparts))
        {
            feature_y_offset = Math.Max(Dm.num((my_hand.worn_glove_offset?.get_offset() as DmList)?["y"]), feature_y_offset);
        }

        gloves_overlay.pixel_z += feature_y_offset;
        apply_height(gloves_overlay, LOWER_BODY);

        // We dont have any >2 hands human species (and likely wont ever), so theres no point in splitting this because:
        // It will only run if the left hand OR the right hand is missing, and it wont run if both are missing because you cant wear gloves with no arms
        // (unless admins mess with this then its their fault)
        if (num_hands < default_num_hands)
        {
            // M6 slice 7 (D155): the filter and its render target reach the client (protocol version 25).
            if (update_worn_gloves__alpha_filter_target == null)
                update_worn_gloves__alpha_filter_target = Dm.@new<atom_movable>(null);
            atom_movable alpha_filter_target = update_worn_gloves__alpha_filter_target;
            alpha_filter_target.icon = Dm.resource("icons/effects/effects.dmi");
            alpha_filter_target.icon_state = Dm.text("missing[]", !Dm.truthy(has_left_hand(check_disabled: FALSE)) ? "l" : "r");
            alpha_filter_target.render_target = Dm.text("*MissGlove [] []", REF(this), !Dm.truthy(has_left_hand(check_disabled: FALSE)) ? "L" : "R");
            // gloves_overlay.add_overlay(alpha_filter_target): /image/proc/add_overlay(x) is `overlays |= x` (overlays.dm:113), as
            // update_wound_overlays writes it (carbon_update_icons.cs).
            Dm.combine(ref gloves_overlay.overlays, alpha_filter_target);
            // gloves_overlay.filters += filter(...): the image's filters list is reassigned with the new filter (dm_filter.cs).
            DmList gloves_filters = gloves_overlay.filters as DmList ?? Dm.list();
            Dm.append(ref gloves_filters, Dm.filter(Dm.named("type", "alpha"), Dm.named("render_source", alpha_filter_target.render_target), Dm.named("y", feature_y_offset), Dm.named("flags", MASK_INVERSE)));
            gloves_overlay.filters = gloves_filters;
        }

        overlays_standing[GLOVES_LAYER] = gloves_overlay;
        apply_overlay(GLOVES_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:199  var/static/atom/movable/alpha_filter_target (rules 10.6: a TgState field)
    private static ref atom_movable update_worn_gloves__alpha_filter_target => ref TgState.Current.mob_living_carbon_human__update_worn_gloves__alpha_filter_target;

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:211
    public override async DmTask update_worn_glasses()
    {
        remove_overlay(GLASSES_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_EYES);

        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (my_head == null) //decapitated
            return;

        if (Dm.truthy(glasses))
        {
            obj_item worn_item = glasses;
            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEEYES)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/eyes.dmi");

            mutable_appearance glasses_overlay = await glasses.build_worn_icon(default_layer: GLASSES_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(glasses_overlay, UPPER_BODY);
            my_head.worn_glasses_offset?.apply_offset(glasses_overlay);
            overlays_standing[GLASSES_LAYER] = glasses_overlay;
        }
        apply_overlay(GLASSES_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:233
    public override async DmTask update_worn_ears()
    {
        remove_overlay(EARS_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_EARS);

        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (my_head == null) //decapitated
            return;

        if (Dm.truthy(ears))
        {
            obj_item worn_item = ears;
            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEEARS)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/ears.dmi");

            mutable_appearance ears_overlay = await ears.build_worn_icon(default_layer: EARS_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(ears_overlay, UPPER_BODY);
            my_head.worn_ears_offset?.apply_offset(ears_overlay);
            overlays_standing[EARS_LAYER] = ears_overlay;
        }
        apply_overlay(EARS_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:254
    public override async DmTask update_worn_neck()
    {
        remove_overlay(NECK_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_NECK);

        if (Dm.truthy(wear_neck))
        {
            obj_item worn_item = wear_neck;

            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDENECK)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/neck.dmi");

            mutable_appearance neck_overlay = await worn_item.build_worn_icon(default_layer: NECK_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(neck_overlay, ENTIRE_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_neck_offset?.apply_offset(neck_overlay);
            overlays_standing[NECK_LAYER] = neck_overlay;
        }

        apply_overlay(NECK_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:274
    public override async DmTask update_worn_shoes()
    {
        remove_overlay(SHOES_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_FEET);

        if (num_legs < 2)
            return;

        if (Dm.truthy(shoes))
        {
            obj_item worn_item = shoes;

            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDESHOES)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/feet.dmi"); // DEFAULT_SHOES_FILE (__DEFINES/species_clothing_paths.dm:5, a resource: inline)

            mutable_appearance shoes_overlay = await shoes.build_worn_icon(default_layer: SHOES_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            float feature_y_offset = 0;
            // GAP(lead): GLOB.leg_zones (_globalvars/lists/flavor_misc.dm) is not declared; its list, inline.
            foreach (object body_zone in Dm.each(Dm.list(BODY_ZONE_R_LEG, BODY_ZONE_L_LEG)))
            {
                obj_item_bodypart_leg my_leg = get_bodypart(body_zone as string) as obj_item_bodypart_leg;
                feature_y_offset = Math.Max(Dm.num((my_leg?.worn_foot_offset?.get_offset() as DmList)?["y"]), feature_y_offset);
            }

            shoes_overlay.pixel_z += feature_y_offset;
            // apply_height(shoes_overlay, NO_MODIFY) // Shoes are rooted
            overlays_standing[SHOES_LAYER] = shoes_overlay;
        }

        apply_overlay(SHOES_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:301
    public override async DmTask update_suit_storage()
    {
        remove_overlay(SUIT_STORE_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_SUITSTORE);

        if (Dm.truthy(s_store))
        {
            obj_item worn_item = s_store;
            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDESUITSTORAGE)))
                return;


            mutable_appearance s_store_overlay = await worn_item.build_worn_icon(default_layer: SUIT_STORE_LAYER, default_icon_file: Dm.resource("icons/mob/clothing/belt_mirror.dmi"), bodyshape: bodyshape) as mutable_appearance;
            apply_height(s_store_overlay, ENTIRE_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_suit_storage_offset?.apply_offset(s_store_overlay);
            overlays_standing[SUIT_STORE_LAYER] = s_store_overlay;
        }
        apply_overlay(SUIT_STORE_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:318
    public override async DmTask update_worn_head()
    {
        remove_overlay(HEAD_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_HEAD);

        if (Dm.truthy(head))
        {
            obj_item worn_item = head;
            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEHEADGEAR)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/head/default.dmi");

            mutable_appearance head_overlay = await head.build_worn_icon(default_layer: HEAD_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(head_overlay, UPPER_BODY);
            obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            my_head?.worn_head_offset?.apply_offset(head_overlay);
            overlays_standing[HEAD_LAYER] = head_overlay;
        }

        apply_overlay(HEAD_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:337
    public override async DmTask update_worn_belt()
    {
        remove_overlay(BELT_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_BELT);

        if (Dm.truthy(belt))
        {
            obj_item worn_item = belt;
            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEBELT)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/belt.dmi");

            mutable_appearance belt_overlay = await belt.build_worn_icon(default_layer: BELT_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(belt_overlay, ENTIRE_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_belt_offset?.apply_offset(belt_overlay);
            overlays_standing[BELT_LAYER] = belt_overlay;
        }

        apply_overlay(BELT_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:356
    public override async DmTask update_worn_oversuit()
    {
        remove_overlay(SUIT_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_OCLOTHING);

        if (Dm.truthy(wear_suit))
        {
            obj_item worn_item = wear_suit;

            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/suits/default.dmi"); // DEFAULT_SUIT_FILE (__DEFINES/species_clothing_paths.dm:7)

            mutable_appearance suit_overlay = await wear_suit.build_worn_icon(default_layer: SUIT_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(suit_overlay, ENTIRE_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_suit_offset?.apply_offset(suit_overlay);
            overlays_standing[SUIT_LAYER] = suit_overlay;
        }

        apply_overlay(SUIT_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:376
    public override void update_pockets()
    {
        if (Dm.truthy(hud_used))
        {
            hud_used.update_inventory_slot(ITEM_SLOT_LPOCKET);
            hud_used.update_inventory_slot(ITEM_SLOT_RPOCKET);
        }
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:381
    public override async DmTask update_worn_mask()
    {
        remove_overlay(FACEMASK_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_MASK);

        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (my_head == null) //Decapitated
            return;

        if (Dm.truthy(wear_mask))
        {
            obj_item worn_item = wear_mask;
            if (HAS_TRAIT(worn_item, TRAIT_NO_WORN_ICON) || Dm.truthy(Dm.bitand(obscured_slots, HIDEMASK)))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/mask.dmi");

            mutable_appearance mask_overlay = await wear_mask.build_worn_icon(default_layer: FACEMASK_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(mask_overlay, UPPER_BODY);
            my_head.worn_mask_offset?.apply_offset(mask_overlay);
            overlays_standing[FACEMASK_LAYER] = mask_overlay;
        }

        apply_overlay(FACEMASK_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:403
    public override async DmTask update_worn_back()
    {
        remove_overlay(BACK_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_BACK);

        if (Dm.truthy(back))
        {
            if (HAS_TRAIT(back, TRAIT_NO_WORN_ICON))
                return;

            object icon_file = Dm.resource("icons/mob/clothing/back.dmi");

            mutable_appearance back_overlay = await back.build_worn_icon(default_layer: BACK_LAYER, default_icon_file: icon_file, bodyshape: bodyshape) as mutable_appearance;
            apply_height(back_overlay, ENTIRE_BODY);
            obj_item_bodypart_chest my_chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
            my_chest?.worn_back_offset?.apply_offset(back_overlay);
            overlays_standing[BACK_LAYER] = back_overlay;
        }
        apply_overlay(BACK_LAYER);
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:420
    public override async DmTask<object> get_held_overlays()
    {
        hud_used?.update_inventory_slot(ITEM_SLOT_HANDS);
        DmList hands = Dm.list();
        foreach (obj_item worn_item in Dm.each<obj_item>(held_items))
        {
            object held_index = get_held_index_of_item(worn_item);
            object t_state = worn_item.inhand_icon_state;
            if (!Dm.truthy(t_state))
                t_state = worn_item.icon_state;


            object icon_file = IS_RIGHT_INDEX(Dm.num(held_index)) ? worn_item.righthand_file : worn_item.lefthand_file;
            mutable_appearance hand_overlay = await worn_item.build_worn_icon(default_layer: HANDS_LAYER, default_icon_file: icon_file, isinhands: TRUE, bodyshape: bodyshape) as mutable_appearance;
            apply_height(hand_overlay, LOWER_BODY);
            obj_item_bodypart_arm held_in_hand = hand_bodyparts[held_index] as obj_item_bodypart_arm;
            held_in_hand?.held_hand_offset?.apply_offset(hand_overlay);

            Dm.append(ref hands, hand_overlay);
        }
        return hands;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:536
    public virtual object get_overlays_copy(DmList unwantedLayers = null)
    {
        DmList @out = Dm.list();
        for (float i = 1, i__end = TOTAL_LAYERS; i <= i__end; i++)
        {
            if (Dm.truthy(overlays_standing[i]))
            {
                if (Dm.@in(i, unwantedLayers))
                    continue;
                Dm.append(ref @out, overlays_standing[i]);
            }
        }
        return @out;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:663
    [DmParams("is_creating")]
    public override async DmTask update_body(params object[] __extra)
    {
        float? is_creating = Dm.arg<float?>(__extra, 0);
        is_creating ??= FALSE;
        remove_overlay(BODY_LAYER);
        DmList clothing_overlays = get_underwear_overlays() as DmList;
        if (Dm.truthy(length(clothing_overlays)))
        {
            foreach (image overlay in Dm.each_cast<image>(clothing_overlays))
                apply_height(overlay, ENTIRE_BODY);
            overlays_standing[BODY_LAYER] = clothing_overlays;
            apply_overlay(BODY_LAYER);
        }
        // parent call will update the actual bodyparts
        await base.update_body(Dm.forward(__extra, is_creating));
        return;
    }

    /// Returns a list of all underclothing overlays to be applied to the mob
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:675
    public virtual object get_underwear_overlays()
    {
        DmList dot = null;
        dot = Dm.list();
        if (HAS_TRAIT(this, TRAIT_HUSK) || HAS_TRAIT(this, TRAIT_INVISIBLE_MAN) || HAS_TRAIT(this, TRAIT_NO_UNDERWEAR))
            return dot;

        // Underwear, Undershirts & Socks
        if (Dm.truthy(underwear))
        {
            var undie_accessory = SSaccessories.underwear_list[underwear] as datum_sprite_accessory_clothing_underwear;
            var underwear_overlay = undie_accessory?.make_appearance(underwear_color as string, physique, bodyshape) as mutable_appearance;
            if (underwear_overlay != null)
                Dm.append(dot, underwear_overlay);
        }

        if (Dm.truthy(undershirt))
        {
            var shirt_accessory = SSaccessories.undershirt_list[undershirt] as datum_sprite_accessory_clothing_undershirt;
            var shirt_overlay = shirt_accessory?.make_appearance(null, physique, bodyshape) as mutable_appearance;
            if (shirt_overlay != null)
                Dm.append(dot, shirt_overlay);
        }

        if (Dm.truthy(socks) && num_legs >= 2 && !Dm.truthy(Dm.bitand(bodyshape, BODYSHAPE_DIGITIGRADE)))
        {
            var sock_accessory = SSaccessories.socks_list[socks] as datum_sprite_accessory_clothing_socks;
            var socks_overlay = sock_accessory?.make_appearance(null, physique, bodyshape) as mutable_appearance;
            if (socks_overlay != null)
                Dm.append(dot, socks_overlay);
        }

        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:705
    public override async DmTask update_eyes(float? refresh = null)
    {
        refresh ??= TRUE;
        remove_overlay(EYES_LAYER);

        obj_item_bodypart_head noggin = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (noggin == null || Dm.truthy(noggin.is_invisible))
            return;

        if (Dm.truthy(refresh))
        {
            obj_item_organ_eyes eyes = Dm.locate<obj_item_organ_eyes>(noggin);
            if (eyes != null)
                await eyes.refresh(this, call_update: FALSE);
        }

        DmList eye_overlays = noggin.get_eye_overlays() as DmList;
        if (Dm.truthy(length(eye_overlays)))
        {
            foreach (image overlay in Dm.each_cast<image>(eye_overlays))
                apply_height(overlay, UPPER_BODY);
            overlays_standing[EYES_LAYER] = eye_overlays;
            apply_overlay(EYES_LAYER);
        }
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:727
    public override void update_hair()
    {
        remove_overlay(HAIR_LAYER);

        obj_item_bodypart_head head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        DmList head_overlays = head?.get_hair_overlays() as DmList;
        if (Dm.truthy(length(head_overlays)))
        {
            foreach (image overlay in Dm.each_cast<image>(head_overlays))
                apply_height(overlay, UPPER_BODY);
            overlays_standing[HAIR_LAYER] = head_overlays;
            apply_overlay(HAIR_LAYER);
        }
    }

    /// Updates face (as of now, only eye) offsets
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:739
    public override void update_face_offset()
    {
        DmList eye_overlays = overlays_standing[EYES_LAYER] as DmList;

        if (HAS_TRAIT(this, TRAIT_INVISIBLE_MAN) || HAS_TRAIT(this, TRAIT_HUSK) || !Dm.truthy(length(eye_overlays)))
            return;

        remove_overlay(EYES_LAYER);

        obj_item_bodypart_head noggin = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        foreach (mutable_appearance overlay in Dm.each_cast<mutable_appearance>(eye_overlays))
        {
            overlay.pixel_w = 0;
            overlay.pixel_z = 0;
            apply_height(overlay, UPPER_BODY);
            noggin.worn_face_offset?.apply_offset(overlay); // tg reads it unguarded: a runtime in tg on a head without one
        }

        overlays_standing[EYES_LAYER] = eye_overlays;
        apply_overlay(EYES_LAYER);
    }


    /// Makes all aspects of the mob invisibile but the head and its associated sprites
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:759
    public virtual async DmTask render_only_head()
    {
        if (!Dm.truthy(dna?.species))
            return;

        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        if (!(my_head is obj_item_bodypart_head))
            return;

        cut_overlays();
        await my_head.update_limb();
        add_overlay(await my_head.get_limb_icon());
        await update_worn_head();
        await update_worn_mask();
        await update_eyes();
        update_hair();
    }

    /**
     * Applies an offset or a filter to an appearance accordance to the height of our mob
     *
     * * appearance - The appearance to apply the height changes to
     * * body_area - The body area this appearance is on, used to determine what offsets/filters to apply
     */
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:785
    public override void apply_height(image appearance = null, object body_area = null)
    {
        if (mob_height == HUMAN_HEIGHT_MEDIUM)
            return; // default, no handling needed

        object sw__1 = body_area;
        if (Dm.eq(sw__1, LOWER_BODY) || Dm.eq(sw__1, UPPER_BODY))
        {
            appearance.pixel_z += Dm.num((GLOB.human_heights_to_offsets[mob_height] as DmList)[body_area]);
            return;
        }
        else if (Dm.eq(sw__1, ENTIRE_BODY))
        {
            apply_height_filter(appearance);
            return;
        }
        else if (Dm.eq(sw__1, NO_MODIFY))
            return;

        stack_trace(Dm.text("Invalid height body_area specified ([])", (body_area is var or__1 && Dm.truthy(or__1) ? or__1 : "null")), "code/modules/mob/living/carbon/human/human_update_icons.dm", 799);
    }

    /**
     * Applies a filter to an appearance according to mob height
     */
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:804
    [DmPrivate]
    // M6 slice 7b (D156): the displacement filters reach the client (protocol version 25)
    public virtual object apply_height_filter(image appearance = null)
    {
        cut_torso_mask ??= Dm.icon("icons/effects/cut.dmi", "Cut1");
        cut_legs_mask ??= Dm.icon("icons/effects/cut.dmi", "Cut2");
        lenghten_torso_mask ??= Dm.icon("icons/effects/cut.dmi", "Cut3");
        lenghten_legs_mask ??= Dm.icon("icons/effects/cut.dmi", "Cut4");
        lenghten_arms_mask ??= Dm.icon("icons/effects/cut.dmi", "Cut5");

        appearance.remove_filter(Dm.list(
            "Cut_Torso",
            "Cut_Legs",
            "Lenghten_Legs",
            "Lenghten_Torso",
            "Lenghten_Arms",
            "Gnome_Cut_Torso",
            "Gnome_Cut_Legs",
            "Monkey_Torso",
            "Monkey_Legs",
            "Monkey_Gnome_Cut_Torso",
            "Monkey_Gnome_Cut_Legs"));

        object sw__1 = mob_height;
        // Don't set this one directly, use TRAIT_DWARF
        if (Dm.eq(sw__1, MONKEY_HEIGHT_DWARF))
            appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Monkey_Gnome_Cut_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_torso_mask, x: 0, y: 0, size: 3))),
                Dm.list(Dm.kv("name", "Monkey_Gnome_Cut_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_legs_mask, x: 0, y: 0, size: 4)))));
        else if (Dm.eq(sw__1, MONKEY_HEIGHT_MEDIUM))
            appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Monkey_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_torso_mask, x: 0, y: 0, size: 2))),
                Dm.list(Dm.kv("name", "Monkey_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_legs_mask, x: 0, y: 0, size: 4)))));
        else if (Dm.eq(sw__1, HUMAN_HEIGHT_DWARF)) // tall monkeys and dwarves use the same value
        {
            if (ismonkey(this))
                appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Monkey_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_torso_mask, x: 0, y: 0, size: 1))),
                Dm.list(Dm.kv("name", "Monkey_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_legs_mask, x: 0, y: 0, size: 1)))));
            else
                appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Gnome_Cut_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_torso_mask, x: 0, y: 0, size: 2))),
                Dm.list(Dm.kv("name", "Gnome_Cut_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_legs_mask, x: 0, y: 0, size: 3)))));
        }
        // Don't set this one directly, use TRAIT_DWARF
        else if (Dm.eq(sw__1, HUMAN_HEIGHT_SHORTEST))
            appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Cut_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_torso_mask, x: 0, y: 0, size: 1))),
                Dm.list(Dm.kv("name", "Cut_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(cut_legs_mask, x: 0, y: 0, size: 1)))));
        else if (Dm.eq(sw__1, HUMAN_HEIGHT_SHORT))
            appearance.add_filter("Cut_Legs", 1, displacement_map_filter(cut_legs_mask, x: 0, y: 0, size: 1) as DmList);
        else if (Dm.eq(sw__1, HUMAN_HEIGHT_TALL))
            appearance.add_filter("Lenghten_Legs", 1, displacement_map_filter(lenghten_legs_mask, x: 0, y: 0, size: 1) as DmList);
        else if (Dm.eq(sw__1, HUMAN_HEIGHT_TALLER))
            appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Lenghten_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(lenghten_torso_mask, x: 0, y: 0, size: 1))),
                Dm.list(Dm.kv("name", "Lenghten_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(lenghten_legs_mask, x: 0, y: 0, size: 1)))));
        else if (Dm.eq(sw__1, HUMAN_HEIGHT_TALLEST))
        {
            appearance.add_filters(Dm.list(
                Dm.list(Dm.kv("name", "Lenghten_Torso"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(lenghten_torso_mask, x: 0, y: 0, size: 1))),
                Dm.list(Dm.kv("name", "Lenghten_Arms"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(lenghten_arms_mask, x: 0, y: 0, size: 1))),
                Dm.list(Dm.kv("name", "Lenghten_Legs"), Dm.kv("priority", 1), Dm.kv("params", displacement_map_filter(lenghten_legs_mask, x: 0, y: 0, size: 1)))));
        }

        // Kinda gross but because many humans overlays do not use KEEP_TOGETHER we need to manually propogate the filter
        // Otherwise overlays, such as worn overlays on icons, won't have the filter "applied", and the effect kinda breaks
        if (!Dm.truthy(Dm.bitand(appearance.appearance_flags, KEEP_TOGETHER)))
        {
            foreach (mutable_appearance child_overlay in Dm.each_cast<mutable_appearance>(Dm.add(appearance.underlays, appearance.overlays)))
                apply_height_filter(child_overlay);
        }

        return appearance;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:807-811  var/static/icon/cut_torso_mask ... lenghten_arms_mask
    private static ref icon cut_torso_mask => ref TgState.Current.mob_living_carbon_human__apply_height_filter__cut_torso_mask;
    private static ref icon cut_legs_mask => ref TgState.Current.mob_living_carbon_human__apply_height_filter__cut_legs_mask;
    private static ref icon lenghten_torso_mask => ref TgState.Current.mob_living_carbon_human__apply_height_filter__lenghten_torso_mask;
    private static ref icon lenghten_legs_mask => ref TgState.Current.mob_living_carbon_human__apply_height_filter__lenghten_legs_mask;
    private static ref icon lenghten_arms_mask => ref TgState.Current.mob_living_carbon_human__apply_height_filter__lenghten_arms_mask;

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:959
    public override async DmTask<object> update_body_parts(object update_limb_data = null)
    {
        object dot = null;
        dot = await base.update_body_parts(update_limb_data);
        if (!Dm.truthy(dot))
            return dot;
        cached_body_width = ICON_SIZE_X;
        cached_body_height = ICON_SIZE_Y;
        DmList bodypart_overlays = overlays_standing[BODYPARTS_LAYER] as DmList;
        if (!Dm.truthy(length(bodypart_overlays)))
            return dot;
        DmList parsed_overlays = bodypart_overlays.Copy();
        float i = 1;
        while (i <= length(parsed_overlays))
        {
            mutable_appearance overlay = parsed_overlays[i] as mutable_appearance;
            if (!(overlay is image)) // Malformed overlays, etc
            {
                i += 1;
                continue;
            }
            float overlay_x = overlay.pixel_x + overlay.pixel_w;
            float overlay_y = overlay.pixel_y + overlay.pixel_z;
            if (islist(parsed_overlays[overlay])) // Nested overlay
            {
                overlay_x += Dm.num(((DmList)parsed_overlays[overlay])[SUB_OVERLAY_X_INDEX]);
                overlay_y += Dm.num(((DmList)parsed_overlays[overlay])[SUB_OVERLAY_Y_INDEX]);
            }
            // cached_body_width = max(cached_body_width, overlay.get_cached_width()) and the height (:980-981):
            // /image/proc/get_cached_width reads get_icon_dimensions (game/data_huds.dm:502), the .dmi's size, which the
            // server does not load (D104: the client reads tg's icon files); the cached size serves runechat and the atom
            // HUDs (M8-M9), and every M2 human sprite is 32x32, the value set above.
            Dm.Deferred("code/modules/mob/living/carbon/human/human_update_icons.dm:980", "update_body_parts: the overlays' icon dimensions (get_icon_dimensions needs .dmi metadata; runechat and atom HUD heights, M10+)");
            cached_body_min_x_offset = min(cached_body_min_x_offset, overlay_x);
            cached_body_min_y_offset = min(cached_body_min_y_offset, overlay_y);
            foreach (object sub_overlay in Dm.each(overlay.overlays))
                parsed_overlays[sub_overlay] = Dm.list(overlay_x, overlay_y);
            i += 1;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:956  #define SUB_OVERLAY_X_INDEX 1 (file-local, #undef at :988)
    private const int SUB_OVERLAY_X_INDEX = 1;
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:957  #define SUB_OVERLAY_Y_INDEX 1 (file-local, #undef at :989)
    private const int SUB_OVERLAY_Y_INDEX = 1;
}

/// Modifies a sprite slightly to conform to female body shapes
// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:440 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:440
    public static object wear_female_version(object icon_state = null, object icon_file_path = null, object icon = null, object type = null, object greyscale_colors = null, object bodyshape = null)
    {
        // DEVIATION(DV-F1a): the female uniform is cut from the uniform icon with icon(), Blend(ICON_MULTIPLY) by a
        // masking_helpers.dmi crop and fcopy_rsc (:441-452), a server-side icon; the server renders no icons (D104, as for
        // the legs' masks, DV20). It returns no icon, so build_worn_icon draws the uniform's own worn icon uncut
        // (human_update_icons.dm:603-605): a female human's jumpsuit keeps the male cut. The client would apply the crop.
        Dm.Deferred("code/modules/mob/living/carbon/human/human_update_icons.dm:441", "wear_female_version: the female cut of a uniform (a server-side icon Blend; DV-F1a, the client composes icons)");
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:455
public partial class obj_item : obj
{
    /// Modifies a sprite to conform to custom body shapes
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:455
    public virtual object get_bodyshape_icon(icon base_icon = null, object key = null, object greyscale_colors = null, object bodyshape = null)
    {
        if (!(istext(key))) throw Dm.CRASH(assertion_message("code/modules/mob/living/carbon/human/human_update_icons.dm", 456, "istext(key)", "get_bodyshape_icon: no key passed"), "code/modules/mob/living/carbon/human/human_update_icons.dm", 456);
        if (Dm.truthy(Dm.bitand(bodyshape, BODYSHAPE_CERULEAN)))
        {
            if (Dm.truthy(Dm.bitand(bodyshapes_with_variations, BODYSHAPE_CERULEAN)) || Dm.truthy(Dm.bitand(supports_variations_flags, CERULEAN_MASKING)))
            {
                // return generate_cerulean_icons(base_icon, key, greyscale_colors, bodyshape) (cerulean_update_icons.dm): the cerulean's tail
                // clothing waits for the cerulean species (gaps SP2)
                Dm.NotTranslated("code/modules/mob/living/carbon/human/human_update_icons.dm:459", "get_bodyshape_icon: generate_cerulean_icons, the cerulean's clothing (gaps SP2)");
                return null;
            }
        }
        // Gaps SP1: the digitigrade clothing is the icon recipe the client composes (D104): tg's mask and GAGS legs as /icon Blend operations
        if (Dm.truthy(Dm.bitand(bodyshape, BODYSHAPE_DIGITIGRADE)) && Dm.truthy(Dm.bitand(supports_variations_flags, CLOTHING_DIGITIGRADE_MASK)))
        {
            if (greyscale_colors == null || Dm.num(length(SSgreyscale.ParseColorString(greyscale_colors))) > 1)
                greyscale_colors = get_general_color(base_icon);

            object index = Dm.text("[]-[]-[]", key, type, greyscale_colors);
            DmList digitigrade_clothing_cache = get_bodyshape_icon__digitigrade_clothing_cache;
            icon resulting_icon = digitigrade_clothing_cache[index] as icon;
            if (resulting_icon == null)
            {
                resulting_icon = generate_digitigrade_icons(base_icon, greyscale_colors) as icon;
                if (resulting_icon == null)
                {
                    stack_trace(Dm.text("[] is set to generate a masked digitigrade icon, but generate_digitigrade_icons was not implemented (or error'd).", type), "code/modules/mob/living/carbon/human/human_update_icons.dm", 470);
                    return base_icon;
                }
                digitigrade_clothing_cache[index] = fcopy_rsc(resulting_icon);
            }

            return Dm.icon(resulting_icon);
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:463  var/static/list/digitigrade_clothing_cache (a proc-level static, rules 10.6)
    private static ref DmList get_bodyshape_icon__digitigrade_clothing_cache => ref TgState.Current.obj_item__get_bodyshape_icon__digitigrade_clothing_cache;

    /**
     * Generates a digitigrade version of this item's worn icon
     *
     * Arguments:
     * * base_icon: The icon to generate the digitigrade icon from
     * * greyscale_colors: The greyscale colors to use for the digitigrade icon
     *
     * Returns an icon that is the digitigrade version of the item's worn icon
     * Returns null if the item has no support for digitigrade variations via this method
     */
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:486
    public virtual object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        return null;
    }

    /**
     * Get what color the item is on "average"
     * Can be used to approximate what color this item is/should be
     *
     * Arguments:
     * * base_icon: The icon to get the color from
     */
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:496
    public virtual object get_general_color(icon base_icon = null)
    {
        if (Dm.truthy(greyscale_colors) && Dm.num(length(SSgreyscale.ParseColorString(greyscale_colors))) == 1)
            return greyscale_colors;
        return color;
    }

    /*
    Does everything in relation to building the /mutable_appearance used in the mob's overlays list
    covers:
    Inhands and any other form of worn item
    Rentering large appearances
    Layering appearances on custom layers
    Building appearances from custom icon files

    By Remie Richards (yes I'm taking credit because this just removed 90% of the copypaste in update_icons())

    state: A string to use as the state, this is FAR too complex to solve in this proc thanks to shitty old code
    so it's specified as an argument instead.

    default_layer: The layer to draw this on if no other layer is specified

    default_icon_file: The icon file to draw states from if no other icon file is specified

    isinhands: If true then alternate_worn_icon is skipped so that default_icon_file is used,
    in this situation default_icon_file is expected to match either the lefthand_ or righthand_ file var

    female_uniform: A value matching a uniform item's female_sprite_flags var, if this is anything but NO_FEMALE_UNIFORM, we
    generate/load female uniform sprites matching all previously decided variables


    */
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:570
    public virtual async DmTask<object> build_worn_icon(float? default_layer = null, object default_icon_file = null, float? isinhands = null, float? female_uniform = null, object override_state = null, object override_file = null, float? bodyshape = null)
    {
        default_layer ??= 0;
        isinhands ??= FALSE;
        female_uniform ??= NO_FEMALE_UNIFORM;
        bodyshape ??= NONE;

        //Find a valid icon_state from variables+arguments
        object t_state = (override_state is var or__1 && Dm.truthy(or__1) ? or__1 : ((Dm.truthy(isinhands) ? inhand_icon_state : worn_icon_state) is var or__2 && Dm.truthy(or__2) ? or__2 : icon_state));
        //Find a valid icon file from variables+arguments
        object file2use = (override_file is var or__3 && Dm.truthy(or__3) ? or__3 : ((Dm.truthy(isinhands) ? null : worn_icon) is var or__4 && Dm.truthy(or__4) ? or__4 : default_icon_file));
        //Find a valid layer from variables+arguments
        object layer2use = (alternate_worn_layer is var or__5 && Dm.truthy(or__5) ? or__5 : default_layer);

        mutable_appearance draw_target = null; // MA of the item itself, not the final result
        icon building_icon = null; // used to construct an icon across multiple procs before converting it to MA
        if (Dm.truthy(female_uniform))
            building_icon = wear_female_version(icon_state: t_state, icon_file_path: file2use, icon: file2use, type: female_uniform, greyscale_colors: greyscale_colors, bodyshape: bodyshape) as icon;
        if (!Dm.truthy(isinhands) && Dm.truthy(Dm.bitand(bodyshapes_with_variations, bodyshape)))
        {
            building_icon = get_bodyshape_icon(base_icon: building_icon ?? Dm.icon(file2use, t_state), key: Dm.text("[]-[]-[]", t_state, file2use, female_uniform), greyscale_colors: greyscale_colors, bodyshape: bodyshape) as icon;
        }
        if (Dm.truthy(building_icon))
            draw_target = mutable_appearance(building_icon, layer: -Dm.num(layer2use)) as mutable_appearance;
        else
            draw_target = mutable_appearance(file2use, (string)t_state, layer: -Dm.num(layer2use)) as mutable_appearance;

        //Get the overlays for this item when it's being worn
        //eg: ammo counters, primed grenade flashes, etc.
        DmList worn_overlays__1 = worn_overlays(draw_target, isinhands, file2use, bodyshape) as DmList;
        if (Dm.truthy(length(worn_overlays__1)))
            Dm.append(ref draw_target.overlays, worn_overlays__1);
        draw_target = color_atom_overlay(draw_target) as mutable_appearance;

        // Okay so this has to be done because some overlays, like blood, want to be KEEP_APART
        // but KEEP_APART breaks float layering, so what we need to do is make fake KEEP_APART for us to use
        mutable_appearance standing = mutable_appearance(layer: -Dm.num(layer2use), appearance_flags: KEEP_TOGETHER) as mutable_appearance;
        Dm.append(ref standing.overlays, draw_target);
        DmList separate_overlays = await separate_worn_overlays(standing, draw_target, isinhands, file2use, bodyshape) as DmList;
        if (Dm.truthy(length(separate_overlays)))
            Dm.append(ref standing.overlays, separate_overlays);
        else // Don't nest overlays if there's nothing to nest against
        {
            Dm.remove(ref standing.overlays, draw_target);
            standing = draw_target;
        }

        standing = center_image(standing, Dm.truthy(isinhands) ? inhand_x_dimension : worn_x_dimension, Dm.truthy(isinhands) ? inhand_y_dimension : worn_y_dimension) as mutable_appearance;

        //Worn offsets
        DmList offsets = get_worn_offsets(isinhands) as DmList;
        standing.pixel_w += Dm.num(offsets[1]);
        standing.pixel_z += Dm.num(offsets[2]);

        standing.alpha = alpha;

        return standing;
    }

    /// Returns offsets used for equipped item overlays in list(px_offset,py_offset) form.
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:640
    public virtual object get_worn_offsets(object isinhands = null)
    {
        DmList dot = null;
        dot = Dm.list(0, 0); //(px,py)
        if (Dm.truthy(isinhands))
        {
            //Handle held offsets
            mob holder = loc as mob;
            if (holder is mob)
            {
                DmList offsets = holder.get_item_offsets_for_index(holder.get_held_index_of_item(this)) as DmList;
                if (Dm.truthy(offsets))
                {
                    dot[1] = offsets["x"];
                    dot[2] = offsets["y"];
                }
            }
        }
        else
            dot[2] = worn_y_offset;
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:654
public partial class mob : atom_movable
{
    //Can't think of a better way to do this, sadly
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:654
    public virtual object get_item_offsets_for_index(object i = null)
    {
        object sw__1 = i;
        if (Dm.eq(sw__1, 3)) //odd = left hands
            return Dm.list(Dm.kv("x", 0), Dm.kv("y", 16));
        else if (Dm.eq(sw__1, 4)) //even = right hands
            return Dm.list(Dm.kv("x", 0), Dm.kv("y", 16));
        else //No offsets or Unwritten number of hands
            return Dm.list(Dm.kv("x", 0), Dm.kv("y", 0)); //Handle held offsets
    }
}

// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:702
public partial class mob_living : mob
{
    /// Updates eye sprites if relevant
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:702
    public virtual async DmTask update_eyes(float? refresh = null)
    {
        refresh ??= TRUE;
        return;
    }

    /// Updates hair sprites if relevant
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:724
    public virtual void update_hair()
    {
        return;
    }
}

// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:782
public partial class mob_living_carbon : mob_living
{
    /**
     * Applies an offset or a filter to an appearance accordance to the height of our mob
     *
     * * appearance - The appearance to apply the height changes to
     * * body_area - The body area this appearance is on, used to determine what offsets/filters to apply
     */
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:782
    public virtual void apply_height(image appearance = null, object body_area = null)
    {
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:199  /mob/living/carbon/human/update_worn_gloves var/static/atom/movable/alpha_filter_target
    internal atom_movable mob_living_carbon_human__update_worn_gloves__alpha_filter_target;
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:807-811  /mob/living/carbon/human/apply_height_filter var/static/icon/...
    internal icon mob_living_carbon_human__apply_height_filter__cut_torso_mask;
    internal icon mob_living_carbon_human__apply_height_filter__cut_legs_mask;
    internal icon mob_living_carbon_human__apply_height_filter__lenghten_torso_mask;
    internal icon mob_living_carbon_human__apply_height_filter__lenghten_legs_mask;
    internal icon mob_living_carbon_human__apply_height_filter__lenghten_arms_mask;
}

public partial class mob_living_carbon_human : mob_living_carbon
{
    // Wide organs or bodyparts shouldn't offset human HUD directly
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:943
    public override object get_hud_x_offset()
    {
        return 0;
    }

    // But they are affected by height
    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:947
    public override object get_hud_y_offset()
    {
        return ((DmList)GLOB.human_heights_to_offsets[mob_height])[Dm.text("[]", UPPER_BODY)];
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:950
    public override object get_cached_width()
    {
        return cached_body_width;
    }

    // tg: code/modules/mob/living/carbon/human/human_update_icons.dm:953
    public override object get_cached_height()
    {
        return cached_body_height;
    }
}

// tg: code/modules/mob/living/carbon/human/human_update_icons.dm:463 (gaps SP1: the digitigrade clothing's cache)
public sealed partial class TgState
{
    internal DmList obj_item__get_bodyshape_icon__digitigrade_clothing_cache = Dm.list();
}
