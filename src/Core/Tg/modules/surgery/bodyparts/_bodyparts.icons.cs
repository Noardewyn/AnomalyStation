// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/_bodyparts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the appearance procs of _bodyparts.dm (the limb icon, its damage overlays, colours, textures and bodypart
// overlays: get_bodypart_damage_state, update_limb, update_draw_color, get_limb_icon, ...), split off by the M2 contracts
// step so that M2 P3 (appearance) and M2 P2 (_bodyparts.cs: the var block, damage, bleeding, ownership) each own one file
// of the same tg file (docs/ai/specs/m2-ownership.md). The procs keep tg's order; the rest of the file is _bodyparts.cs.
// Translated by M2 P3. Left to _bodyparts.cs (P2): update_bodypart_damage_state (:1139), wash (:1284) and
// update_atom_colour (:1307), the overrides between them. M6 slice 6: get_bodypart_damage_state's gauze overlay (:1181).

// tg: code/modules/surgery/bodyparts/_bodyparts.dm:1151
public partial class obj_item_bodypart : obj_item
{
    /// Gets overlays to apply to the mob when damaged.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1151
    public virtual async DmTask<object> get_bodypart_damage_state()
    {
        if (!Dm.truthy(dmg_overlay_type))
            return null;

        DmList overlays = null;
        if (Dm.truthy(brutestate))
        {
            mutable_appearance blood_overlay = mutable_appearance(
                icon: Dm.resource("icons/mob/effects/dam_mob.dmi"),
                icon_state: Dm.text("[]_[]_[]0", dmg_overlay_type, body_zone, brutestate),
                layer: -DAMAGE_LAYER
            ) as mutable_appearance;
            datum_blood_type bloodtype__1 = (datum_blood_type)owner?.get_bloodtype();
            object damage_color = bloodtype__1 == null ? null : await bloodtype__1.get_damage_color(owner);
            blood_overlay.color = (damage_color is var or__1 && Dm.truthy(or__1) ? or__1 : BLOOD_COLOR_RED); // this should probably take from stored blood dna
            LAZYADD(ref overlays, blood_overlay);

            mutable_appearance brute_damage_overlay = mutable_appearance(
                icon: Dm.resource("icons/mob/effects/dam_mob.dmi"),
                icon_state: Dm.text("[]_[]_[]0_overlay", dmg_overlay_type, body_zone, brutestate),
                layer: -DAMAGE_OVERLAY_LAYER,
                appearance_flags: RESET_COLOR
            ) as mutable_appearance;
            LAZYADD(ref overlays, brute_damage_overlay);
        }

        if (Dm.truthy(burnstate))
        {
            mutable_appearance burn_overlay = mutable_appearance(
                icon: Dm.resource("icons/mob/effects/dam_mob.dmi"),
                icon_state: Dm.text("[]_[]_0[]", dmg_overlay_type, body_zone, burnstate),
                layer: -DAMAGE_LAYER
            ) as mutable_appearance;
            LAZYADD(ref overlays, burn_overlay);
        }

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze != null)
        {
            // future todo : icon states for dirty bandages as well
            // future todo : different color for different gauze types (med gauze is blue)
            mutable_appearance gauze_overlay = await current_gauze.build_worn_icon(
                default_layer: GAUZE_LAYER, // build_worn_icon inverts it for us
                override_file: Dm.resource("icons/mob/human/bandage.dmi"),
                override_state: current_gauze.worn_icon_state) as mutable_appearance;
            LAZYADD(ref overlays, gauze_overlay);
        }

        return overlays;
    }

    //we inform the bodypart of the changes that happened to the owner, or give it the informations from a source mob.
    //set is_creating to true if you want to change the appearance of the limb outside of mutation changes or forced changes.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1196
    [ShouldCallParent]
    public virtual async DmTask<object> update_limb(float? dropping_limb = null, float? is_creating = null)
    {
        dropping_limb ??= FALSE;
        is_creating ??= FALSE;

        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_UPDATED]))
            this._SendSignal(COMSIG_BODYPART_UPDATED, Dm.list(this, dropping_limb, is_creating));
        if (Dm.truthy(owner))
        {
            if (Dm.truthy(owner._listen_lookup?[COMSIG_CARBON_BODYPART_UPDATED]))
                owner._SendSignal(COMSIG_CARBON_BODYPART_UPDATED, Dm.list(owner, this, dropping_limb, is_creating));
        }

        // IS_ORGANIC_LIMB(src) (__DEFINES/surgery.dm:60): no member yet, expanded inline (rules 11.1)
        if (Dm.truthy(Dm.bitand(bodytype, BODYTYPE_ORGANIC)))
        {
            // Try to add a cached blood type data, we must do it in here because for some reason DNA gets initialized AFTER the mob's limbs are created.
            // Should be fine as this gets called before all the important stuff happens
            if (Dm.truthy(is_creating) && !Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_VIRGIN)))
            {
                blood_dna_info = owner.get_blood_dna_list() as DmList;
                // need to remove the synethic blood DNA that is initialized
                // wash also adds the blood dna again
                await wash(CLEAN_TYPE_BLOOD);
                Dm.mask(ref bodypart_flags, Dm.bitnot(BODYPART_VIRGIN));
            }
            if (!Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_UNHUSKABLE)) && Dm.truthy(owner) && HAS_TRAIT(owner, TRAIT_HUSK))
            {
                dmg_overlay_type = ""; //no damage overlay shown when husked
                // /datum/status_effect/zombie::id is "zombified" (zombies.dm:48), as carbon.dm:712 can_be_revived reads it
                is_husked = HAS_TRAIT_FROM_ONLY(owner, TRAIT_HUSK, "zombified") ? HUSKED_ZOMBIE : HUSKED_BURN;
            }
            else if (Dm.truthy(owner) && HAS_TRAIT(owner, TRAIT_INVISIBLE_MAN))
            {
                dmg_overlay_type = ""; //no damage overlay shown when invisible since the wounds themselves are invisible.
                is_invisible = TRUE;
            }
            else
            {
                dmg_overlay_type = Dm.initial(this, (obj_item_bodypart p) => p.dmg_overlay_type);
                is_husked = NONE;
                is_invisible = FALSE;
            }
        }

        update_draw_color();

        if (!Dm.truthy(is_creating) || !Dm.truthy(owner))
            return FALSE;

        // There should technically to be an ishuman(owner) check here, but it is absent because no basetype carbons use bodyparts
        // No, xenos don't actually use bodyparts. Don't ask.
        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;

        limb_gender = (human_owner.physique == MALE) ? "m" : "f";
        if (HAS_TRAIT(human_owner, TRAIT_USES_SKINTONES))
            skin_tone = human_owner.skin_tone;
        else if (HAS_TRAIT(human_owner, TRAIT_MUTANT_COLORS))
        {
            skin_tone = "";
            datum_species owner_species = (datum_species)human_owner.dna.species;
            if (Dm.truthy(owner_species.fixed_mut_color))
                species_color = owner_species.fixed_mut_color;
            else
                species_color = (string)human_owner.dna.features[FEATURE_MUTANT_COLOR];
        }
        else
        {
            skin_tone = "";
            species_color = "";
        }

        update_draw_color();

        // Recolors mutant overlays to match new mutant colors
        // Ensures marking overlays are updated accordingly as well
        foreach (datum_bodypart_overlay_mutant overlay in Dm.each<datum_bodypart_overlay_mutant>(bodypart_overlays))
            overlay.inherit_color(this, force: TRUE);
        // Ensures marking overlays are updated accordingly as well
        foreach (datum_bodypart_overlay_simple_body_marking marking in Dm.each<datum_bodypart_overlay_simple_body_marking>(bodypart_overlays))
            marking.set_appearance(human_owner.dna.features[marking.dna_feature_key], species_color);

        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1257
    public virtual void update_draw_color()
    {
        draw_color = null;
        if (Dm.truthy(LAZYLEN(color_overrides)))
        {
            float? priority = null;
            foreach (object override_priority in Dm.each(color_overrides))
            {
                if (Dm.num(text2num(override_priority)) > Dm.num(priority))
                {
                    priority = text2num(override_priority);
                    draw_color = color_overrides[override_priority];
                }
            }
            return;
        }
        if (Dm.truthy(should_draw_greyscale))
            draw_color = (Dm.truthy(species_color) ? species_color : (Dm.truthy(skin_tone) ? skintone2hex(skin_tone) : null));
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1269
    public virtual void add_color_override(object new_color = null, object color_priority = null)
    {
        LAZYSET(ref color_overrides, Dm.text("[]", color_priority), new_color);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1272
    public virtual void remove_color_override(object color_priority = null)
    {
        LAZYREMOVE(ref color_overrides, Dm.text("[]", color_priority));
    }

    /// To update the bodypart's icon when not attached to a mob
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1293
    [ShouldCallParent]
    public virtual async DmTask update_icon_dropped()
    {
        cut_overlays();
        DmList standing = await get_limb_icon(dropped: TRUE) as DmList;
        if (!Dm.truthy(length(standing)))
        {
            icon_state = Dm.initial(this, (atom p) => p.icon_state); //no overlays found, we default back to initial icon.
            return;
        }

        foreach (image img in Dm.each_cast<image>(standing))
        {
            img.pixel_w += px_x;
            img.pixel_z += px_y;
        }
        add_overlay(standing);
    }

    ///Generates an /image for the limb to be used as an overlay
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1330
    [ShouldCallParent]
    public virtual async DmTask<object> get_limb_icon(object dropped = null)
    {
        icon_state = ""; //to erase the default sprite, we're building the visual aspects of the bodypart through overlays alone.

        DmList dot = null;
        dot = Dm.list();
        // Stumps are FAKE limbs that hold the spot for REAL limbs. thusly no sprite of their own, so early return!
        if (Dm.truthy(Dm.bitand(bodypart_flags, BODYPART_STUMP))) // IS_STUMP(src) (__DEFINES/surgery.dm:67), inline
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_GET_LIMB_ICON]))
                this._SendSignal(COMSIG_BODYPART_GET_LIMB_ICON, Dm.list(this, dot, dropped));
            return dot;
        }

        // Arms past the first pair get body_zone suffixed (l_arm_2 and so on). No state exists for them
        // This guard is bad :)
        if (held_index >= 3)
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_GET_LIMB_ICON]))
                this._SendSignal(COMSIG_BODYPART_GET_LIMB_ICON, Dm.list(this, dot, dropped));
            return dot;
        }

        object image_dir = Dm.truthy(dropped) ? SOUTH : null;
        // tg's image(icon, "state", layer, dir = x) is written with icon_state and layer named (rules 10.9: a text second
        // argument is the icon_state), which BYOND binds the same way (loc stays null).
        // Handles invisibility (not alpha or actual invisibility but invisibility)
        if (Dm.truthy(is_invisible))
        {
            dot[image(icon_invisible, icon_state: Dm.text("invisible_[]", body_zone), layer: -BODYPARTS_LAYER, dir: image_dir)] = NONE;
            if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_GET_LIMB_ICON]))
                this._SendSignal(COMSIG_BODYPART_GET_LIMB_ICON, Dm.list(this, dot, dropped));
            return dot;
        }

        // Normal non-husk handling
        // This is the MEAT of limb icon code
        object used_icon = ((Dm.truthy(should_draw_greyscale) ? icon_greyscale : (object)should_draw_greyscale) is var or__1 && Dm.truthy(or__1) ? or__1 : icon_static);
        string used_state = Dm.text("[]_[][]", limb_id, body_zone, Dm.truthy(is_dimorphic) ? Dm.text("_[]", limb_gender) : "");

        image limb = image(used_icon, icon_state: used_state, layer: -BODYPARTS_LAYER, dir: image_dir);
        image aux = null;

        // icon_exists_or_scream(limb.icon, limb.icon_state) //Prints a stack trace on the first failure of a given iconstate.
        // GAP(lead, __HELPERS/icons.cs): icon_exists needs the .dmi's states (rust-g icon metadata), which the server does not
        // load (D104); the check only logs a missing state.
        Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1363", "get_limb_icon: icon_exists_or_scream (logs a missing icon state; .dmi metadata is the client's, D104)");

        dot[limb] = Dm.bitor(LIMB_OVERLAY_TEXTURED, LIMB_OVERLAY_CORE);

        if (Dm.truthy(aux_zone)) //Hand shit
        {
            aux = image(limb.icon, icon_state: Dm.text("[]_[]", limb_id, aux_zone), layer: -Dm.num(aux_layer), dir: image_dir);
            dot[aux] = Dm.bitor(LIMB_OVERLAY_TEXTURED, LIMB_OVERLAY_CORE);
        }

        if (Dm.truthy(dropped) && Dm.truthy(dmg_overlay_type))
        {
            if (Dm.truthy(brutestate))
            {
                // divided into two overlays: one that gets colored and one that doesn't.
                image brute_damage_overlay = mutable_appearance(Dm.resource("icons/mob/effects/dam_mob.dmi"), Dm.text("[]_[]_[]0_overlay", dmg_overlay_type, body_zone, brutestate), -DAMAGE_LAYER) as image;
                Dm.combine(ref brute_damage_overlay.appearance_flags, RESET_COLOR);
                image brute_blood_overlay = image(Dm.resource("icons/mob/effects/dam_mob.dmi"), icon_state: Dm.text("[]_[]_[]0", dmg_overlay_type, body_zone, brutestate), layer: -DAMAGE_LAYER, dir: SOUTH);
                brute_blood_overlay.color = await get_color_from_blood_list(blood_dna_info);
                Dm.append(ref brute_blood_overlay.overlays, brute_damage_overlay);
                dot[brute_blood_overlay] = NONE;
            }
            if (Dm.truthy(burnstate))
            {
                image burn_damage_overlay = image(Dm.resource("icons/mob/effects/dam_mob.dmi"), icon_state: Dm.text("[]_[]_0[]", dmg_overlay_type, body_zone, burnstate), layer: -DAMAGE_LAYER, dir: SOUTH);
                dot[burn_damage_overlay] = NONE;
            }
        }

        atom location = (loc is var or__2 && Dm.truthy(or__2) ? or__2 : (owner is var or__3 && Dm.truthy(or__3) ? or__3 : (atom)this)) as atom;
        if (blocks_emissive != EMISSIVE_BLOCK_NONE)
        {
            // DEVIATION(DV12): emissive blockers are not created (lighting planes are not ported)
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1386", "get_limb_icon: the limb's (and hand's) emissive blockers (DV12)");
        }

        if (!Dm.truthy(is_husked) && Dm.truthy(is_emissive))
        {
            // DEVIATION(DV12): emissive appearances are not created (lighting planes are not ported)
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1398", "get_limb_icon: the limb's emissive appearances (DV12)");
        }

        if (Dm.truthy(is_husked))
        {
            foreach (image husk_image in Dm.each_cast<image>(await huskify_image(limb)))
                dot[husk_image] = Dm.bitor(LIMB_OVERLAY_TEXTURED, LIMB_OVERLAY_CORE);
            if (Dm.truthy(aux))
            {
                foreach (image husk_image in Dm.each_cast<image>(await huskify_image(aux)))
                    dot[husk_image] = Dm.bitor(LIMB_OVERLAY_TEXTURED, LIMB_OVERLAY_CORE);
            }
            draw_color = is_husked == HUSKED_ZOMBIE ? zombie_color : husk_color;
        }
        else
            update_draw_color();

        if (Dm.truthy(draw_color))
        {
            limb.color = Dm.text("[]", draw_color);
            if (Dm.truthy(aux_zone))
                aux.color = Dm.text("[]", draw_color);
        }

        // No need to handle leg layering if dropped, we only face south anyways
        if (!Dm.truthy(dropped))
        {
            // Legs are a bit goofy in regards to layering, and we will need two images instead of one to fix that
            foreach ((object generated_overlay, object generated_overlay_flags) in Dm.pairs(dot))
            {
                DmList maked_generated_overlays = handle_masking(generated_overlay as image) as DmList;
                if (!Dm.truthy(length(maked_generated_overlays)))
                    continue;
                // Remove the old, unmasked image
                Dm.remove(ref dot, generated_overlay);
                // Add in the new, masked images (with the same flags)
                foreach (object masked_image in Dm.each(maked_generated_overlays))
                    dot[masked_image] = generated_overlay_flags;
            }

            // Apply height to the overlays we generated so far
            // This is done before collecting bodypart overlays so we don't apply height twice to the same overlays
            if (owner != null)
            {
                foreach ((object generated_overlay, object generated_overlay_flags) in Dm.pairs(dot))
                {
                    // While you may think that heads could be applied with UPPER_BODY instead of ENTIRE_BODY to save us one filter,
                    // it's more important to keep it consistent for things like getflaticon
                    owner.apply_height(generated_overlay as image, ENTIRE_BODY);
                }
            }
        }

        // Draw external organs like horns and frills
        // Height is applied again in here so we can specify where the overlay is set (ie offset_location)
        foreach (datum_bodypart_overlay overlay in Dm.each<datum_bodypart_overlay>(bodypart_overlays))
        {
            if (!Dm.truthy(overlay.can_draw_on_bodypart(this, owner)))
                continue;

            foreach ((object actual_overlay, object actual_overlay_flags) in Dm.pairs(await overlay.get_all_overlays(this) as DmList))
            {
                if (Dm.truthy(dropped) || owner == null)
                {
                    dot[image(actual_overlay, dir: SOUTH)] = overlay.overlay_flags;
                    continue;
                }

                owner.apply_height(actual_overlay as image, overlay.offset_location);
                dot[actual_overlay] = actual_overlay_flags;
            }
        }

        // Then texture everything at once, including bodypart overlays
        foreach (datum_bodypart_texture texture in Dm.each_cast<datum_bodypart_texture>(bodypart_textures))
        {
            if (!Dm.truthy(texture.can_texture_bodypart(this)))
                continue;
            foreach ((object generated_overlay, object generated_overlay_flags) in Dm.pairs(dot))
            {
                if (!Dm.truthy(Dm.bitand(generated_overlay_flags, LIMB_OVERLAY_TEXTURED)))
                    continue;

                texture.modify_bodypart_appearance(generated_overlay as image, generated_overlay_flags);
            }
        }

        // tg: #ifndef UNIT_TESTS ... if(PERFORM_ALL_TESTS(focus_only/bodypart_overlay_flags)) ... #endif (:1469-1475):
        // PERFORM_ALL_TESTS is FALSE in production (rules 1.7)
        if (Dm.truthy(FALSE))
            Dm.NotTranslated("code/modules/surgery/bodyparts/_bodyparts.dm:1470", "get_limb_icon: the emissive meta-tag focus test (CI only)");

        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_GET_LIMB_ICON]))
            this._SendSignal(COMSIG_BODYPART_GET_LIMB_ICON, Dm.list(this, dot, dropped));
        return assoc_to_keys(dot);
    }

    /**
     * Takes in an image and greyscales it to later be recolored to look like a husk
     *
     * Then returns a separate image/MA that is the blood overlay for the husk
     * May return multiple if the blood overlay has an emissive associated
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1486
    public virtual async DmTask<object> huskify_image(image thing_to_husk = null)
    {
        DmList dot = null;
        icon husk_icon = Dm.icon(thing_to_husk.icon); // new(thing_to_husk.icon): BYOND's new /icon(file) is icon(file)
        husk_icon.ColorTone(HUSK_COLOR_TONE);
        thing_to_husk.icon = husk_icon;

        mutable_appearance husk_blood = mutable_appearance(icon_husk, Dm.text("[]_husk_[]", husk_type, body_zone), thing_to_husk.layer, appearance_flags: RESET_COLOR) as mutable_appearance;
        dot = Dm.list(husk_blood);

        // BLEND_INSET_OVERLAY on KEEP_TOGETHER atoms masks itself with the atom,
        // so we cannot add this as an overlay to our limb to have it automatically mask
        husk_blood.blend_mode = BLEND_INSET_OVERLAY;
        husk_blood.dir = thing_to_husk.dir;

        if (!Dm.truthy(LAZYLEN(blood_dna_info)))
        {
            husk_blood.color = BLOOD_COLOR_RED;
            return dot;
        }

        husk_blood.color = await get_color_from_blood_list(blood_dna_info);

        object average_emissive_alpha = 0;
        foreach ((object dna, object blood_type) in Dm.pairs(blood_dna_info))
            average_emissive_alpha = Dm.add(average_emissive_alpha, (astype(blood_type, typeof(datum_blood_type)) as datum_blood_type)?.get_emissive_alpha(this));

        if (!Dm.truthy(average_emissive_alpha))
            return dot;

        average_emissive_alpha = Dm.div(average_emissive_alpha, LAZYLEN(blood_dna_info));
        // DEVIATION(DV12): the husk blood's emissive appearance is not created (lighting planes are not ported)
        Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1513", "huskify_image: the husk blood's emissive appearance (DV12)");
        return dot;
    }

    /**
     * Adds a bodypart overlay to the limb
     *
     * * overlay: The overlay to add. Either an instance of a bodypart overlay or a typepath of a bodypart overlay.
     * If you pass a typepath, the proc will avoid creating duplicates.
     * * update: Whether to call update procs after adding the overlay.
     * Set this to FALSE if you are adding multiple overlays at once.
     *
     * Returns the overlay that was added, or null if it was not added.
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1529
    public virtual async DmTask<object> add_bodypart_overlay(object overlay = null, float? update = null)
    {
        update ??= TRUE;
        if (ispath(overlay, typeof(datum_bodypart_overlay)))
        {
            if (Dm.truthy(Dm.locate_type(overlay as Type, bodypart_overlays)))
                return null;
            overlay = Dm.@new(overlay as Type);
        }

        var added = (datum_bodypart_overlay)overlay;
        LAZYADD(ref bodypart_overlays, added);
        await added.added_to_limb(this);
        if (!Dm.truthy(update))
            return added;
        if (owner == null)
            await update_icon_dropped();
        else if (!Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await owner.update_body_parts();
        return added;
    }

    /**
     * Removes a bodypart overlay from the limb
     *
     * * overlay: The overlay to remove. Either an instance of a bodypart overlay or a typepath of a bodypart overlay.
     * If you pass a typepath, the first overlay of that typepath found will be removed.
     * * update: Whether to call update procs after removing the overlay.
     * Set this to FALSE if you are removing multiple overlays at once.
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1553
    public virtual async DmTask remove_bodypart_overlay(object overlay = null, float? update = null)
    {
        update ??= TRUE;
        if (ispath(overlay, typeof(datum_bodypart_overlay)))
        {
            overlay = Dm.locate_type(overlay as Type, bodypart_overlays);
            if (overlay == null)
                return;
        }

        var removed = (datum_bodypart_overlay)overlay;
        LAZYREMOVE(ref bodypart_overlays, removed);
        await removed.removed_from_limb(this);
        if (!Dm.truthy(update))
            return;
        if (owner == null)
            await update_icon_dropped();
        else if (!Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await owner.update_body_parts();
    }

    /**
     * Adds a bodypart texture to the limb
     *
     * * texture: The texture to add. Either an instance of a bodypart texture or a typepath of a bodypart texture.
     * If you pass a typepath, the proc will avoid creating duplicates.
     * * update: Whether to call update procs after adding the texture.
     * Set this to FALSE if you are adding multiple textures at once.
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1576
    public virtual async DmTask add_bodypart_texture(object texture = null, float? update = null)
    {
        update ??= TRUE;
        if (ispath(texture, typeof(datum_bodypart_texture)))
        {
            if (Dm.truthy(Dm.locate_type((Type)texture, bodypart_textures)))
                return;
            texture = Dm.@new((Type)texture);
        }

        LAZYADD(ref bodypart_textures, texture);
        if (!Dm.truthy(update))
            return;
        if (owner == null)
            await update_icon_dropped();
        else if (!Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await owner.update_body_parts();
    }

    /**
     * Removes a bodypart texture from the limb
     *
     * * texture: The texture to remove. Either an instance of a bodypart texture or a typepath of a bodypart texture.
     * If you pass a typepath, the first texture of that typepath found will be removed.
     * * update: Whether to call update procs after removing the texture.
     * Set this to FALSE if you are removing multiple textures at once.
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1598
    public virtual async DmTask remove_bodypart_texture(object texture = null, float? update = null)
    {
        update ??= TRUE;
        if (ispath(texture, typeof(datum_bodypart_texture)))
        {
            // texture = locate(texture) in bodypart_textures (a typepath value; SHIM-GAP, see add_bodypart_texture)
            object found__1 = null;
            foreach (object item in Dm.each(bodypart_textures))
            {
                if (istype(item, (Type)texture)) { found__1 = item; break; }
            }
            texture = found__1;
            if (texture == null)
                return;
        }

        LAZYREMOVE(ref bodypart_textures, texture);
        if (!Dm.truthy(update))
            return;
        if (owner == null)
            await update_icon_dropped();
        else if (!Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await owner.update_body_parts();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1736
    public virtual async DmTask<object> update_part_wound_overlay()
    {
        if (!Dm.truthy(owner))
            return FALSE;
        if (!Dm.truthy(can_bleed()))
        {
            if (Dm.truthy(bleed_overlay_icon))
            {
                bleed_overlay_icon = null;
                await owner.update_wound_overlays();
            }
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_UPDATE_WOUND_OVERLAY]) ? NONE : this._SendSignal(COMSIG_BODYPART_UPDATE_WOUND_OVERLAY, Dm.list(this, cached_bleed_rate))), COMPONENT_PREVENT_WOUND_OVERLAY_UPDATE)))
            return null;

        float bleed_rate = cached_bleed_rate;
        object new_bleed_icon = null;

        float sw__1 = bleed_rate;
        if (sw__1 >= -INFINITY && sw__1 <= BLEED_OVERLAY_LOW)
            new_bleed_icon = null;
        else if (sw__1 >= BLEED_OVERLAY_LOW && sw__1 <= BLEED_OVERLAY_MED)
            new_bleed_icon = Dm.text("[]_1", body_zone);
        else if (sw__1 >= BLEED_OVERLAY_MED && sw__1 <= BLEED_OVERLAY_GUSH)
        {
            if (owner.body_position == LYING_DOWN || HAS_TRAIT(owner, TRAIT_STASIS) || owner.stat == DEAD)
                new_bleed_icon = Dm.text("[]_2s", body_zone);
            else
                new_bleed_icon = Dm.text("[]_2", body_zone);
        }
        else if (sw__1 >= BLEED_OVERLAY_GUSH && sw__1 <= INFINITY)
        {
            if (HAS_TRAIT(owner, TRAIT_STASIS) || owner.stat == DEAD)
                new_bleed_icon = Dm.text("[]_2s", body_zone);
            else
                new_bleed_icon = Dm.text("[]_3", body_zone);
        }

        if (!Dm.eq(new_bleed_icon, bleed_overlay_icon))
        {
            bleed_overlay_icon = new_bleed_icon;
            await owner.update_wound_overlays();
        }
        return null;
    }

    ///A multi-purpose setter for all things immediately important to the icon and iconstate of the limb.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1810
    public virtual async DmTask change_appearance(object icon = null, object id = null, object greyscale = null, object dimorphic = null)
    {
        object icon_holder = null;
        if (Dm.truthy(greyscale))
        {
            icon_greyscale = icon;
            icon_holder = icon;
            should_draw_greyscale = TRUE;
        }
        else
        {
            icon_static = icon;
            icon_holder = icon;
            should_draw_greyscale = FALSE;
        }

        if (Dm.truthy(id)) //limb_id should never be falsey
            limb_id = (string)id;

        if (dimorphic != null)
            is_dimorphic = Dm.num(dimorphic);

        if (!Dm.truthy(owner))
            await update_icon_dropped();
        else if (!Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await owner.update_body_parts();

        //This foot gun needs a safety
        // if(!icon_exists(icon_holder, "[limb_id]_[body_zone][is_dimorphic ? "_[limb_gender]" : ""]")) reset_appearance();
        // stack_trace(...) (:1832-1834): GAP(lead, __HELPERS/icons.cs): icon_exists needs the .dmi's states, which the server
        // does not load (D104); the safety only guards against a typo in a limb id.
        Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1832", "change_appearance: the icon_exists safety (.dmi metadata is the client's, D104)");
    }

    ///Resets the base appearance of a limb to it's default values.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1838
    public virtual async DmTask reset_appearance()
    {
        icon_static = Dm.initial(this, (obj_item_bodypart p) => p.icon_static);
        icon_greyscale = Dm.initial(this, (obj_item_bodypart p) => p.icon_greyscale);
        limb_id = Dm.initial(this, (obj_item_bodypart p) => p.limb_id);
        is_dimorphic = Dm.initial(this, (obj_item_bodypart p) => p.is_dimorphic);
        should_draw_greyscale = Dm.initial(this, (obj_item_bodypart p) => p.should_draw_greyscale);

        if (!Dm.truthy(owner))
            await update_icon_dropped();
        else if (!Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await owner.update_body_parts();
    }
}
