// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/cerulean_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2c (D349): the whole file: clothing worn by a cerulean has no legs (the masks) or its own sprites (the suits). The cerulean's
// modsuit icon (handle_cerulean_modsuit) is the modsuits' own proc and stays as it is.

// File of procs for human_update_icons.dm specifically to render cerulean clothing appropriately. so it doesn't get any more lines than it already has...
public partial class obj_item : obj
{
    /**
     *	Modifies the sprite of clothing to have no legs! For pants, which mer folk canonically can't wear.
     *	What we generate will be saved in a cache, how nice!
     */
    // tg: code/modules/mob/living/carbon/human/cerulean_update_icons.dm:7
    public virtual object generate_cerulean_icons(icon base_icon = null, object key = null, object greyscale_colors = null, object bodyshape = null)
    {
        DmList cerulean_icon_cache = TgState.Current.obj_item__generate_cerulean_icons__cerulean_icon_cache; // var/static/list/cerulean_icon_cache
        mob_living_carbon_human wearer = loc as mob_living_carbon_human;
        string physique = wearer?.physique == FEMALE ? "f" : null;
        object index = Dm.text("[][]-[]-[]", key, physique != null ? Dm.text("-[]", physique) : "", type, greyscale_colors);
        icon cerulean_clothing_icon = cerulean_icon_cache[index] as icon;

        if (cerulean_clothing_icon != null)
            return Dm.icon(cerulean_clothing_icon);

        if (greyscale_colors == null || Dm.num(length(SSgreyscale.ParseColorString(greyscale_colors))) > 1)
            greyscale_colors = get_general_color(base_icon);

        // if we are generating for modsuits, we need to run through a bespoke proc!
        obj_item_clothing_suit_mod modsuit_item = this as obj_item_clothing_suit_mod;
        if (modsuit_item != null)
            cerulean_clothing_icon = modsuit_item.handle_cerulean_modsuit(base_icon, key, greyscale_colors, physique) as icon;
        // go to work
        else if (Dm.truthy(Dm.bitand(bodyshapes_with_variations, BODYSHAPE_CERULEAN)))
        {
            // if we have to mask
            if (Dm.truthy(Dm.bitand(supports_variations_flags, CERULEAN_MASKING)))
            {
                // we are just cutting the pant
                if (Dm.truthy(Dm.bitand(supports_variations_flags, CLOTHING_CERULEAN_MASK_LEGS)))
                    cerulean_clothing_icon = apply_icon_mask(base_icon, LEGS_MASK) as icon;
                // remove any pixels that typically appear between the legs
                if (Dm.truthy(Dm.bitand(supports_variations_flags, CLOTHING_CERULEAN_MASK_INBETWEEN)))
                    cerulean_clothing_icon = apply_icon_mask(base_icon, BACK_COAT_MASK) as icon;
            }

            // no masks, we have custom sprites
            else
            {
                // suits
                obj_item_clothing_suit suit_item = this as obj_item_clothing_suit;
                if (suit_item != null && Dm.truthy(icon_exists(Dm.resource("icons/mob/human/species/misc/cerulean_suit.dmi"), icon_state)))
                {
                    if (greyscale_config_worn != null)
                    {
                        cerulean_clothing_icon = Dm.icon(
                            SSgreyscale.GetColoredIconByType(
                                typeof(datum_greyscale_config_suit_worn_cerulean),
                                greyscale_colors),
                            icon_state);
                    }
                    else
                        cerulean_clothing_icon = Dm.icon(Dm.resource("icons/mob/human/species/misc/cerulean_suit.dmi"), icon_state);
                    // flippy flippers
                    if (physique == "f")
                    {
                        object flipper_color = greyscale_colors;
                        if (!Dm.eq(suit_item.cerulean_flipper_palette, FLIPPERS))
                            flipper_color = suit_item.cerulean_flipper_palette;
                        if (!Dm.eq(flipper_color, NO_FLIPPERS))
                            generate_fem_flippers(cerulean_clothing_icon, flipper_color);
                    }
                }
            }
        }

        //not gen'ing is ok
        if (cerulean_clothing_icon == null)
            cerulean_clothing_icon = base_icon;
        //return gen'd icon
        cerulean_icon_cache[index] = fcopy_rsc(cerulean_clothing_icon);
        return Dm.icon(cerulean_clothing_icon);
    }

    /// apply a flipper icon for female physique Ceruleans, who have extra fins to keep their eggs close
    /// ideally we color after the theme fetched from var/cerulean_flipper_palette
    // tg: code/modules/mob/living/carbon/human/cerulean_update_icons.dm:63
    public virtual void generate_fem_flippers(icon clothing_icon = null, object set_color = null)
    {
        clothing_icon.Blend(
            Dm.icon(
                SSgreyscale.GetColoredIconByType(
                    typeof(datum_greyscale_config_modular_mod_parts_cerulean_basic),
                    set_color),
                Dm.text("[]", FLIPPERS)),
            ICON_OVERLAY);
    }
}

// tg: code/modules/mob/living/carbon/human/cerulean_update_icons.dm:8 (gaps SP2c: the proc-level static cache, rules 10.6)
public sealed partial class TgState
{
    internal DmList obj_item__generate_cerulean_icons__cerulean_icon_cache = Dm.list();
}
