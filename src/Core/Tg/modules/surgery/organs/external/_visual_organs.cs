// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/external/_visual_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the second /obj/item/organ var block, setup_bodypart_overlay, should_visual_organ_apply_to, simple_change_sprite
// and update_overlays (C5: the monkey's tail), mutate_feature with genetics (gaps P3), the lizard's horns, frills and snout, the
// moth's antennae and the podperson's hair with their overlays (gaps P3b: the organs the DNA infuser grows).

/*
System for drawing organs with overlays. These overlays are drawn directly on the bodypart, attached to a person or not
Works in tandem with the /datum/sprite_accessory datum to generate sprites
Unlike normal organs, we're actually inside a persons limbs at all times
*/
// The [DmPath] of /obj/item/organ is on its earliest .dme file (rules 2.3).
// tg: code/modules/surgery/organs/external/_visual_organs.dm:6
public partial class obj_item_organ : obj_item
{
    ///The overlay datum that actually draws stuff on the limb
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:8  var/datum/bodypart_overlay/mutant/bodypart_overlay:
    // object (rules 3.2, a typed var that holds wider values): the type sets a typepath, setup_bodypart_overlay (:33)
    // stores the instance made from it
    public object bodypart_overlay;

    ///With what DNA block do we mutate in mutate_feature() ? For genetics
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:11  var/datum/dna_block/dna_block (V3: the organ types
    // set a typepath)
    public Type dna_block;

    ///Set to EXTERNAL_BEHIND, EXTERNAL_FRONT or EXTERNAL_ADJACENT if you want to draw one of those layers as the object sprite. FALSE to use your own
    ///This will not work if it doesn't have a limb to generate its icon with
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:15
    public float use_mob_sprite_as_obj_sprite = FALSE;

    ///Does this organ have any bodytypes to pass to its bodypart_owner?
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:18
    public float external_bodytypes = NONE;
    ///Does this organ have any bodyshapes to pass to its bodypart_owner?
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:20
    public float external_bodyshapes = NONE;

    ///Which flags does a 'modification tool' need to have to restyle us, if it all possible (located in code/_DEFINES/mobs)
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:23
    public float restyle_flags = NONE;

    ///If not null, overrides the appearance with this sprite accessory datum
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:26  var/datum/sprite_accessory/sprite_accessory_override
    // (V3: subtypes set a typepath)
    public Type sprite_accessory_override;

    /**accessory_type is optional if you haven't set sprite_datums for the object, and is used mostly to generate sprite_datums from a persons DNA
    * For _mob_sprite we make a distinction between "Round Snout" and "round". Round Snout is the name of the sprite datum, while "round" would be part of the sprite
    * I'm sorry
    */
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:32
    public virtual void setup_bodypart_overlay(object accessory_type = null)
    {
        datum_bodypart_overlay_mutant overlay = Dm.@new(bodypart_overlay as Type, this) as datum_bodypart_overlay_mutant;
        bodypart_overlay = overlay;

        accessory_type ??= sprite_accessory_override;
        bool update_overlays_now = true;
        if (Dm.truthy(accessory_type))
        {
            overlay.set_appearance(accessory_type);
            overlay.imprint_on_next_insertion = FALSE;
        }
        else if (loc != null) //we've been spawned into the world, and not in nullspace to be added to a limb (yes its fucking scuffed)
            overlay.randomize_appearance();
        else
            update_overlays_now = false;

        if (Dm.truthy(use_mob_sprite_as_obj_sprite) && update_overlays_now)
            update_appearance(UPDATE_OVERLAYS);

        if (Dm.truthy(restyle_flags))
            RegisterSignal(this, COMSIG_ATOM_RESTYLE, nameof(on_attempt_feature_restyle)); // gaps OG1: restyling.dm
    }

    ///Update our features after something changed our appearance
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:70
    public virtual void mutate_feature(object features = null, mob_living_carbon_human human = null)
    {
        if (dna_block == null)
            return;

        datum_bodypart_overlay_mutant overlay = bodypart_overlay as datum_bodypart_overlay_mutant;
        DmList feature_list = overlay.get_global_feature_list() as DmList;

        datum_dna_block_feature feature_block = GLOB.dna_feature_blocks[dna_block] as datum_dna_block_feature;
        overlay.set_appearance_from_name(feature_list[deconstruct_block(feature_block.get_block(features), length(feature_list))]);
    }

    ///If you need to change an external_organ for simple one-offs, use this. Pass the accessory type : /datum/accessory/something
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:80
    public virtual async DmTask simple_change_sprite(object accessory_type = null)
    {
        (bodypart_overlay as datum_bodypart_overlay_mutant).set_appearance(accessory_type); //we only take types for maintainability

        if (owner != null && !Dm.truthy(Dm.bitand(owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS))) //are we a person?
            await owner.update_body_parts();
        else if (bodypart_owner != null)
            await bodypart_owner.update_icon_dropped(); //are we in a limb?
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:90
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!Dm.truthy(use_mob_sprite_as_obj_sprite) || bodypart_owner == null || bodypart_overlay == null)
            return dot;

        Dm.append(ref dot, await (bodypart_overlay as datum_bodypart_overlay).get_all_overlays(bodypart_owner));
        return dot;
    }
}

// tg: code/modules/surgery/organs/external/_visual_organs.dm:52 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /// Some sanity checks, but mostly to check if the person has their preference/dna set to load
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:52
    public static object should_visual_organ_apply_to(object organpath = null, mob_living_carbon target = null)
    {
        if (!Dm.truthy(Dm.initial(organpath, (obj_item_organ p) => p.bodypart_overlay)))
            return TRUE;

        if (organpath == null || target == null)
        {
            stack_trace("passed a null path or mob to 'should_visual_organ_apply_to'", "code/modules/surgery/organs/external/_visual_organs.dm", 57);
            return FALSE;
        }

        // var/datum/bodypart_overlay/mutant/bodypart_overlay = initial(...): a typepath (rules 3.2, typepaths read into a typed
        // local are Type)
        Type bodypart_overlay = Dm.initial(organpath, (obj_item_organ p) => p.bodypart_overlay) as Type;
        object feature_key = (Dm.num(bodypart_overlay != null) is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : Dm.initial(bodypart_overlay, "feature_key") /* GAP(lead): /datum/bodypart_overlay/mutant/var/feature_key is not declared; read by name */);
        if (feature_key == null)
            return TRUE;

        if (!Dm.eq(target.dna.features[feature_key], SPRITE_ACCESSORY_NONE))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/surgery/organs/external/_visual_organs.dm:114 (gaps P3b: the lizard infusion)
public partial class datum_bodypart_overlay_mutant_horns : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:124
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEHAIR)) ? TRUE : FALSE;
    }
}

// tg: code/modules/surgery/organs/external/_visual_organs.dm:143 (gaps P3b: the lizard infusion)
public partial class datum_bodypart_overlay_mutant_frills : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:171  var/static/list/cached_frill_icons (a proc-level static, rules 10.6)
    private static ref DmList get_image__cached_frill_icons => ref TgState.Current.datum_bodypart_overlay_mutant_frills__get_image__cached_frill_icons;

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:152
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEHAIR)) ? TRUE : FALSE;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:155
    public override object override_color(obj_item_bodypart bodypart_owner = null)
    {
        if (bodypart_owner.owner?.get_organ_by_type(typeof(obj_item_organ_tail_fish_cerulean)) != null) //if we are fish frills
            return bodypart_owner.owner.dna.features[FEATURE_TAIL_FISH_COLOR];
        return bodypart_owner.draw_color;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:160
    public override object icon_render_key(obj_item_bodypart limb = null)
    {
        object dot = base.icon_render_key(limb);
        if (LAZYLEN(limb?.owner?.hair_masks) > 0)
            dot = Dm.add(dot, jointext(limb.owner.hair_masks, ","));
        return dot;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:165
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        if (!(LAZYLEN(limb?.owner?.hair_masks) > 0))
            return base.get_image(limb, layer_index, layer_real);

        DmList hair_masks_to_use = limb.owner.hair_masks;
        object icon_state_to_use = build_icon_state(layer_index, limb);
        string frill_cache_key = Dm.text("[]-[]-[]", sprite_datum.type, icon_state_to_use, jointext(hair_masks_to_use, ","));
        icon cached_icon = LAZYACCESS(get_image__cached_frill_icons, frill_cache_key) as icon;
        if (cached_icon == null)
        {
            cached_icon = Dm.icon(sprite_datum.icon, build_icon_state(layer_index, limb));
            foreach (object mask in Dm.each(hair_masks_to_use))
                cached_icon.Blend(Dm.icon(Dm.initial(mask, (datum_hair_mask m) => m.icon), Dm.initial(mask, (datum_hair_mask m) => m.icon_state)), ICON_ADD);
            LAZYSET(ref get_image__cached_frill_icons, frill_cache_key, cached_icon);
        }

        mutable_appearance uncached_appearance = mutable_appearance(cached_icon, layer: Dm.num(layer_real)) as mutable_appearance;
        if (Dm.truthy(sprite_datum.center))
            center_image(uncached_appearance, sprite_datum.dimension_x, sprite_datum.dimension_y);
        return uncached_appearance;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:171  var/static/list/cached_frill_icons
    internal DmList datum_bodypart_overlay_mutant_frills__get_image__cached_frill_icons;
}

///Guess what part of the lizard this is?
// tg: code/modules/surgery/organs/external/_visual_organs.dm:187 (gaps P3b)
public partial class obj_item_organ_snout : obj_item_organ
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:206
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb__arg = null)
    {
        obj_item_bodypart_head limb = limb__arg as obj_item_bodypart_head;
        await base.on_bodypart_insert(limb__arg);
        if (limb.worn_mask_offset == null)
        {
            worn_mask_offset = limb.worn_mask_offset = await Dm.new_async(typeof(datum_worn_feature_offset),
                Dm.named("attached_part", limb),
                Dm.named("feature_key", OFFSET_FACEMASK),
                Dm.named("offset_x", Dm.list(Dm.kv("east", 1), Dm.kv("west", -1)))) as datum_worn_feature_offset;
        }
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:215
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb__arg = null, object movement_flags = null)
    {
        obj_item_bodypart_head limb = limb__arg as obj_item_bodypart_head;
        if (worn_mask_offset != null)
        {
            QDEL_NULL(ref worn_mask_offset);
            limb.worn_mask_offset = null;
        }
        await base.on_bodypart_remove(limb__arg, movement_flags);
    }
}

// tg: code/modules/surgery/organs/external/_visual_organs.dm:221 (gaps P3b: the lizard infusion)
public partial class datum_bodypart_overlay_mutant_snout : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:227
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDESNOUT)) ? TRUE : FALSE;
    }
}

///A moth's antennae
// tg: code/modules/surgery/organs/external/_visual_organs.dm:231 (gaps P3b: the mothroach infusion)
public partial class obj_item_organ_antennae : obj_item_organ
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:251
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);

        RegisterSignal(organ_owner, COMSIG_HUMAN_BURNING, nameof(try_burn_antennae));
        RegisterSignal(organ_owner, COMSIG_LIVING_POST_FULLY_HEAL, nameof(heal_antennae));
        return dot;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:257
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);

        UnregisterSignal(organ_owner as datum, Dm.list(COMSIG_HUMAN_BURNING, COMSIG_LIVING_POST_FULLY_HEAL));
    }

    ///check if our antennae can burn off ;_;
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:263
    [SignalHandler]
    public virtual void try_burn_antennae(mob_living_carbon_human human = null)
    {
        if (!Dm.truthy(burnt) && Dm.num(human.bodytemperature) >= 800 && Dm.num(human.fire_stacks) > 0) //do not go into the extremely hot light. you will not survive
        {
            to_chat(human, span_danger(I18n.T("Your precious antennae burn to a crisp!")));

            burn_antennae();
            Waiting.NoSleep(human.update_body_parts());
        }
    }

    ///Burn our antennae off ;_;
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:273
    public virtual void burn_antennae()
    {
        var antennae = bodypart_overlay as datum_bodypart_overlay_mutant_antennae;
        antennae.burnt = TRUE;
        burnt = TRUE;
    }

    ///heal our antennae back up!!
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:279
    [SignalHandler]
    public virtual void heal_antennae(datum source = null, object heal_flags = null)
    {
        if (!Dm.truthy(burnt))
            return;

        if (Dm.truthy(Dm.bitand(heal_flags, Dm.bitor(HEAL_LIMBS, HEAL_ORGANS))))
        {
            var antennae = bodypart_overlay as datum_bodypart_overlay_mutant_antennae;
            antennae.burnt = FALSE;
            burnt = FALSE;
        }
    }
}

///Moth antennae datum, with full burning functionality
// tg: code/modules/surgery/organs/external/_visual_organs.dm:291
public partial class datum_bodypart_overlay_mutant_antennae : datum_bodypart_overlay_mutant
{
    ///Accessory datum of the burn sprite
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:300  var/datum/sprite_accessory/burn_datum: object (rules 3.2, a
    // typed var that holds wider values): the type sets a typepath, New (:304) stores the singleton fetched from it
    public object burn_datum = typeof(datum_sprite_accessory_moth_antennae_burnt_off);

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:296
    public datum_bodypart_overlay_mutant_antennae()
    {
        feature_key = FEATURE_MOTH_ANTENNAE;
        dyable = TRUE;
        offset_location = UPPER_BODY;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:292  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        layers = Dm.list(Dm.kv(EXTERNAL_FRONT, BODY_FRONT_LAYER), Dm.kv(EXTERNAL_BEHIND, BODY_BEHIND_LAYER));
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:304
    [DmParams("attached_organ")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        burn_datum = fetch_sprite_datum(burn_datum); //turn the path into the singleton instance
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:309
    public override object get_base_icon_state()
    {
        return Dm.truthy(burnt) ? ((datum_sprite_accessory)burn_datum).icon_state : sprite_datum.icon_state;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:312
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEANTENNAE)) ? TRUE : FALSE;
    }
}

///Podperson bodypart overlay, with special coloring functionality to render the flowers in the inverse color
// tg: code/modules/surgery/organs/external/_visual_organs.dm:334 (gaps P3b: the plant infusion)
public partial class datum_bodypart_overlay_mutant_pod_hair : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/_visual_organs.dm:348
    public override async DmTask color_image(image overlay = null, obj_item_bodypart limb = null, object layer_index = null)
    {
        if (!Dm.eq(layer_index, color_swapped_layer))
        {
            await base.color_image(overlay, limb, layer_index);
            return;
        }

        object color_to_use = Dm.truthy(dye_color) ? dye_color : draw_color;
        if (Dm.truthy(color_to_use)) // can someone explain to me why draw_color is allowed to EVER BE AN EMPTY STRING
        {
            DmList rgb_list = Dm.rgb2num(Dm.str(color_to_use));
            overlay.color = rgb(color_inverse_base - Dm.num(rgb_list[1]), color_inverse_base - Dm.num(rgb_list[2]), color_inverse_base - Dm.num(rgb_list[3])); //inversa da color
        }
        else
            overlay.color = null;
    }

    // tg: code/modules/surgery/organs/external/_visual_organs.dm:359
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEHAIR)) ? TRUE : FALSE;
    }
}
