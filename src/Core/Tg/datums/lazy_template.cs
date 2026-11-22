// Translated from tgstation (snapshot 1d99bd7f): code/datums/lazy_template.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1a (D316): the lazy templates, whole (the contractor's victims go to the ninja holding facility). A template's
// map is a converted .as3dmap mounted at tg's path (MapSeam, tools/convert-maps.sh). The var blocks are generated (D111).

/**
 * Datum used to designate certain areas that do not need to exist nor be loaded at world start
 * but do want to be loaded under certain circumstances. Use this for stuff like the nukie base or wizden, aka stuff that only matters when their antag is rolled.
 */
// tg: code/datums/lazy_template.dm:6
public partial class datum_lazy_template : datum
{
    // tg: code/datums/lazy_template.dm:21
    public override async DmTask New(params object[] __extra)
    {
        reservations = Dm.list();
        await base.New(__extra);
    }

    // tg: code/datums/lazy_template.dm:25
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(force))
        {
            stack_trace(Dm.text("Something is trying to delete []", type), "code/datums/lazy_template.dm", 27);
            return QDEL_HINT_LETMELIVE;
        }

        QDEL_LIST(reservations);
        Dm.remove(GLOB.lazy_templates, key);
        return base.Destroy(force);
    }

    /**
     * Does the grunt work of loading the template.
     */
    // tg: code/datums/lazy_template.dm:37
    public virtual async DmTask<object> lazy_load()
    {
        // This is a static assosciative list that is used to ensure maps that have variations are correctly varied when spawned
        // I want to make it to where you can make a range and it'll randomly pick'n'take from the available versions at random
        // But that can be done later when I have the time
        DmList multiple_allocation_hash = TgState.Current.datum_lazy_template__lazy_load__multiple_allocation_hash;

        string load_path = Dm.text("[]/[].dmm", map_dir, map_name);
        if (Dm.truthy(uses_multiple_allocations))
        {
            float times = Dm.num(multiple_allocation_hash[key]);
            times += 1;
            multiple_allocation_hash[key] = times;
            load_path = Dm.text("[]/[]_[].dmm", map_dir, map_name, times);
        }

        if (string.IsNullOrEmpty(load_path) || !Dm.truthy(Dm.fexists(load_path)))
            throw Dm.CRASH(Dm.text("lazy template [] has an invalid load_path: '[]', check directory and map name!", type, load_path), "code/datums/lazy_template.dm", 52);

        var parsed_template = await load_map(Dm.file(load_path), measure_only: TRUE) as datum_parsed_map;
        if (parsed_template?.parsed_bounds == null)
            throw Dm.CRASH(Dm.text("Failed to cache lazy template for loading: '[]'", key), "code/datums/lazy_template.dm", 59);

        float width = Dm.num(parsed_template.parsed_bounds[MAP_MAXX]) - Dm.num(parsed_template.parsed_bounds[MAP_MINX]) + 1;
        float height = Dm.num(parsed_template.parsed_bounds[MAP_MAXY]) - Dm.num(parsed_template.parsed_bounds[MAP_MINY]) + 1;
        float z_count = Dm.num(parsed_template.parsed_bounds[MAP_MAXZ]);
        var reservation = await SSmapping.request_turf_block_reservation(
            width,
            height,
            z_count,
            reservation_type: turf_reservation_type) as datum_turf_reservation;
        if (reservation == null)
            throw Dm.CRASH(Dm.text("Failed to reserve a block for lazy template: '[]'", key), "code/datums/lazy_template.dm", 70);

        // lists kept for overall loading
        DmList loaded_atom_movables = Dm.list();
        DmList loaded_turfs = Dm.list();
        DmList loaded_areas = Dm.list();

        DmList loaded_cables = Dm.list();
        DmList loaded_atmospherics = Dm.list();

        for (int z_idx = (int)z_count; z_idx >= 1; z_idx--)
        {
            var bottom_left = reservation.bottom_left_turfs[z_idx] as turf;
            var top_right = reservation.top_right_turfs[z_idx] as turf;
            float x1 = bottom_left.x, y1 = bottom_left.y, z = bottom_left.z, x2 = top_right.x, y2 = top_right.y;

            // Make our turfs dead to atmos
            // Cache for sonic speed
            DmList to_rebuild = SSair.adjacent_rebuild;
            foreach (turf contained_turf in Dm.each<turf>(block(x1, y1, z, x2, y2, z)))
            {
                SSair.remove_from_active(contained_turf);
                Dm.remove(to_rebuild, contained_turf);
                foreach (turf sub_turf in Dm.each<turf>(contained_turf.atmos_adjacent_turfs))
                    sub_turf.atmos_adjacent_turfs?.Remove(contained_turf);
                contained_turf.atmos_adjacent_turfs?.Cut();
            }

            await load_map(
                Dm.file(load_path),
                x1,
                y1,
                z,
                z_upper: z_idx,
                z_lower: z_idx,
                place_on_top: place_on_top);
            // TURF-IDENTITY: the block is taken again, the load replaced the reserved turfs
            foreach (turf loaded in Dm.each<turf>(block(x1, y1, z, x2, y2, z)))
            {
                Dm.append(ref loaded_turfs, loaded);
                Dm.combine(ref loaded_areas, get_area(loaded));

                // atoms can actually be in the contents of two or more turfs based on its icon/bound size
                // see https://www.byond.com/docs/ref/index.html#/atom/var/contents
                foreach (object thing in Dm.each(Dm.sub(loaded.get_all_contents(), loaded)))
                {
                    if (thing is obj_structure_cable)
                        Dm.append(ref loaded_cables, thing);
                    else if (thing is obj_machinery_atmospherics)
                        Dm.append(ref loaded_atmospherics, thing);
                    Dm.combine(ref loaded_atom_movables, thing);
                }
            }
        }

        SSatoms.InitializeAtoms(Dm.add(Dm.add(loaded_areas, loaded_atom_movables), loaded_turfs) as DmList);
        // TURF-IDENTITY (gaps BR1): a turf whose Initialize changed it (a random mineral wall becomes its ore's wall) is a new object in
        // the port; tg's list names the location, so the loaded turfs are the turfs now at their cells
        for (int i = 1; i <= loaded_turfs.len; i++)
            loaded_turfs[i] = turf.dm_identity(loaded_turfs[i] as turf);
        foreach (turf loaded in Dm.each<turf>(loaded_turfs))
            CALCULATE_ADJACENT_TURFS(loaded, NORMAL_TURF);

        SSlighting.setup_static_lighting_if_needed(loaded_turfs);
        SSmachines.setup_template_powernets(loaded_cables);
        SSair.setup_template_machinery(loaded_atmospherics);
        foreach (turf loaded in Dm.each<turf>(loaded_turfs))
            CeilingSystem.Of(World.Current).TemplateLoaded(loaded); // DEVIATION(DV183): the template's cells get the map's default ceilings, as map_template.cs

        if (Dm.truthy(_listen_lookup?[COMSIG_LAZY_TEMPLATE_LOADED]))
            _SendSignal(COMSIG_LAZY_TEMPLATE_LOADED, Dm.list(this, loaded_atom_movables, loaded_turfs, loaded_areas)); // gaps BR1: the source first, as SEND_SIGNAL gives it
        Dm.append(ref reservations, reservation);
        return reservation;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/lazy_template.dm:42  var/static/list/multiple_allocation_hash = list()
    internal DmList datum_lazy_template__lazy_load__multiple_allocation_hash = Dm.list();
}
