// Translated from tgstation (snapshot 1d99bd7f): code/datums/bodypart_overlays/mutant_bodypart_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5: the monkey's tail is the first mutant overlay that runs).

///Variant of bodypart_overlay meant to work synchronously with external organs. Gets imprinted upon Insert in on_species_gain
// tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:4
public partial class datum_bodypart_overlay_mutant : datum_bodypart_overlay
{
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:21
    [DmParams("attached_organ")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_organ attached_organ = Dm.arg<obj_item_organ>(__extra, 0);
        await base.New(__extra);

        RegisterSignal(attached_organ, COMSIG_ORGAN_IMPLANTED, nameof(on_mob_insert));
    }

    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:26
    public virtual void on_mob_insert(obj_item_organ parent = null, mob_living_carbon receiver = null)
    {
        if (isalien(receiver))
            return; // Xenomorphs have no dna or other features required to support this, maybe one day

        if (!Dm.truthy(should_visual_organ_apply_to(parent.type, receiver)))
            stack_trace(Dm.text("adding a [] to a [] when it shouldn't be!", parent.type, receiver.type), "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 33);

        if (Dm.truthy(imprint_on_next_insertion)) //We only want this set *once*
        {
            object feature_name = receiver.dna.features[feature_key] ?? ((datum_species)receiver.dna.species).mutant_organs[parent.type];
            if (feature_name == null)
            {
                stack_trace(Dm.text("[] has no default feature name for organ []!", type, parent.type), "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 38);
                feature_name = get_consistent_feature_entry(get_global_feature_list() as DmList); //fallback to something
            }
            set_appearance_from_name(feature_name);
            imprint_on_next_insertion = FALSE;
        }
    }

    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:43
    public override async DmTask<object> get_overlay(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        inherit_color(limb); // If draw_color is not set yet, go ahead and do that
        object created_overlays = await base.get_overlay(limb, layer_index, layer_real);
        // tg: :46-53  the emissive layers of a sprite accessory: DEVIATION(DV12), emissives are not ported
        return created_overlays;
    }

    ///Completely random image and color generation (obeys what a player can choose from)
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:57
    public virtual void randomize_appearance()
    {
        randomize_sprite();
        draw_color = Dm.text("#[]", random_color());
        imprint_on_next_insertion = FALSE;
    }

    ///Grab a random sprite
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:63
    public virtual void randomize_sprite()
    {
        sprite_datum = get_random_appearance() as datum_sprite_accessory;
    }

    ///Grab a random appearance datum (thats not locked)
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:67
    public virtual object get_random_appearance()
    {
        DmList valid_restyles = Dm.list();
        DmList feature_list = get_global_feature_list() as DmList;
        foreach (object accessory in Dm.each(feature_list))
        {
            datum_sprite_accessory accessory_datum = feature_list[accessory] as datum_sprite_accessory;
            if (Dm.truthy(accessory_datum.locked)) //locked is for stuff that shouldn't appear here
                continue;
            if (!Dm.truthy(accessory_datum.natural_spawn))
                continue;
            Dm.append(ref valid_restyles, accessory_datum);
        }
        return pick(valid_restyles);
    }

    ///Return the BASE icon state of the sprite datum (so not the gender, layer, feature_key)
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:81
    public virtual object get_base_icon_state()
    {
        return sprite_datum.icon_state;
    }

    ///Used to build the final incon state for the sprite
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:85
    public virtual object build_icon_state(object layer_index = null, obj_item_bodypart limb = null)
    {
        object gender_key = Dm.truthy(sprite_datum.gender_specific) && Dm.truthy(limb?.limb_gender) ? limb.limb_gender : "m"; // Male is default because sprite accessories are so ancient they predate the concept of not hardcoding gender
        object base_state = get_base_icon_state();
        return Dm.text("[]_[]_[]_[]", gender_key, feature_key, base_state, layer_index);
    }

    ///Get the image we need to draw on the person. Called from get_overlay() which is called from _bodyparts.dm. Limb can be null
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:92
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        if (sprite_datum == null)
            throw Dm.CRASH(Dm.text("Trying to call get_image() on [] while it didn't have a sprite_datum. This shouldn't happen, report it as soon as possible.", type), "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 94);

        mutable_appearance appearance = mutable_appearance(sprite_datum.icon, build_icon_state(layer_index, limb) as string, layer: Dm.num(layer_real)) as mutable_appearance;
        if (Dm.truthy(sprite_datum.center))
            center_image(appearance, sprite_datum.dimension_x, sprite_datum.dimension_y);

        return appearance;
    }

    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:102
    public override async DmTask color_image(image overlay = null, obj_item_bodypart limb = null, object layer_index = null)
    {
        overlay.color = Dm.truthy(sprite_datum.color_src) ? (Dm.truthy(dye_color) ? dye_color : draw_color) : null;
    }

    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:105
    public override async DmTask added_to_limb(obj_item_bodypart limb = null)
    {
        inherit_color(limb);
    }

    ///Change our accessory sprite, using the accesssory type. If you need to change the sprite for something, use simple_change_sprite()
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:109
    [DmParams("accessory_type")]
    public override void set_appearance(params object[] __extra)
    {
        sprite_datum = fetch_sprite_datum(Dm.arg(__extra, 0)) as datum_sprite_accessory;
    }

    ///In a lot of cases, appearances are stored in DNA as the Name, instead of the path. Use set_appearance instead of possible
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:113
    public virtual void set_appearance_from_name(object accessory_name = null)
    {
        sprite_datum = fetch_sprite_datum_from_name(accessory_name) as datum_sprite_accessory;
    }

    ///Generate a unique key based on our sprites. So that if we've aleady drawn these sprites, they can be found in the cache and wont have to be drawn again (blessing and curse, but mostly curse)
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:117
    public override object icon_render_key(obj_item_bodypart limb = null)
    {
        DmList dot = Dm.list();
        Dm.append(ref dot, Dm.text("[]", get_base_icon_state()));
        Dm.append(ref dot, Dm.text("[]", feature_key));
        Dm.append(ref dot, Dm.text("[]", Dm.truthy(dye_color) ? dye_color : draw_color));
        return dot;
    }

    ///Return a dumb glob list for this specific feature (called from parse_sprite)
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:124
    public virtual object get_global_feature_list()
    {
        DmList feature_list = SSaccessories.feature_list[feature_key] as DmList;
        if (feature_list == null)
        {
            stack_trace("External organ has no feature list, it will render invisible", "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 127);
            return Dm.list();
        }
        return feature_list;
    }

    /// Used to add special coloring behavior to certain mutant parts.
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:132
    public virtual object override_color(obj_item_bodypart bodypart_owner = null)
    {
        throw Dm.CRASH("External organ color set to override with no override proc.", "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 133);
    }

    ///Give the organ its color. Force will override the existing one.
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:136
    public virtual object inherit_color(obj_item_bodypart bodypart_owner = null, object force = null)
    {
        if (bodypart_owner == null)
        {
            draw_color = null;
            return TRUE;
        }

        if (Dm.truthy(draw_color) && !Dm.truthy(force))
            return FALSE;

        object sw__1 = color_source;
        if (Dm.eq(sw__1, ORGAN_COLOR_OVERRIDE))
            draw_color = override_color(bodypart_owner);
        else if (Dm.eq(sw__1, ORGAN_COLOR_INHERIT))
            draw_color = bodypart_owner.draw_color;
        else if (Dm.eq(sw__1, ORGAN_COLOR_HAIR))
        {
            datum_species species = bodypart_owner.owner?.dna?.species as datum_species;
            object fixed_color = species?.get_fixed_hair_color(bodypart_owner.owner);
            if (!ishuman(bodypart_owner.owner))
            {
                draw_color = fixed_color;
                return null;
            }
            mob_living_carbon_human human_owner = bodypart_owner.owner as mob_living_carbon_human;
            obj_item_bodypart_head my_head = human_owner.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head; //not always the same as bodypart_owner
            //head hair color takes priority, owner hair color is a backup if we lack a head or something
            if (my_head == null)
            {
                draw_color = Dm.truthy(fixed_color) ? fixed_color : human_owner.hair_color;
                return null;
            }
            if (Dm.truthy(Dm.bitand(my_head.head_flags, HEAD_HAIR | HEAD_FACIAL_HAIR)))
                draw_color = Dm.truthy(my_head.fixed_hair_color) ? my_head.fixed_hair_color : my_head.hair_color;
            else //inherit mutant color of the bodypart if the owner doesn't have hair.
                draw_color = bodypart_owner.draw_color;
        }

        return TRUE;
    }

    ///Sprite accessories are singletons, stored list("Big Snout" = instance of /datum/sprite_accessory/snout/big), so here we get that singleton
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:169
    public virtual object fetch_sprite_datum(object accessory_path = null)
    {
        return fetch_sprite_datum_from_name(Dm.initial(accessory_path, "name"));
    }

    ///Get the singleton from the sprite name
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:173
    public virtual object fetch_sprite_datum_from_name(object accessory_name = null)
    {
        DmList feature_list = get_global_feature_list() as DmList;
        object found = feature_list[accessory_name];
        if (Dm.truthy(found))
            return found;

        if (!Dm.truthy(length(feature_list)))
            throw Dm.CRASH(Dm.text("External organ [] returned no sprite datums from get_global_feature_list(), so no accessories could be found!", type), "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 180);
        else if (Dm.truthy(accessory_name))
            throw Dm.CRASH(Dm.text("External organ [] couldn't find sprite accessory []!", type, accessory_name), "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 182);
        else
            throw Dm.CRASH(Dm.text("External organ [] had fetch_sprite_datum called with a null accessory name!", type), "code/datums/bodypart_overlays/mutant_bodypart_overlay.dm", 184);
    }

    ///From dye sprays. Set the dye_color (draw_color override) of this organ to a new value.
    // tg: code/datums/bodypart_overlays/mutant_bodypart_overlay.dm:187
    public virtual async DmTask set_dye_color(object new_color = null, obj_item_organ organ = null)
    {
        dye_color = new_color;
        if (organ.owner != null)
            await organ.owner.update_body_parts();
        else if (organ.bodypart_owner != null)
            await organ.bodypart_owner.update_icon_dropped();
    }
}
