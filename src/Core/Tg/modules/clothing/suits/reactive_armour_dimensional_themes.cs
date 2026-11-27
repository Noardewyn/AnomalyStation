// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/reactive_armour_dimensional_themes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the barricade armour's dimensional themes. A theme turns the floors and walls around a tile into its own (with its
// material), puffs smoke and places two to four of its barriers on the free tiles; the safe themes (wood, snow, space, glass, the
// secure plastitanium and meat) and the dangerous ones (the clown's bear traps, uranium, plasma, ice and the lava of the lavaland
// theme). The var blocks are generated (D111).

/***
 * Datum describing a 'theme' which transforms a 3x3 area's turfs when applied.
 * It also creates a number of themed barriers in that area.
 */
// tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:8
public partial class datum_armour_dimensional_theme : datum
{
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:1  #define MAX_BARRIERS 4 (file-local)
    private const float MAX_BARRIERS = 4;
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:2  #define MIN_BARRIERS 2 (file-local)
    private const float MIN_BARRIERS = 2;

    /**
     * Applies a random dimensional theme effect centered around a provided point.
     * This will transform floors and walls, as well as creating some themed barriers in that area.
     *
     * Arguments
     * * source - The central turf to apply outwards from.
     * * dangerous - If true this will pick a 'dangerous' theme.
     */
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:23
    public virtual async DmTask apply_random(turf source = null, float? dangerous = null)
    {
        dangerous ??= FALSE;
        Type theme_type;
        if (Dm.truthy(dangerous))
            theme_type = pick(subtypesof(typeof(datum_armour_dimensional_theme_dangerous))) as Type;
        else
            theme_type = pick(subtypesof(typeof(datum_armour_dimensional_theme_safe))) as Type;
        datum_armour_dimensional_theme theme = Dm.@new(theme_type) as datum_armour_dimensional_theme;
        await theme.apply(source);
        qdel(theme);
    }

    /**
     * Transforms turfs around a source point into a new themed material.
     * Also place some barriers in unoccupied areas.
     *
     * Arguments
     * * source - The central turf to apply outwards from.
    */
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:40
    public virtual async DmTask apply(turf source = null)
    {
        obj_effect_particle_effect_fluid_smoke poof = Dm.@new<obj_effect_particle_effect_fluid_smoke>(source);
        poof.lifetime = 2 * SECONDS;
        DmList target_area = get_target_area(source) as DmList;
        foreach (turf target in Dm.each<turf>(target_area))
            await convert_turf(target);
        await place_barriers(source, target_area);
    }

    /**
     * Returns a list of turfs which it is valid to apply changes to.
     *
     * Arguments
     * * source - The central turf to check outwards from.
     */
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:54
    public virtual object get_target_area(turf source = null)
    {
        DmList target_area = RANGE_TURFS(1, source);
        foreach (turf check_turf in Dm.each<turf>(target_area))
        {
            if (isspaceturf(check_turf))
            {
                Dm.remove(ref target_area, check_turf);
                continue;
            }
            if (isindestructiblefloor(check_turf))
            {
                Dm.remove(ref target_area, check_turf);
                continue;
            }
            if (isindestructiblewall(check_turf))
            {
                Dm.remove(ref target_area, check_turf);
                continue;
            }
            if (Dm.truthy(check_turf.holodeck_compatible))
            {
                Dm.remove(ref target_area, check_turf);
                continue;
            }
        }

        return target_area;
    }

    /**
     * Changes a turf into a different turf
     *
     * Arguments
     * * to_convert - Turf to convert.
     */
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:78
    public virtual async DmTask convert_turf(turf to_convert = null)
    {
        if (isfloorturf(to_convert))
        {
            turf_open open_turf = to_convert as turf_open;
            await open_turf.replace_floor(replace_floor, flags: Dm.bitor(CHANGETURF_INHERIT_AIR, CHANGETURF_INHERIT_MOUNTS));
        }
        else if (iswallturf(to_convert))
            await to_convert.ChangeTurf(replace_wall, flags: CHANGETURF_INHERIT_MOUNTS);

        if (material != null)
        {
            DmList custom_materials = Dm.list(Dm.kv(await SSmaterials.get_material(material), SHEET_MATERIAL_AMOUNT));
            await to_convert.set_custom_materials(custom_materials);
        }
    }

    /**
     * Places a random amount of themed barriers in unoccupied spaces adjacent to a source tile.
     *
     * Arguments
     * * source - Central tile to place barriers around.
     * * target_area - Tiles which have had their materials converted, needs further culling to remove dense tiles.
     */
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:96
    public virtual async DmTask place_barriers(turf source = null, DmList target_area = null)
    {
        Dm.remove(ref target_area, source);
        foreach (turf check_turf in Dm.each<turf>(target_area))
        {
            if (!Dm.truthy(await check_turf.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            Dm.remove(ref target_area, check_turf);
        }

        float to_place = rand(MIN_BARRIERS, MAX_BARRIERS);
        DmList custom_materials = Dm.list();
        if (material != null)
            custom_materials = Dm.list(Dm.kv(await SSmaterials.get_material(material), SHEET_MATERIAL_AMOUNT));

        while (Dm.num(target_area.len) > 0 && to_place > 0)
        {
            turf place_turf = pick(target_area) as turf;
            await place_barrier(place_turf, custom_materials);
            Dm.remove(ref target_area, place_turf);
            to_place--;
        }
    }

    /**
     * Places a barrier at a specified turf.
     *
     * Arguments
     * * source - Tile to place barrier upon.
     * * materials - Custom material data to apply to the barrier.
     */
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:121
    public virtual async DmTask place_barrier(turf source = null, DmList materials = null)
    {
        obj placed_barricade = Dm.@new(barricade, source) as obj;
        if (Dm.truthy(barricade_anchored))
            placed_barricade.anchored = TRUE;
        if (Dm.truthy(materials.len))
            await placed_barricade.set_custom_materials(materials);
    }
}

// tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:182
public partial class datum_armour_dimensional_theme_dangerous_lavaland : datum_armour_dimensional_theme_dangerous
{
    /// Replace the barrier spawning to instead create weak lava.
    // tg: code/modules/clothing/suits/reactive_armour_dimensional_themes.dm:187
    public override async DmTask place_barrier(turf source = null, DmList materials = null)
    {
        await source.ChangeTurf(typeof(turf_open_lava_smooth_weak), flags: CHANGETURF_INHERIT_AIR);
    }
}
