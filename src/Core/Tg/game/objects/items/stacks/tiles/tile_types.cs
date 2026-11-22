// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tiles/tile_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the base /obj/item/stack/tile translated (M5 slice 2: the floor tile stacks); the other tiles of the file
// (grass, wood, carpets, light tiles, fake space, ...) come with their floors (their types stay inert). The var blocks are
// generated (D111).

/**
 * TILE STACKS
 *
 * Allows us to place a turf on a plating.
 */
// tg: code/game/objects/items/stacks/tiles/tile_types.dm:6
public partial class obj_item_stack_tile : obj_item_stack
{
    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:33
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        float? merge = Dm.arg<float?>(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        float? mat_amt = Dm.arg<float?>(__extra, 3);
        merge ??= TRUE;
        mat_amt ??= 1;
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt));
        pixel_x = rand(-3, 3);
        pixel_y = rand(-3, 3); //randomize a little
        Waiting.NoSleep(AddElement(typeof(datum_element_openspace_item_click_handler)));
        if (Dm.truthy(tile_reskin_types))
            tile_reskin_types = tile_reskin_list(tile_reskin_types) as DmList;
        if (Dm.truthy(tile_rotate_dirs))
        {
            tile_rotate_dirs_number = tile_rotate_dirs.Copy();
            DmList values = Dm.list();
            foreach (object set_dir in Dm.each(tile_rotate_dirs))
                Dm.append(ref values, dir2text(set_dir));
            tile_rotate_dirs = tile_dir_list(values, turf_type) as DmList;
        }
        return dot;
    }


    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:48
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(tile_reskin_types) || Dm.truthy(tile_rotate_dirs))
            Dm.append(ref dot, span_notice(I18n.T("Use while in your hand to change what type of [] you want.", this)));
        if (Dm.truthy(throwforce) && !Dm.truthy(is_cyborg)) //do not want to divide by zero or show the message to borgs who can't throw
        {
            string damage_value = null;
            float sw__1 = ceil(MAX_LIVING_HEALTH / throwforce); //throws to crit a human
            if (Dm.in_to(sw__1, 1, 3))
                damage_value = I18n.T("superb");
            else if (Dm.in_to(sw__1, 4, 6))
                damage_value = I18n.T("great");
            else if (Dm.in_to(sw__1, 7, 9))
                damage_value = I18n.T("good");
            else if (Dm.in_to(sw__1, 10, 12))
                damage_value = I18n.T("fairly decent");
            else if (Dm.in_to(sw__1, 13, 15))
                damage_value = I18n.T("mediocre");
            if (!Dm.truthy(damage_value))
                return dot;
            Dm.append(ref dot, span_notice(I18n.T("Those could work as a [] throwing weapon.", damage_value)));
        }
        return dot;
    }

    /**
     * Place our tile on a plating, or replace it.
     *
     * Arguments:
     * * target_plating - Instance of the plating we want to place on. Replaced during successful executions.
     * * user - The mob doing the placing.
     */
    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:76
    public virtual async DmTask<object> place_tile(turf_open_floor_plating target_plating = null, mob user = null)
    {
        Type placed_turf_path = turf_type;
        if (!ispath(placed_turf_path))
            return null;
        if (!istype(target_plating, typeof(turf_open_floor_plating)))
            return null;

        if (!Dm.truthy(await use(1)))
            return null;
        turf placed = await target_plating.place_on_top(placed_turf_path, flags: CHANGETURF_INHERIT_AIR) as turf;
        await placed.setDir(turf_dir);
        playsound(placed, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
        return placed;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:90
    public override async DmTask handle_openspace_click(turf target = null, mob user = null, DmList modifiers = null)
    {
        // target.base_item_interaction(user, src, list2params(modifiers)): tg passes the modifiers as params text where the
        // proc declares a list; the typed parameter reads text as null (rules 3.5).
        await target.base_item_interaction(user as mob_living, this, null);
    }
}

// Gaps SK1 (D333): the neon carpet's glow (its neon overlay; the emissive half is DV12), the material tile's materials carried onto
// the floor it places, and the emissive test tile.
// tg: code/game/objects/items/stacks/tiles/tile_types.dm:470
public partial class obj_item_stack_tile_carpet_neon : obj_item_stack_tile_carpet
{
    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:492
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        var neon_overlay = mutable_appearance(Dm.truthy(neon_icon) ? neon_icon : icon, (string)(Dm.truthy(neon_icon_state) ? neon_icon_state : icon_state), alpha: alpha) as mutable_appearance;
        neon_overlay.color = neon_color;
        Dm.append(ref dot, neon_overlay);
        // DEVIATION(DV12): . += emissive_appearance(neon_icon || icon, neon_icon_state || icon_state, src, alpha = emissive_alpha)
        return dot;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:499
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        if (!Dm.truthy(isinhands) || !Dm.truthy(neon_inhand_icon_state))
            return dot;

        var neon_overlay = mutable_appearance(icon_file, (string)neon_inhand_icon_state) as mutable_appearance;
        neon_overlay.color = neon_color;
        Dm.append(ref dot, neon_overlay);
        // DEVIATION(DV12): . += emissive_appearance(icon_file, neon_inhand_icon_state, src, alpha = emissive_alpha)
        return dot;
    }
}

// tg: code/game/objects/items/stacks/tiles/tile_types.dm:1227
public partial class obj_item_stack_tile_material : obj_item_stack_tile
{
    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:1238
    public override async DmTask<object> place_tile(turf_open_floor_plating target_plating = null, mob user = null)
    {
        // Save refernce to the materials for the case when we place last tile in the stack
        DmList saved_mats_per_unit = mats_per_unit;
        object dot = await base.place_tile(target_plating, user);
        var floor = dot as turf_open_floor_material;
        if (floor != null)
            await floor.set_custom_materials(saved_mats_per_unit);
        return dot;
    }
}

/// Floor tiles used to test emissive turfs.
// tg: code/game/objects/items/stacks/tiles/tile_types.dm:1326
public partial class obj_item_stack_tile_emissive_test : obj_item_stack_tile
{
    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:1333
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, icon_state, src, alpha = alpha)
        return dot;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_types.dm:1337
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): . += emissive_appearance(standing.icon, standing.icon_state, src, alpha = standing.alpha)
        return dot;
    }
}
