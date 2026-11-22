// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/visuals_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file. Drone overlays and visuals.

public partial class mob_living_basic_drone : mob_living_basic
{
    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:3
    public virtual object apply_overlay(object cache_index = null)
    {
        object dot = drone_overlays[cache_index];
        if (Dm.truthy(dot))
            add_overlay(dot);
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:8
    public virtual void remove_overlay(object cache_index = null)
    {
        object overlay = drone_overlays[cache_index];
        if (Dm.truthy(overlay))
        {
            cut_overlay(overlay);
            drone_overlays[cache_index] = null;
        }
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:14
    public override async DmTask update_clothing(object slot_flags = null)
    {
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HEAD)))
            await update_worn_head();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_MASK)))
            await update_worn_mask();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HANDS)))
            await update_held_items();
        if (Dm.truthy(Dm.bitand(slot_flags, Dm.bitor(ITEM_SLOT_HANDS, ITEM_SLOT_DEX_STORAGE))))
            update_inv_internal_storage();
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:24
    public virtual void update_inv_internal_storage()
    {
        hud_used?.update_inventory_slot(ITEM_SLOT_DEX_STORAGE);
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:27
    public override async DmTask update_worn_head()
    {
        remove_overlay(DRONE_HEAD_LAYER);
        hud_used?.update_inventory_slot(ITEM_SLOT_HEAD);

        if (head != null)
        {
            object used_head_icon = Dm.resource("icons/mob/clothing/head/utility.dmi");
            if (head is obj_item_clothing_mask)
                used_head_icon = Dm.resource("icons/mob/clothing/mask.dmi");
            var head_overlay = await head.build_worn_icon(default_layer: DRONE_HEAD_LAYER, default_icon_file: used_head_icon) as mutable_appearance;
            head_overlay.pixel_z -= 15;

            drone_overlays[DRONE_HEAD_LAYER] = head_overlay;
        }

        apply_overlay(DRONE_HEAD_LAYER);
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:42
    public override async DmTask update_worn_mask()
    {
        await update_worn_head();
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:45
    public override async DmTask<object> regenerate_icons()
    {
        // Drones only have 4 slots, which in this specific instance
        // is a small blessing.
        await update_held_items();
        await update_worn_head();
        update_inv_internal_storage();
        return null;
    }

    /**
     * Prompt for user to pick [/mob/living/basic/drone/var/visualAppearance]
     *
     * Does nothing if there is no user
     *
     * Called on [/mob/proc/Login]
     */
    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:60
    public virtual async DmTask pickVisualAppearance()
    {
        picked = FALSE;
        object drone_icon = Dm.resource("icons/mob/silicon/drone.dmi");
        DmList drone_icons = Dm.list(
            Dm.kv("Maintenance Drone", image(icon: drone_icon, icon_state: Dm.text("[]_grey", MAINTDRONE))),
            Dm.kv("Repair Drone", image(icon: drone_icon, icon_state: REPAIRDRONE)),
            Dm.kv("Scout Drone", image(icon: drone_icon, icon_state: SCOUTDRONE)));
        object picked_icon = await show_radial_menu(this, this, drone_icons, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu)), radius: 38, require_near: TRUE);
        switch (Dm.str(picked_icon))
        {
            case "Maintenance Drone":
            {
                visualAppearance = MAINTDRONE;
                DmList drone_colors = Dm.list(
                    Dm.kv("blue", image(icon: drone_icon, icon_state: Dm.text("[]_blue", visualAppearance))),
                    Dm.kv("green", image(icon: drone_icon, icon_state: Dm.text("[]_green", visualAppearance))),
                    Dm.kv("grey", image(icon: drone_icon, icon_state: Dm.text("[]_grey", visualAppearance))),
                    Dm.kv("orange", image(icon: drone_icon, icon_state: Dm.text("[]_orange", visualAppearance))),
                    Dm.kv("pink", image(icon: drone_icon, icon_state: Dm.text("[]_pink", visualAppearance))),
                    Dm.kv("red", image(icon: drone_icon, icon_state: Dm.text("[]_red", visualAppearance))));
                object picked_color = await show_radial_menu(this, this, drone_colors, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu)), radius: 38, require_near: TRUE);
                if (Dm.truthy(picked_color))
                {
                    icon_state = Dm.text("[]_[]", visualAppearance, picked_color);
                    icon_living = Dm.text("[]_[]", visualAppearance, picked_color);
                }
                else
                {
                    icon_state = Dm.text("[]_grey", visualAppearance);
                    icon_living = Dm.text("[]_grey", visualAppearance);
                }
                break;
            }
            case "Repair Drone":
                visualAppearance = REPAIRDRONE;
                icon_state = visualAppearance;
                icon_living = visualAppearance;
                break;
            case "Scout Drone":
                visualAppearance = SCOUTDRONE;
                icon_state = visualAppearance;
                icon_living = visualAppearance;
                break;
            default:
                visualAppearance = MAINTDRONE;
                icon_state = Dm.text("[]_grey", visualAppearance);
                icon_living = Dm.text("[]_grey", visualAppearance);
                break;
        }
        icon_dead = Dm.text("[]_dead", visualAppearance);
        picked = TRUE;
    }

    /**
     * check_menu: Checks if we are allowed to interact with a radial menu
     */
    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:100
    public virtual object check_menu()
    {
        if (Dm.truthy(incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/drone/visuals_icons.dm:107
    public virtual object getItemPixelShiftY()
    {
        switch (visualAppearance)
        {
            case MAINTDRONE:
                return 0;
            case REPAIRDRONE:
            case SCOUTDRONE:
            case CLOCKDRONE:
                return -6;
        }
        return null;
    }
}
