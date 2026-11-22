// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/spatial_gridmap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The function-like macros of the file (wave 4 package A: GET_SPATIAL_INDEX and HAS_SPATIAL_GRID_CONTENTS, which
// /atom/movable/proc/Moved reads, atoms_movable.dm:916-919; M4b audio slice 1, the spatial grid: the rest); the constants are
// generated. The GRID_CELL_* macros assign their list argument, so they take it by ref (rules 11.1, as the LAZY macros).

public static partial class GlobalProcs
{
    /// Takes a coordinate, and spits out the spatial grid index (x or y) it's inside
    // tg: code/__DEFINES/spatial_gridmap.dm:4  #define GET_SPATIAL_INDEX(coord) ROUND_UP((coord) / SPATIAL_GRID_CELLSIZE)
    public static float GET_SPATIAL_INDEX(float coord) => ROUND_UP((coord) / SPATIAL_GRID_CELLSIZE);

    /// changes the cell_(x or y) vars on /datum/spatial_grid_cell to the x or y coordinate on the map for the LOWER LEFT CORNER of the grid cell.
    /// index is from 1 to SPATIAL_GRID_CELLS_PER_SIDE
    // tg: code/__DEFINES/spatial_gridmap.dm:7  #define GRID_INDEX_TO_COORDS(index) ((((index) - 1) * SPATIAL_GRID_CELLSIZE) + 1)
    public static float GRID_INDEX_TO_COORDS(object index) => ((((Dm.num(index)) - 1) * SPATIAL_GRID_CELLSIZE) + 1);

    /// number of grid cells per x or y side of all z levels. pass in world.maxx or world.maxy
    // tg: code/__DEFINES/spatial_gridmap.dm:9  #define SPATIAL_GRID_CELLS_PER_SIDE(world_bounds) GET_SPATIAL_INDEX(world_bounds)
    public static float SPATIAL_GRID_CELLS_PER_SIDE(float world_bounds) => GET_SPATIAL_INDEX(world_bounds);

    // tg: code/__DEFINES/spatial_gridmap.dm:20  #define ALL_CONTENTS_OF_CELL(cell) (cell.hearing_contents | cell.client_contents | cell.atmos_contents)
    public static DmList ALL_CONTENTS_OF_CELL(datum_spatial_grid_cell cell) => (Dm.bitor(Dm.bitor(cell.hearing_contents, cell.client_contents), cell.atmos_contents) as DmList);

    ///whether movable is itself or containing something which should be in one of the spatial grid channels.
    // tg: code/__DEFINES/spatial_gridmap.dm:23  #define HAS_SPATIAL_GRID_CONTENTS(movable) (movable.spatial_grid_key)
    public static object HAS_SPATIAL_GRID_CONTENTS(object movable) => (((atom_movable)movable).spatial_grid_key);

    // macros meant specifically to add/remove movables from the internal lists of /datum/spatial_grid_cell,
    // when empty they become references to a single list in SSspatial_grid and when filled they become their own list
    // this is to save memory without making them lazylists as that slows down iteration through them
    // tg: code/__DEFINES/spatial_gridmap.dm:28  #define GRID_CELL_ADD(cell_contents_list, movable_or_list)
    public static void GRID_CELL_ADD(ref DmList cell_contents_list, object movable_or_list)
    {
        if (!Dm.truthy(length(cell_contents_list)))
        {
            cell_contents_list = Dm.list();
            Dm.append(ref cell_contents_list, movable_or_list);
        }
        else
            Dm.append(ref cell_contents_list, movable_or_list);
    }

    // tg: code/__DEFINES/spatial_gridmap.dm:36  #define GRID_CELL_SET(cell_contents_list, movable_or_list)
    public static void GRID_CELL_SET(ref DmList cell_contents_list, object movable_or_list)
    {
        if (!Dm.truthy(length(cell_contents_list)))
        {
            cell_contents_list = Dm.list();
            Dm.append(ref cell_contents_list, movable_or_list);
        }
        else
            Dm.combine(ref cell_contents_list, movable_or_list);
    }

    // tg: code/__DEFINES/spatial_gridmap.dm:44  #define GRID_CELL_ASSOC_SET(cell_contents_list, key, value)
    public static void GRID_CELL_ASSOC_SET(ref DmList cell_contents_list, object key, object value)
    {
        if (!Dm.truthy(length(cell_contents_list)))
        {
            cell_contents_list = Dm.list();
            cell_contents_list[key] = value;
        }
        else
            cell_contents_list[key] = value;
    }

    //dont use these outside of SSspatial_grid's scope use the procs it has for this purpose
    // tg: code/__DEFINES/spatial_gridmap.dm:53  #define GRID_CELL_REMOVE(cell_contents_list, movable_or_list)
    public static void GRID_CELL_REMOVE(ref DmList cell_contents_list, object movable_or_list)
    {
        Dm.remove(ref cell_contents_list, movable_or_list);
        if (!Dm.truthy(length(cell_contents_list)))
            cell_contents_list = SSspatial_grid.dummy_list;
    }

    ///remove from every list
    // tg: code/__DEFINES/spatial_gridmap.dm:60  #define GRID_CELL_REMOVE_ALL(cell, movable)
    public static void GRID_CELL_REMOVE_ALL(datum_spatial_grid_cell cell, object movable)
    {
        GRID_CELL_REMOVE(ref cell.hearing_contents, movable);
        GRID_CELL_REMOVE(ref cell.client_contents, movable);
        GRID_CELL_REMOVE(ref cell.atmos_contents, movable);
    }
}
