// Translated from tgstation (snapshot 1d99bd7f): code/datums/sprite_accessories/hair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice D2 (lobby.md 8): the procs and GLOB.blended_hair_icons_cache; the vars, the 221 hairs, the masks and the gradients are
// generated. The blended icons are recipes the client draws (icon.cs, D104).

// Cache of each hairstyle's icon after being blended with the given masks
// "joined mask types" is each mask's type as a string joined by commas (for no masks, it is the empty string)
// /datum/sprite_accessory/hair path -> list(joined mask types -> icon)
public partial class datum_controller_global_vars
{
    // tg: code/datums/sprite_accessories/hair.dm:27  GLOBAL_LIST_EMPTY(blended_hair_icons_cache)
    public DmList blended_hair_icons_cache;

    // tg: code/datums/sprite_accessories/hair.dm:27  (GLOBAL_MANAGED(blended_hair_icons_cache, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblended_hair_icons_cache()
    {
        blended_hair_icons_cache = Dm.list();
        Dm.append(ref gvars_datum_init_order, "blended_hair_icons_cache");
    }
}

// tg: code/datums/sprite_accessories/hair.dm:29
[DmPath("/datum/sprite_accessory/hair")]
public partial class datum_sprite_accessory_hair : datum_sprite_accessory
{
    /// Retrieve the base hair icon with all hair appendeges blended in, with hair masks applied, from the cache, or generate it if it doesn't exist
    // tg: code/datums/sprite_accessories/hair.dm:42
    public virtual object getCachedIcon(DmList hair_masks = null)
    {
        icon cachedIcon = null;
        string joinedMasks = LAZYLEN(hair_masks) != 0 ? Dm.jointext(hair_masks, ",") : "";
        var masks_to_icons = GLOB.blended_hair_icons_cache[GetType()] as DmList;
        if (!Dm.truthy(masks_to_icons))
            GLOB.blended_hair_icons_cache[GetType()] = Dm.list();
        else
            cachedIcon = masks_to_icons[joinedMasks] as icon;

        if (cachedIcon == null)
        {
            if (LAZYLEN(hair_masks) != 0)
            {
                if (LAZYLEN(hair_appendages_inner) != 0)
                {
                    // Check if there are any hair appendages in a zone that is not strictly masked
                    bool found_mask_dodger = false;
                    foreach (object mask in Dm.each(hair_masks))
                    {
                        foreach (object appendage in Dm.each(hair_appendages_inner))
                        {
                            object zone = hair_appendages_inner[appendage];
                            if (!Dm.truthy(Dm.bitand(zone, Mask(mask).strict_coverage_zones)))
                                found_mask_dodger = true;
                        }
                    }

                    if (found_mask_dodger)
                    {
                        // We have to process each icon individually
                        cachedIcon = Dm.icon(icon, icon_state);
                        // mask the base icon
                        foreach (object mask in Dm.each(hair_masks))
                            BlendMask(cachedIcon, mask);

                        // mask the appendages if required and add them to the base icon
                        foreach (object appendage_icon_state in Dm.each(hair_appendages_inner))
                        {
                            icon appendage_icon = Dm.icon(icon, appendage_icon_state);
                            object zone = hair_appendages_inner[appendage_icon_state];
                            foreach (object mask in Dm.each(hair_masks))
                            {
                                if (Dm.truthy(Dm.bitand(zone, Mask(mask).strict_coverage_zones)))
                                    BlendMask(appendage_icon, mask);
                            }
                            cachedIcon.Blend(appendage_icon, ICON_OVERLAY);
                        }
                    }
                    else
                    {
                        // No mask dodgers, so we can just mask the full (hopefully cached) icon
                        cachedIcon = Dm.icon(getCachedIcon());
                        foreach (object mask in Dm.each(hair_masks))
                            BlendMask(cachedIcon, mask);
                    }
                }
                else
                {
                    // No hair appendages, so just apply all hair masks to the base icon
                    cachedIcon = Dm.icon(icon, icon_state);
                    foreach (object mask in Dm.each(hair_masks))
                        BlendMask(cachedIcon, mask);
                }
            }
            else
            {
                // no hair masks
                cachedIcon = Dm.icon(icon, icon_state);
                if (LAZYLEN(hair_appendages_inner) != 0)
                {
                    foreach (object appendage_icon_state in Dm.each(hair_appendages_inner))
                    {
                        icon appendage_icon = Dm.icon(icon, appendage_icon_state);
                        cachedIcon.Blend(appendage_icon, ICON_OVERLAY);
                    }
                }
            }
            // set cache
            ((DmList)GLOB.blended_hair_icons_cache[GetType()])[joinedMasks] = cachedIcon;
        }
        return cachedIcon;
    }

    // PORT: a carbon's hair_masks hold the hats' hair_mask type paths (inventory.dm:134), which tg reads as datums; the type's
    // instance in SSaccessories.hair_masks_list (sprite_accessories.dm:116) carries the same vars.
    private static datum_hair_mask Mask(object mask) => mask as datum_hair_mask ?? (datum_hair_mask)SSaccessories.hair_masks_list[mask];

    // var/icon/mask_icon = icon('icons/mob/human/hair_masks.dmi', mask.icon_state); mask_icon.Shift(SOUTH, y_offset);
    // cachedIcon.Blend(mask_icon, ICON_ADD)
    // shortcut: the Shift is the mask blended y_offset pixels lower, which leaves the hair's top y_offset rows unmasked where tg's
    // shifted mask is blank; every mask hides the top of the head anyway, so only a tall style under a hat could differ (two
    // styles have a y_offset); give icon a Shift operation if one shows.
    private void BlendMask(icon target, object mask)
    {
        icon mask_icon = Dm.icon(Dm.resource("icons/mob/human/hair_masks.dmi"), Mask(mask).icon_state);
        target.Blend(mask_icon, ICON_ADD, 1, 1 - (int)y_offset);
    }
}
