// Translated from tgstation (snapshot 1d99bd7f): code/datums/bodypart_overlays/emote_bodypart_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice B: the whole file (the blush, the tears and the tongue of the emotes).

///Variant of bodypart_overlay for displaying emote overlays. See [/datum/emote/living/blush/run_emote] for an example on how to use one of these.
// tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:2
public partial class datum_bodypart_overlay_simple_emote : datum_bodypart_overlay_simple
{
    // tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:16
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        var image = base.get_image(limb, layer_index, layer_real) as image;
        feature_offset?.apply_offset(image);
        return image;
    }

    // tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:21
    public override async DmTask added_to_limb(obj_item_bodypart limb = null)
    {
        attached_bodypart = WEAKREF(limb) as datum_weakref;
        if (Dm.@in(offset_key, limb.feature_offsets))
            feature_offset = limb.feature_offsets[offset_key] as datum_worn_feature_offset;
    }

    // tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:26
    public override async DmTask removed_from_limb(obj_item_bodypart limb = null)
    {
        attached_bodypart = null;
    }

    ///Removes the overlay from the attached bodypart and updates the necessary sprites
    // tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:30
    public override object Destroy(float? force = null)
    {
        var referenced_bodypart = attached_bodypart?.resolve() as obj_item_bodypart;
        if (referenced_bodypart != null)
            Waiting.NoSleep(referenced_bodypart.remove_bodypart_overlay(this));
        return base.Destroy(force);
    }
}

public partial class mob_living_carbon_human : mob_living_carbon
{
    /**
     * Creates a new emote bodypart overlay and applies it to the human. The overlay can be removed by simply deleting the returned overlay.
     *
     * * Arguments:
     * * overlay_typepath - Typepath to the overlay that should be applied. Should be a subtype of datum/bodypart_overlay/simple/emote.
     *
     * Returns the given overlay, which can be deleted to stop displaying it. Will return null if no bodypart matching the overlay's attached_body_zone field can be found.
     */
    // tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:45
    public virtual async DmTask<object> give_emote_overlay(Type overlay_typepath = null) // a typepath (rules 3.4)
    {
        var bodypart = get_bodypart(Dm.initial(overlay_typepath, (datum_bodypart_overlay_simple_emote p) => p.attached_body_zone)) as obj_item_bodypart;
        return bodypart == null ? null : await bodypart.add_bodypart_overlay(overlay_typepath);
    }
}

// tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:57
public partial class datum_bodypart_overlay_simple_emote_blush : datum_bodypart_overlay_simple_emote
{
    // tg: code/datums/bodypart_overlays/emote_bodypart_overlay.dm:63
    public override async DmTask color_image(image overlay = null, obj_item_bodypart limb = null, object layer_index = null)
    {
        object base_color = (limb.owner?.get_bloodtype() as datum_blood_type) is { } blood_type ? await blood_type.get_damage_color(limb.owner) : null;
        if (!Dm.truthy(base_color))
        {
            await base.color_image(overlay, limb, layer_index);
            return;
        }

        DmList blood_hsl = rgb2num(Dm.str(base_color), COLORSPACE_HSL);
        //  take blood color then just make it a lot brighter and desaturate it a bit
        blood_hsl[2] = Dm.max(0, Dm.num(blood_hsl[2]) - 20);
        blood_hsl[3] = Dm.min(100, Dm.num(blood_hsl[3]) + 30);

        overlay.color = rgb(Dm.num(blood_hsl[1]), Dm.num(blood_hsl[2]), Dm.num(blood_hsl[3]), space: COLORSPACE_HSL);
        overlay.alpha = 200;
    }
}
