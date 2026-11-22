// Translated from tgstation (snapshot 1d99bd7f): code/datums/bodypart_overlays/bodypart_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice B (the emotes' overlays: the blush, the tears, the tongue): the whole file. The subtypes of other species (mutant parts,
// markings, textures) come with those species. DEVIATION(DV12): the overlay's emissive blocker is not created (emissives are not
// ported).

///Bodypart ovarlay datum. These can be added to any limb to give them a proper overlay, that'll even stay if the limb gets removed
///This is the abstract parent, don't use it!!
// tg: code/datums/bodypart_overlays/bodypart_overlay.dm:3
[DmPath("/datum/bodypart_overlay")]
public partial class datum_bodypart_overlay : datum
{
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:28
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_layers(layers);
    }

    /// Used for adding a layer at runtime
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:33
    public virtual void set_layer(object layer_postfix = null, object layer_number = null)
    {
        DmList existing_layers = LAZYCOPY(layers);
        existing_layers[layer_postfix] = layer_number;
        layers = string_assoc_list(existing_layers) as DmList;
    }

    /// Used for adding a layer at runtime without replacing existing layers
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:39
    public virtual void add_layer(object layer_postfix = null, object layer_number = null)
    {
        if (LAZYACCESS(layers, layer_postfix) == null)
            set_layer(layer_postfix, layer_number);
    }

    /// Used for adding layers at runtime
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:44
    public virtual void set_layers(DmList layer_list = null)
    {
        if (!Dm.truthy(length(layer_list)))
            return;
        layers = string_assoc_list(layer_list) as DmList;
    }

    /// Used for removing layers at runtime
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:51
    public virtual void clear_layer(object layer_postfix = null)
    {
        DmList existing_layers = layers.Copy();
        Dm.remove(ref existing_layers, layer_postfix);
        if (!Dm.truthy(length(existing_layers)))
        {
            layers = null;
            return;
        }
        layers = string_assoc_list(existing_layers) as DmList;
    }

    /**
     * Returns a list of overlays that this overlay datum draws
     *
     * * limb: the limb we're drawing onto
     *
     * Returns a list of images/mutable appearances
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:67
    public virtual async DmTask<object> get_all_overlays(obj_item_bodypart limb = null)
    {
        DmList overlays = Dm.list();
        foreach ((object overlay_postfix, object overlay_layer) in Dm.pairs(layers))
            Dm.append(ref overlays, await get_overlay(limb, overlay_postfix, -Dm.num(overlay_layer)));
        return overlays;
    }

    /**
     * Handles generating an individual overlay or an overlay + an emissive blocker
     *
     * * limb: the limb we're drawing onto, can be null if we're just drawing a general overlay for the mob and not a specific limb
     * * layer_index: the index of the layer we're drawing on, used for generating the image
     * * layer_real: the actual layer we're drawing on, used for generating the image
     *
     * Returns a list of images/mutable appearances, usually just one, but can be two if we need an emissive blocker
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:82
    public virtual async DmTask<object> get_overlay(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        var main_image = get_image(limb, layer_index, layer_real) as image;
        if (Dm.truthy(limb?.is_husked) && draw_on_husks != HUSK_OVERLAY_NORMAL)
            main_image = await huskify_image(main_image, limb) as image;
        else
            await color_image(main_image, limb, layer_index);

        DmList created_overlays = Dm.list();
        created_overlays[main_image] = overlay_flags;
        // tg: :93-95  the emissive blocker overlay (LIMB_OVERLAY_META): DEVIATION(DV12), emissives are not ported
        return created_overlays;
    }

    /// Helper to make an image look like a husk.
    /// Returns the husk-ified image, doesn't modify the original image.
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:101
    public virtual async DmTask<object> huskify_image(image main_image = null, obj_item_bodypart limb = null)
    {
        icon husk_icon = Dm.icon(main_image.icon); // new(main_image.icon): BYOND's new /icon(file) is icon(file)
        husk_icon.ColorTone(HUSK_COLOR_TONE);
        main_image.icon = husk_icon;
        main_image.color = limb.draw_color;
        return main_image;
    }

    /**
     * The actual proc where images / mutable appearances are generated for get_overlay
     * Must be implemented by subtypes, at its simplest it should just return an image
     *
     * * limb: the limb we're drawing onto, can be null if we're just drawing a general overlay for the mob and not a specific limb
     * * layer_index: the index of the layer we're drawing on.
     * Typically some postfix for an icon state ie. "front" or "behind" (Used like "overlay_front" or "overlay_behind"
     * * layer_real: the actual layer we're drawing on, used for generating the image.
     * Some (negative) number like -10 to -30. (Used like layer = layer_real, you shouldn't need to mess with it.)
     *
     * Returns an image/mutable appearance to draw on the mob.
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:121
    public virtual object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        throw Dm.CRASH("Get image needs to be overridden", "code/datums/bodypart_overlays/bodypart_overlay.dm", 122);
    }

    /**
     * Used to color the image, if necessary.
     * By default it does nothing.
     *
     * * image: the image generated by get_image() that we want to color
     * * limb: the limb we're drawing onto, can be null if we're just drawing a general overlay for the mob and not a specific limb.
     * * layer_index: the index of the layer we're drawing on, used for generating the image.
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:132
    public virtual async DmTask color_image(image overlay = null, obj_item_bodypart limb = null, object layer_index = null)
    {
    }

    /// Called on being added to a limb.
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:136
    public virtual async DmTask added_to_limb(obj_item_bodypart limb = null)
    {
    }

    /// Called on being removed from a limb.
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:140
    public virtual async DmTask removed_from_limb(obj_item_bodypart limb = null)
    {
    }

    /**
     * Used by subtypes to change the appearance of the overlay.
     * Typically implemented in tandem with a sprite accessory.
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:147
    public virtual void set_appearance(params object[] __extra)
    {
        throw Dm.CRASH("Update appearance needs to be overridden", "code/datums/bodypart_overlays/bodypart_overlay.dm", 148);
    }

    /**
     * Checks whether the bodypart overlay can be drawn on the mob/bodypart.
     * Used so snouts are hidden by masks and whatnot.
     *
     * * bodypart_owner: the bodypart we're trying to draw on, will never be null.
     * * owner: the mob we're trying to draw on. Likely bodypart_owner.owner, can be null.
     *
     * Returns TRUE if we can draw
     * Returns FALSE if we can't draw
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:160
    [ShouldCallParent]
    public virtual object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.num(!Dm.truthy(bodypart_owner.is_husked) || draw_on_husks != HUSK_OVERLAY_NONE);
    }

    /**
     * Used in human rendering to cache generated bodyparts.
     * Anything your bodypart overlay can change - such as color, icon state, etc - should be included in this,
     * otherwise you'll end up with caching issues where different looking overlays will share the same cache and thus look the same.
     *
     * * limb: the limb we're drawing onto, can be null if we're just drawing a general overlay for the mob and not a specific limb.
     *
     * Returns a list of strings/numbers/whatever that represents the unique appearance of the overlay.
     */
    // tg: code/datums/bodypart_overlays/bodypart_overlay.dm:173
    public virtual object icon_render_key(obj_item_bodypart limb = null)
    {
        return Dm.list();
    }
}
