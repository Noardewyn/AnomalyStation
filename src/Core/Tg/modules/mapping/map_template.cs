// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/map_template.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D272): the whole file, the templates the shuttles load from (map files read through the file seam: the host mounts
// the converted templates at tg's paths, MapSeam). load_new_z is the away missions' (D108).

// tg: code/modules/mapping/map_template.dm:1
[DmPath("/datum/map_template")]
public partial class datum_map_template : datum
{
    // tg: code/modules/mapping/map_template.dm:31
    [DmParams("path", "rename", "cache")]
    public override async DmTask New(params object[] __extra)
    {
        object path = Dm.arg(__extra, 0);
        object rename = Dm.arg(__extra, 1);
        object cache = Dm.arg(__extra, 2) ?? FALSE;
        await base.New();
        if (path != null)
            mappath = path;
        if (mappath != null)
            await preload_size(mappath, cache);
        if (rename != null)
            name = Dm.str(rename);
        ceiling_baseturfs.Insert(1, typeof(turf_baseturf_bottom));
    }

    // tg: code/modules/mapping/map_template.dm:42
    public virtual async DmTask<object> preload_size(object path = null, object cache = null)
    {
        cache ??= FALSE;
        var parsed = (datum_parsed_map)await Dm.new_async(typeof(datum_parsed_map), Dm.file(Dm.str(path)));
        DmList bounds = parsed?.bounds;
        if (bounds != null)
        {
            width = Dm.num(bounds[MAP_MAXX]); // Assumes all templates are rectangular, have a single Z level, and begin at 1,1,1
            height = Dm.num(bounds[MAP_MAXY]);
            if (Dm.truthy(cache))
                cached_map = parsed;
        }
        return bounds;
    }

    // tg: code/modules/mapping/map_template.dm:52
    public virtual async DmTask initTemplateBounds(DmList bounds = null)
    {
        if (bounds == null) //something went wrong
        {
            stack_trace(Dm.text("[] template failed to initialize correctly!", name), "code/modules/mapping/map_template.dm", 54);
            return;
        }

        DmList atmos_machines = Dm.list();
        DmList cables = Dm.list();
        DmList movables = Dm.list();
        DmList ports = Dm.list();
        DmList areas = Dm.list();

        DmList turfs = block(
            Dm.num(bounds[MAP_MINX]), Dm.num(bounds[MAP_MINY]), Dm.num(bounds[MAP_MINZ]),
            Dm.num(bounds[MAP_MAXX]), Dm.num(bounds[MAP_MAXY]), Dm.num(bounds[MAP_MAXZ]));
        foreach (turf current_turf in Dm.each<turf>(turfs))
        {
            var current_turfs_area = (area)current_turf.loc;
            Dm.combine(ref areas, current_turfs_area);
            if (!Dm.truthy(SSatoms.initialized))
                continue;

            foreach (atom_movable movable_in_turf in Dm.each<atom_movable>(current_turf))
            {
                if (movable_in_turf is obj_docking_port_mobile)
                    continue; // mobile docking ports need to be initialized after their template has finished loading, to ensure that their bounds are setup
                Dm.append(ref movables, movable_in_turf);
                if (movable_in_turf is obj_structure_cable)
                {
                    Dm.append(ref cables, movable_in_turf);
                    continue;
                }
                if (movable_in_turf is obj_machinery_atmospherics)
                    Dm.append(ref atmos_machines, movable_in_turf);
                if (movable_in_turf is obj_docking_port_stationary)
                    Dm.append(ref ports, movable_in_turf);
            }
        }

        // Not sure if there is some importance here to make sure the area is in z
        // first or not.  Its defined In Initialize yet its run first in templates
        // BEFORE so... hummm
        SSmapping.reg_in_areas_in_z(areas);
        if (!Dm.truthy(SSatoms.initialized))
            return;

        DmList to_initialize = Dm.add(Dm.add(areas, turfs), movables) as DmList;
        SSatoms.InitializeAtoms(to_initialize, Dm.truthy(returns_created_atoms) ? created_atoms : null);
        SSlighting.setup_static_lighting_if_needed(turfs);

        // NOTE, now that Initialize and LateInitialize run correctly, do we really
        // need these two below?
        SSmachines.setup_template_powernets(cables);
        SSair.setup_template_machinery(atmos_machines);
        foreach (turf loaded in Dm.each<turf>(turfs))
            CeilingSystem.Of(World.Current).TemplateLoaded((turf)locate(loaded.x, loaded.y, loaded.z)); // DEVIATION(DV183)

        //calculate all turfs inside the border
        DmList template_and_bordering_turfs = block(
            Dm.num(bounds[MAP_MINX]) - 1, Dm.num(bounds[MAP_MINY]) - 1, Dm.num(bounds[MAP_MINZ]),
            Dm.num(bounds[MAP_MAXX]) + 1, Dm.num(bounds[MAP_MAXY]) + 1, Dm.num(bounds[MAP_MAXZ]));
        foreach (turf affected_turf in Dm.each<turf>(template_and_bordering_turfs))
        {
            affected_turf.air_update_turf(TRUE, TRUE);
            affected_turf.levelupdate();
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mapping/map_template.dm:109
    public virtual async DmTask<object> load_new_z(object secret = null)
    {
        // tg: a new z-level of ZTRAITS_AWAY (or ZTRAITS_AWAY_SECRET), the template centered on it, initTemplateBounds and smooth_zlevel
        Dm.NotTranslated("code/modules/mapping/map_template.dm:109", "load_new_z: the away missions' levels (D108 loads the station level only)");
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/modules/mapping/map_template.dm:135
    public virtual async DmTask<object> load(turf T = null, object centered = null, params object[] __extra)
    {
        centered ??= FALSE;
        if (Dm.truthy(centered))
            T = locate(T.x - Dm.round(width / 2), T.y - Dm.round(height / 2), T.z);
        if (T == null)
            return null;
        if ((T.x + width) - 1 > Dm.world.maxx)
            return null;
        if ((T.y + height) - 1 > Dm.world.maxy)
            return null;

        // Cache for sonic speed
        DmList to_rebuild = SSair.adjacent_rebuild;
        // iterate over turfs in the border and clear them from active atmos processing
        foreach (turf border_turf in Dm.each<turf>(CORNER_BLOCK_OFFSET(T, width + 2, height + 2, -1, -1)))
        {
            SSair.remove_from_active(border_turf);
            Dm.remove(to_rebuild, border_turf);
            foreach (turf sub_turf in Dm.each<turf>(border_turf.atmos_adjacent_turfs))
                sub_turf.atmos_adjacent_turfs?.Remove(border_turf);
            border_turf.atmos_adjacent_turfs?.Cut();
        }

        // Accept cached maps, but don't save them automatically - we don't want
        // ruins clogging up memory for the whole round.
        datum_parsed_map parsed = cached_map ?? (datum_parsed_map)await Dm.new_async(typeof(datum_parsed_map), Dm.file(Dm.str(mappath)));
        cached_map = Dm.truthy(keep_cached_map) ? parsed : null;

        DmList turf_blacklist = Dm.list();
        update_blacklist(T, turf_blacklist);

        // UNSETEMPTY(turf_blacklist) (__HELPERS/_lists.dm), expanded inline
        if (turf_blacklist.len == 0)
            turf_blacklist = null;
        parsed.turf_blacklist = turf_blacklist;
        if (!Dm.truthy(await parsed.load(
            T.x,
            T.y,
            T.z,
            crop_map: TRUE,
            no_changeturf: Dm.eq(SSatoms.initialized, INITIALIZATION_INSSATOMS) ? TRUE : FALSE,
            place_on_top: should_place_on_top)))
            return null;

        T = (turf)turf.dm_identity(T); // TURF-IDENTITY: tg's T names the corner, whose turf the template replaced
        DmList bounds = parsed.bounds;
        if (bounds == null)
            return null;

        require_area_resort();

        //initialize things that are normally initialized after map load
        await initTemplateBounds(bounds);

        if (Dm.truthy(has_ceiling))
        {
            object affected_turfs = get_affected_turfs(T, FALSE);
            await generate_ceiling(affected_turfs);
        }

        log_game(Dm.text("[] loaded at [],[],[]", name, T.x, T.y, T.z));
        return bounds;
    }

    // tg: code/modules/mapping/map_template.dm:191
    public virtual async DmTask generate_ceiling(object affected_turfs = null)
    {
        foreach (turf turf in Dm.each<turf>(affected_turfs))
        {
            var ceiling = get_step_multiz(turf, UP) as turf;
            if (ceiling != null)
            {
                if (ceiling is turf_open_openspace || ceiling is turf_open_space_openspace)
                    await ceiling.ChangeTurf(ceiling_turf, ceiling_baseturfs, CHANGETURF_INHERIT_AIR);
            }
        }
    }

    // tg: code/modules/mapping/map_template.dm:198
    public virtual async DmTask post_load(params object[] __extra)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mapping/map_template.dm:201
    public virtual void update_blacklist(turf T = null, DmList input_blacklist = null)
    {
    }

    // tg: code/modules/mapping/map_template.dm:204
    public virtual object get_affected_turfs(turf T = null, object centered = null)
    {
        centered ??= FALSE;
        turf placement = T;
        if (Dm.truthy(centered))
        {
            turf corner = locate(placement.x - Dm.round(width / 2), placement.y - Dm.round(height / 2), placement.z);
            if (corner != null)
                placement = corner;
        }
        return block(placement.x, placement.y, placement.z, placement.x + width - 1, placement.y + height - 1, placement.z);
    }

    /// Takes in a type path, locates an instance of that type in the cached map, and calculates its offset from the origin of the map, returns this offset in the form list(x, y).
    // tg: code/modules/mapping/map_template.dm:213
    public virtual object discover_offset(Type marker = null)
    {
        object key = null;
        DmList models = cached_map.grid_models;
        foreach (object model_key in Dm.each(models))
        {
            key = model_key;
            if (Dm.truthy(findtext(models[model_key], Dm.text("[]", marker)))) // Yay compile time checks
                break; // This works by assuming there will ever only be one mobile dock in a template at most
        }

        foreach (datum_grid_set gset in Dm.each<datum_grid_set>(cached_map.gridSets))
        {
            float ycrd = Dm.num(gset.ycrd);
            foreach (object line in Dm.each(gset.gridLines))
            {
                float xcrd = Dm.num(gset.xcrd);
                for (float j = 1, j__end = length(line), step = Dm.num(cached_map.key_len); j <= j__end; j += step)
                {
                    if (Dm.eq(key, copytext(line, j, j + step)))
                        return Dm.list(xcrd, ycrd);
                    ++xcrd;
                }
                --ycrd;
            }
        }
        return null;
    }
}

public static partial class GlobalProcs
{
    //for your ever biggening badminnery kevinz000
    //❤ - Cyberboss
    // tg: code/modules/mapping/map_template.dm:233
    public static async DmTask<object> load_new_z_level(object file = null, object name = null, object secret = null)
    {
        var template = (datum_map_template)await Dm.new_async(typeof(datum_map_template), file, name, TRUE);
        if (template.cached_map == null || Dm.truthy(template.cached_map.check_for_errors()))
            return FALSE;
        await template.load_new_z(secret);
        return TRUE;
    }
}
