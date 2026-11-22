// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/head_hair_and_lips.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P3): the head's lips, hair and eye overlays and the human's hair and lips setters. M10x slice D2: the hair,
// facial hair and gradient overlays from SSaccessories' accessories and the eyes' body overlay (the blended icons are recipes the
// client draws, icon.cs; DV12: no emissive blockers).

// tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:1
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:1
    public virtual void copy_appearance_from(mob_living_carbon_human target = null, float? overwrite_eyes = null)
    {
        overwrite_eyes ??= FALSE;
        datum_species target_species = (datum_species)target.dna.species;

        lip_style = target.lip_style;
        lip_color = target.lip_color;
        hairstyle = target.hairstyle;
        hair_alpha = target_species.hair_alpha;
        hair_color = target.hair_color;
        facial_hairstyle = target.facial_hairstyle;
        facial_hair_alpha = target_species.facial_hair_alpha;
        facial_hair_color = target.facial_hair_color;
        // fixed_hair_color = target_species.get_fixed_hair_color(target) //Can be null (:12)
        // GAP(P2, human/_species.cs): /datum/species/proc/get_fixed_hair_color (_species.dm:1989) is not declared; its body is
        // written here (an ASSERT and a switch on hair_color_mode) until it is, then this becomes the call.
        if (!(target != null)) throw Dm.CRASH(assertion_message("code/modules/mob/living/carbon/human/_species.dm", 1990, "!isnull(for_mob)"), "code/modules/mob/living/carbon/human/_species.dm", 1990);
        object sw__1 = target_species.hair_color_mode;
        if (Dm.eq(sw__1, USE_MUTANT_COLOR))
            fixed_hair_color = target.dna.features[FEATURE_MUTANT_COLOR];
        else if (Dm.eq(sw__1, USE_FIXED_MUTANT_COLOR))
            fixed_hair_color = target_species.fixed_mut_color;
        else
            fixed_hair_color = null;
        gradient_styles = LAZYCOPY(target.grad_style);
        gradient_colors = LAZYCOPY(target.grad_color);
        obj_item_organ_eyes peepers = Dm.locate<obj_item_organ_eyes>(this);
        if (Dm.truthy(peepers))
        {
            if (Dm.truthy(overwrite_eyes) || Dm.initial(peepers, (obj_item_organ_eyes p) => p.eye_color_left) == null)
                peepers.eye_color_left = target.eye_color_left;
            if (Dm.truthy(overwrite_eyes) || Dm.initial(peepers, (obj_item_organ_eyes p) => p.eye_color_right) == null)
                peepers.eye_color_right = target.eye_color_right;
        }

        if (HAS_TRAIT(target, TRAIT_USES_SKINTONES))
            skin_tone = target.skin_tone;
        else if (HAS_TRAIT(target, TRAIT_MUTANT_COLORS))
        {
            skin_tone = "";
            if (Dm.truthy(target_species.fixed_mut_color))
                species_color = target_species.fixed_mut_color;
            else
                species_color = (string)target.dna.features[FEATURE_MUTANT_COLOR];
        }
        else
        {
            skin_tone = "";
            species_color = "";
        }
    }

    /// Returns a list of all overlays associated with the lips
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:35
    public virtual object get_lips_overlays(object dropped = null)
    {
        DmList dot = null;
        dot = Dm.list();
        if (!Dm.truthy(lip_style) || Dm.truthy(is_husked) || Dm.truthy(is_invisible) || Dm.truthy(Dm.bitand(owner?.obscured_slots, HIDEFACIALHAIR)) || !Dm.truthy(Dm.bitand(head_flags, HEAD_LIPS)))
            return dot;

        // image('icons/mob/human/human_face.dmi', "lips_[lip_style]", -BODY_LAYER, dir = ...): icon_state and layer named
        // (rules 10.9: a text second argument)
        image lip_overlay = image(Dm.resource("icons/mob/human/human_face.dmi"), icon_state: Dm.text("lips_[]", lip_style), layer: -BODY_LAYER, dir: (Dm.truthy(dropped) ? SOUTH : null));
        lip_overlay.color = lip_color;
        worn_face_offset?.apply_offset(lip_overlay);
        Dm.append(ref dot, lip_overlay);
        return dot;
    }

    /// Returns a list of all hair/facial hair related overlays, or alternatively the debrained overlay if applicable
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:47
    public virtual object get_hair_overlays(object dropped = null)
    {
        DmList dot = null;
        dot = Dm.list();
        if (Dm.truthy(is_invisible))
            return dot;

        if (!Dm.truthy(is_husked) && !Dm.truthy(Dm.bitand(owner?.obscured_slots, HIDEFACIALHAIR)) && Dm.truthy(Dm.bitand(head_flags, HEAD_FACIAL_HAIR)))
            Dm.append(ref dot, get_base_facial_hair_overlays(dropped));

        if (is_husked != HUSKED_BURN && !Dm.truthy(Dm.bitand(owner?.obscured_slots, HIDEHAIR)))
        {
            obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(this);
            if (QDELETED(brain) && Dm.truthy(Dm.bitand(head_flags, HEAD_DEBRAIN)))
                Dm.append(ref dot, get_debrain_overlay(dropped));
            else if (!Dm.truthy(is_husked) && Dm.truthy(Dm.bitand(head_flags, HEAD_HAIR)))
                Dm.append(ref dot, get_base_hair_overlays(dropped));
        }

        return dot;
    }

    /// Used in constructing the hair overlays - handles just facial hair
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:65
    [DmPrivate]
    public virtual object get_base_facial_hair_overlays(object dropped = null)
    {
        DmList dot = null;
        dot = Dm.list();
        var sprite_accessory = SSaccessories.facial_hairstyles_list[facial_hairstyle] as datum_sprite_accessory_facial_hair;
        if (sprite_accessory == null || Dm.eq(sprite_accessory.icon_state, SPRITE_ACCESSORY_NONE))
            return dot;

        object image_dir = Dm.truthy(dropped) ? SOUTH : null;

        // Overlay
        image facial_hair_overlay = image(sprite_accessory.icon, icon_state: sprite_accessory.icon_state, layer: -HAIR_LAYER, dir: image_dir);
        facial_hair_overlay.alpha = facial_hair_alpha;
        set_overlay_hair_color(facial_hair_overlay, facial_hair_color);
        // DEVIATION(DV12): no emissive blocker (:79-84).

        //Offsets
        worn_face_offset?.apply_offset(facial_hair_overlay);
        Dm.append(ref dot, facial_hair_overlay);

        //Gradients
        object facial_hair_gradient_style = get_hair_gradient_style(GRADIENT_FACIAL_HAIR_KEY);
        if (Dm.eq(facial_hair_gradient_style, SPRITE_ACCESSORY_NONE))
            return dot;

        object facial_hair_gradient_color = get_hair_gradient_color(GRADIENT_FACIAL_HAIR_KEY);
        var facial_hair_gradient_overlay = (image)get_gradient_overlay(Dm.icon(sprite_accessory.icon, sprite_accessory.icon_state), -HAIR_LAYER, SSaccessories.facial_hair_gradients_list[facial_hair_gradient_style] as datum_sprite_accessory, facial_hair_gradient_color, dropped);
        if (facial_hair_alpha == 255)
        {
            Dm.append(ref dot, facial_hair_gradient_overlay);
            return dot;
        }

        // If we have a gradient and hair alpha, we need to merge them into a single appearance by having a shared KEEP_TOGETHER holder
        // since adding our gradient as an overlay would cause it to get colored
        Dm.remove(ref dot, facial_hair_overlay);
        facial_hair_overlay.alpha = 255;
        image shared_holder = image(layer: -HAIR_LAYER, dir: image_dir);
        shared_holder.alpha = facial_hair_alpha;
        shared_holder.appearance_flags = Dm.bitor(shared_holder.appearance_flags, KEEP_TOGETHER);
        Dm.append(shared_holder.overlays, facial_hair_overlay);
        Dm.append(shared_holder.overlays, facial_hair_gradient_overlay);
        Dm.append(ref dot, shared_holder);
        return dot;
    }

    /// Used in constructing the hair overlays - handles just the hair on top of the head
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:115
    [DmPrivate]
    public virtual object get_base_hair_overlays(object dropped = null)
    {
        DmList dot = null;
        dot = Dm.list();
        var hair_sprite_accessory = SSaccessories.hairstyles_list[hairstyle] as datum_sprite_accessory_hair;
        if (hair_sprite_accessory == null || Dm.eq(hair_sprite_accessory.icon_state, SPRITE_ACCESSORY_NONE))
            return dot;

        object image_dir = Dm.truthy(dropped) ? SOUTH : null;

        DmList all_hair_overlays = Dm.list();
        // Hair masks
        icon base_icon = Dm.icon(hair_sprite_accessory.getCachedIcon(owner?.hair_masks));
        // Overlay
        Dm.append(all_hair_overlays, image(base_icon, layer: -HAIR_LAYER, dir: image_dir));
        // If we have any hair appendages (ponytails, etc.) sticking out on a particular side,
        // we need to add an additional hair layer to go above hats/helmets for the sides they stick out on
        if (LAZYLEN(hair_sprite_accessory.hair_appendages_outer) != 0)
        {
            object strictly_masked_zones = NONE;
            foreach (object mask in Dm.each(owner?.hair_masks))
                strictly_masked_zones = Dm.bitor(strictly_masked_zones, (mask as datum_hair_mask ?? (datum_hair_mask)SSaccessories.hair_masks_list[mask]).strict_coverage_zones); // PORT: a type path (hair.cs)
            foreach (object appendage_icon_state in Dm.each(hair_sprite_accessory.hair_appendages_outer))
            {
                object appendage_zone = hair_sprite_accessory.hair_appendages_outer[appendage_icon_state];
                if (!Dm.truthy(Dm.bitand(appendage_zone, strictly_masked_zones))) // if there are no strict masks in this zone
                    Dm.append(all_hair_overlays, image(hair_sprite_accessory.icon, icon_state: appendage_icon_state, layer: -OUTER_HAIR_LAYER, dir: image_dir));
            }
        }

        foreach (object each in Dm.each(all_hair_overlays))
        {
            var hair_overlay = (image)each;
            set_overlay_hair_color(hair_overlay, hair_color);
            hair_overlay.alpha = hair_alpha;
            hair_overlay.pixel_z = hair_sprite_accessory.y_offset;
            // DEVIATION(DV12): no emissive blocker (:142-148).
            // Offsets
            worn_face_offset?.apply_offset(hair_overlay);
            Dm.append(ref dot, hair_overlay);
            // Gradients
            object hair_gradient_style = get_hair_gradient_style(GRADIENT_HAIR_KEY);
            if (Dm.eq(hair_gradient_style, SPRITE_ACCESSORY_NONE))
                continue;

            object hair_gradient_color = get_hair_gradient_color(GRADIENT_HAIR_KEY);
            var hair_gradient_overlay = (image)get_gradient_overlay(base_icon, hair_overlay.layer, SSaccessories.hair_gradients_list[hair_gradient_style] as datum_sprite_accessory, hair_gradient_color, dropped);
            hair_gradient_overlay.pixel_z = hair_sprite_accessory.y_offset;
            if (hair_alpha == 255)
            {
                Dm.append(ref dot, hair_gradient_overlay);
                continue;
            }

            Dm.remove(ref dot, hair_overlay);
            hair_overlay.alpha = 255;
            image shared_holder = image(layer: -HAIR_LAYER, dir: image_dir);
            shared_holder.alpha = facial_hair_alpha;
            shared_holder.appearance_flags = Dm.bitor(shared_holder.appearance_flags, KEEP_TOGETHER);
            Dm.append(shared_holder.overlays, hair_overlay);
            Dm.append(shared_holder.overlays, hair_gradient_overlay);
            Dm.append(ref dot, shared_holder);
        }
        return dot;
    }

    /// Helper for setting hair color of an overlay appropriately
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:178
    [DmPrivate]
    public virtual void set_overlay_hair_color(image hair_overlay = null, object hair_color_to_use = null)
    {
        hair_color_to_use ??= this.hair_color;
        if (Dm.truthy(override_hair_color))
            hair_overlay.color = override_hair_color;
        else if (Dm.truthy(fixed_hair_color))
            hair_overlay.color = fixed_hair_color;
        else
            hair_overlay.color = hair_color_to_use;
    }

    /// Returns a list of all eye related overlays, or an eyeless overlay if applicable
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:188
    public virtual object get_eye_overlays(object dropped = null)
    {
        DmList dot = null;
        dot = Dm.list();

        if (is_husked == HUSKED_BURN)
            return dot;

        obj_item_organ_eyes eyes = Dm.locate<obj_item_organ_eyes>(this);
        if (QDELETED(eyes))
        {
            if (Dm.truthy(Dm.bitand(head_flags, HEAD_EYEHOLES)))
                Dm.append(ref dot, get_eyeless_overlay(dropped));
        }
        else
        {
            if (Dm.truthy(Dm.bitand(head_flags, HEAD_EYESPRITES)))
            {
                Dm.append(ref dot, eyes.generate_body_overlay(this));
            }
        }

        return dot;
    }

    /// Returns an appropriate debrained overlay
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:206
    public virtual object get_debrain_overlay(object dropped = null)
    {
        object debrain_icon = Dm.resource("icons/mob/human/human_face.dmi");
        string debrain_icon_state = "debrained";
        if (Dm.truthy(Dm.bitand(bodytype, BODYTYPE_ALIEN)))
        {
            debrain_icon = Dm.resource("icons/mob/human/species/alien/bodyparts.dmi");
            debrain_icon_state = "debrained_alien";
        }
        else if (Dm.truthy(Dm.bitand(bodytype, BODYTYPE_LARVA_PLACEHOLDER)))
        {
            debrain_icon = Dm.resource("icons/mob/human/species/alien/bodyparts.dmi");
            debrain_icon_state = "debrained_larva";
        }
        else if (Dm.truthy(Dm.bitand(bodytype, BODYTYPE_GOLEM)))
        {
            debrain_icon = Dm.resource("icons/mob/human/species/golems.dmi");
            debrain_icon_state = "debrained";
        }

        image debrain_overlay = mutable_appearance(debrain_icon, debrain_icon_state, -HAIR_LAYER) as image;
        if (Dm.truthy(dropped))
            debrain_overlay = image(debrain_overlay, dir: SOUTH);
        worn_face_offset?.apply_offset(debrain_overlay);
        return debrain_overlay;
    }

    /// Returns an appropriate missing eyes overlay
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:227
    public virtual object get_eyeless_overlay(object dropped = null)
    {
        object eyeless_icon = Dm.resource("icons/mob/human/human_eyes.dmi");
        string eyeless_icon_state = "eyes_missing";

        image eyeless_overlay = mutable_appearance(eyeless_icon, eyeless_icon_state, -HAIR_LAYER) as image;
        if (Dm.truthy(dropped))
            eyeless_overlay = image(eyeless_overlay, dir: SOUTH);
        worn_face_offset?.apply_offset(eyeless_overlay);
        return eyeless_overlay;
    }

    /// Returns an appropriate hair/facial hair gradient overlay
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:239
    public virtual object get_gradient_overlay(icon base_icon = null, object layer = null, datum_sprite_accessory gradient = null, object grad_color = null, object dropped = null)
    {
        var gradient_overlay = (mutable_appearance)mutable_appearance(layer: Dm.num(layer));
        icon temp = Dm.icon(gradient.icon, gradient.icon_state);
        icon temp_hair = Dm.icon(base_icon);
        temp.Blend(temp_hair, ICON_ADD);
        gradient_overlay.icon = temp;
        gradient_overlay.color = grad_color;
        image result = gradient_overlay;
        if (Dm.truthy(dropped))
            result = image(gradient_overlay, dir: SOUTH);
        worn_face_offset?.apply_offset(result);
        return result;
    }

    /**
     * Get the hair gradient style of a head.
     * Defaults to "None".
     * arguments:
     * * key (optional) - corresponds to hair or facial hair index. If no key is provided returns whole list.
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:349
    public virtual object get_hair_gradient_style(object key = null)
    {
        if (Dm.truthy(key))
            return (LAZYACCESS(gradient_styles, key) is var or__1 && Dm.truthy(or__1) ? or__1 : "None");

        return (gradient_styles is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.list(
            "None", //Hair Gradient Style
            "None" //Facial Hair Gradient Style
        ));
    }

    /**
     * Get the hair gradient color of a head.
     * Defaults to black.
     *
     * arguments:
     * * key (optional) - corresponds to hair or facial hair index. If no key is provided returns whole list.
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:403
    public virtual object get_hair_gradient_color(object key = null)
    {
        if (Dm.truthy(key))
            return (LAZYACCESS(gradient_colors, key) is var or__1 && Dm.truthy(or__1) ? or__1 : COLOR_BLACK);

        return (gradient_colors is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.list(
            COLOR_BLACK, //Hair Gradient Color
            COLOR_BLACK //Facial Hair Gradient Color
        ));
    }
}

/**
 * Used to update the makeup on a human and apply/remove lipstick traits, then store/unstore them on the head object in case it gets severed
 **/
// tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:256
public partial class mob_living : mob
{
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:256
    public virtual async DmTask update_lips(object new_style = null, object new_color = null, object apply_trait = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * A wrapper for [mob/living/carbon/human/proc/update_lips] that sets the lip style and color to null.
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:279
    public virtual async DmTask<object> clean_lips()
    {
        return null;
    }

    /**
     * Set the hair style of a human.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:292
    public virtual void set_hairstyle(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Set the hair color of a human.
     * Override instead sets the override value, it will not be changed away from the override value until override is set to null.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:309
    public virtual void set_haircolor(object hex_string = null, object @override = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Get the hair gradient style of a human.
     * Defaults to "None".
     * arguments:
     * * key (optional) - corresponds to hair or facial hair index. If no key is provided returns whole list.
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:331
    public virtual object get_hair_gradient_style(object key = null)
    {
        return null;
    }

    /**
     * Set the hair gradient style of a human.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:362
    public virtual void set_hair_gradient_style(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Get the hair gradient color of a human.
     * Defaults to black.
     *
     * arguments:
     * * key (optional) - corresponds to hair or facial hair index. If no key is provided returns whole list.
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:384
    public virtual object get_hair_gradient_color(object key = null)
    {
        return null;
    }

    /**
     * Set the hair gradient color of a human.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:416
    public virtual void set_hair_gradient_color(object new_color = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Set the facial hair style of a human.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:435
    public virtual void set_facial_hairstyle(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Set the facial hair color of a human.
     * Override instead sets the override value, it will not be changed away from the override value until override is set to null.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:452
    public virtual void set_facial_haircolor(object hex_string = null, object @override = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Set the facial hair gradient style of a human.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:472
    public virtual void set_facial_hair_gradient_style(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }

    /**
     * Set the facial hair gradient color of a human.
     * Update calls update_body_parts().
     **/
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:491
    public virtual void set_facial_hair_gradient_color(object new_color = null, float? update = null)
    {
        update ??= TRUE;
        return;
    }
}

// tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:259
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:259
    public override async DmTask update_lips(object new_style = null, object new_color = null, object apply_trait = null, float? update = null)
    {
        update ??= TRUE;
        lip_style = new_style;
        lip_color = (string)new_color;

        obj_item_bodypart_head hopefully_a_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        REMOVE_TRAITS_IN(this, LIPSTICK_TRAIT);
        if (Dm.truthy(hopefully_a_head))
        {
            hopefully_a_head.stored_lipstick_trait = null;
            hopefully_a_head.lip_style = new_style;
            hopefully_a_head.lip_color = new_color;
        }
        if (Dm.truthy(new_style) && Dm.truthy(apply_trait))
        {
            ADD_TRAIT(this, apply_trait, LIPSTICK_TRAIT);
            if (hopefully_a_head != null)
                hopefully_a_head.stored_lipstick_trait = apply_trait;
        }

        if (Dm.truthy(update))
            await update_body(); // lips is done as a body layer
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:282
    public override async DmTask<object> clean_lips()
    {
        if (!Dm.truthy(lip_style))
            return FALSE;
        await update_lips(null, null, update: TRUE);
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:295
    public override void set_hairstyle(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        hairstyle = (string)new_style;
        if (my_head != null)
            my_head.hairstyle = (string)new_style;

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:312
    public override void set_haircolor(object hex_string = null, object @override = null, float? update = null)
    {
        update ??= TRUE;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        if (Dm.truthy(@override))
        {
            // aight, no head? tough luck
            if (my_head != null)
                my_head.override_hair_color = hex_string;
        }
        else
        {
            hair_color = (string)hex_string;
            if (my_head != null)
                my_head.hair_color = (string)hex_string;
        }

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:334
    public override object get_hair_gradient_style(object key = null)
    {
        if (Dm.truthy(key))
            return (LAZYACCESS(grad_style, key) is var or__1 && Dm.truthy(or__1) ? or__1 : "None");

        return (grad_style is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.list(
            "None", //Hair Gradient Style
            "None" //Facial Hair Gradient Style
        ));
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:365
    public override void set_hair_gradient_style(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        if (Dm.eq(LAZYACCESS(grad_style, GRADIENT_HAIR_KEY), new_style))
            return;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        LAZYSET(ref grad_style, GRADIENT_HAIR_KEY, new_style);
        if (Dm.truthy(my_head))
            LAZYSET(ref my_head.gradient_styles, GRADIENT_HAIR_KEY, new_style);

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:387
    public override object get_hair_gradient_color(object key = null)
    {
        if (Dm.truthy(key))
            return (LAZYACCESS(grad_color, key) is var or__1 && Dm.truthy(or__1) ? or__1 : COLOR_BLACK);

        return (grad_color is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.list(
            COLOR_BLACK, //Hair Gradient Color
            COLOR_BLACK //Facial Hair Gradient Color
        ));
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:419
    public override void set_hair_gradient_color(object new_color = null, float? update = null)
    {
        update ??= TRUE;
        if (Dm.eq(LAZYACCESS(grad_color, GRADIENT_HAIR_KEY), new_color))
            return;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        LAZYSET(ref grad_color, GRADIENT_HAIR_KEY, new_color);
        if (Dm.truthy(my_head))
            LAZYSET(ref my_head.gradient_colors, GRADIENT_HAIR_KEY, new_color);

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:438
    public override void set_facial_hairstyle(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        facial_hairstyle = (string)new_style;
        if (my_head != null)
            my_head.facial_hairstyle = (string)new_style;

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:455
    public override void set_facial_haircolor(object hex_string = null, object @override = null, float? update = null)
    {
        update ??= TRUE;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        if (Dm.truthy(@override))
        {
            // so no head? tough luck
            if (my_head != null)
                my_head.override_hair_color = hex_string;
        }
        else
        {
            facial_hair_color = (string)hex_string;
            if (my_head != null)
                my_head.facial_hair_color = (string)hex_string;
        }

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:475
    public override void set_facial_hair_gradient_style(object new_style = null, float? update = null)
    {
        update ??= TRUE;
        if (Dm.eq(LAZYACCESS(grad_style, GRADIENT_FACIAL_HAIR_KEY), new_style))
            return;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        LAZYSET(ref grad_style, GRADIENT_FACIAL_HAIR_KEY, new_style);
        if (Dm.truthy(my_head))
            LAZYSET(ref my_head.gradient_styles, GRADIENT_FACIAL_HAIR_KEY, new_style);

        if (Dm.truthy(update))
            update_hair();
    }

    // tg: code/modules/surgery/bodyparts/head_hair_and_lips.dm:494
    public override void set_facial_hair_gradient_color(object new_color = null, float? update = null)
    {
        update ??= TRUE;
        if (Dm.eq(LAZYACCESS(grad_color, GRADIENT_FACIAL_HAIR_KEY), new_color))
            return;
        obj_item_bodypart_head my_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        LAZYSET(ref grad_color, GRADIENT_FACIAL_HAIR_KEY, new_color);
        if (Dm.truthy(my_head))
            LAZYSET(ref my_head.gradient_colors, GRADIENT_FACIAL_HAIR_KEY, new_color);

        if (Dm.truthy(update))
            update_hair();
    }
}
