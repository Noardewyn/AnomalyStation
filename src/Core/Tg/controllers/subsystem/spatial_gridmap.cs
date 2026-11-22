// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/spatial_gridmap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1: the hearing of playsound, owner's rule "translate the mechanic"), but the debug
// painter paint_grids and update_color (:854-877: admin colouring of every atom by its cell, NotTranslated). The var block
// is generated (D111); this part adds tg's procs and the object-creating defaults the generator leaves to it (GAP(types)).
// A world whose levels the host built (MapSeam has no selection: the toy and test rooms; SSmapping manages no level and its
// z_list stays null, mapping.cs) gets a grid per level of the map at Initialize (PORT below), as tg's z_list would give.

// tg: code/controllers/subsystem/spatial_gridmap.dm:11
public partial class datum_spatial_grid_cell : datum
{
    // tg: code/controllers/subsystem/spatial_gridmap.dm:36
    [DmParams("cell_x", "cell_y", "cell_z")]
    public override async DmTask New(params object[] __extra)
    {
        object cell_x = Dm.arg(__extra, 0);
        object cell_y = Dm.arg(__extra, 1);
        object cell_z = Dm.arg(__extra, 2);
        await base.New(__extra);
        this.cell_x = cell_x;
        this.cell_y = cell_y;
        this.cell_z = cell_z;
        //cache for sanic speed (lists are references anyways)
        DmList dummy_list = SSspatial_grid.dummy_list;

        if (Dm.truthy(length(dummy_list)))
        {
            dummy_list.Cut();
            stack_trace("SSspatial_grid.dummy_list had something inserted into it at some point! this is a problem as it is supposed to stay empty", "code/controllers/subsystem/spatial_gridmap.dm", 46);
        }

        hearing_contents = dummy_list;
        client_contents = dummy_list;
        atmos_contents = dummy_list;
        dynamic_light_sources = dummy_list;
    }

    // tg: code/controllers/subsystem/spatial_gridmap.dm:53
    public override object Destroy(float? force = null)
    {
        object dot = null;
        if (!Dm.truthy(force))//the response to someone trying to qdel this is a right proper fuck you
        {
            stack_trace("dont try to destroy spatial grid cells without a good reason. if you need to do it use force", "code/controllers/subsystem/spatial_gridmap.dm", 55);
            return QDEL_HINT_LETMELIVE;
        }

        dot = base.Destroy(force);
        return dot;
    }
}

// tg: code/controllers/subsystem/spatial_gridmap.dm:86 SUBSYSTEM_DEF(spatial_grid)
[DmPath("/datum/controller/subsystem/spatial_grid")]
public partial class datum_controller_subsystem_spatial_grid : datum_controller_subsystem
{
    ///the subsystem creates this many [/mob/oranges_ear] mob instances during init. allocations that require more than this create more.
    // tg: code/controllers/subsystem/spatial_gridmap.dm:2  #define NUMBER_OF_PREGENERATED_ORANGES_EARS 2500 (file-local, #undef at :881)
    private const int NUMBER_OF_PREGENERATED_ORANGES_EARS = 2500;

    // tg: code/controllers/subsystem/spatial_gridmap.dm:87
    public datum_controller_subsystem_spatial_grid()
    {
        can_fire = FALSE;
        name = "Spatial Grid";
    }

    // tg: code/controllers/subsystem/spatial_gridmap.dm:88-109 (the object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_mapping));
        grids_by_z_level = Dm.list(); // :94
        waiting_to_add_by_type = Dm.list(Dm.kv(SPATIAL_GRID_CONTENTS_TYPE_HEARING, Dm.list()), Dm.kv(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS, Dm.list()), Dm.kv(SPATIAL_GRID_CONTENTS_TYPE_ATMOS, Dm.list())); // :96
        spatial_grid_categories = Dm.list(); // :100
        dummy_list = Dm.list(); // :106
        pregenerated_oranges_ears = Dm.list(); // :109
    }

    // The New() that SUBSYSTEM_DEF(spatial_grid) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSspatial_grid) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/spatial_gridmap.dm:86
    public override async DmTask New(params object[] __extra)
    {
        if (SSspatial_grid != this)
        {
            if (SSspatial_grid is datum_controller_subsystem_spatial_grid)
            {
                Recover();
                qdel(SSspatial_grid);
            }
            SSspatial_grid = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/spatial_gridmap.dm:116
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        cells_on_x_axis = SPATIAL_GRID_CELLS_PER_SIDE(world.maxx);
        cells_on_y_axis = SPATIAL_GRID_CELLS_PER_SIDE(world.maxy);

        // enter_cell only runs if 'initialized'
        initialized = TRUE;

        foreach (datum_space_level z_level in Dm.each_cast<datum_space_level>(SSmapping.z_list))
        {
            propogate_spatial_grid_to_new_z(null, z_level);
            CHECK_TICK();
        }
        // PORT: a world whose levels the host built (no map selection: SSmapping keeps no z_list, mapping.cs) has the levels
        // of world.maxz; each gets its grid as tg's propogate_spatial_grid_to_new_z gives a managed level.
        if (!Dm.truthy(SSmapping.z_list))
        {
            for (float z_value = 1, z_value__end = world.maxz; z_value <= z_value__end; z_value++)
                propogate_spatial_grid_to_host_built_z(z_value);
        }

        //go through the pre init queue for anything waiting to be let in the grid
        foreach (object channel_type in Dm.each(waiting_to_add_by_type))
        {
            foreach (atom_movable movable in Dm.each_cast<atom_movable>(waiting_to_add_by_type[channel_type]))
            {
                turf movable_turf = get_turf(movable);
                if (Dm.truthy(movable_turf))
                    enter_cell(movable, movable_turf);

                UnregisterSignal(movable, COMSIG_QDELETING);
                Dm.remove_at(waiting_to_add_by_type, channel_type, movable);
            }
        }

        pregenerate_more_oranges_ears(NUMBER_OF_PREGENERATED_ORANGES_EARS);

        RegisterSignal(SSdcs, COMSIG_GLOB_NEW_Z, nameof(propogate_spatial_grid_to_new_z));
        RegisterSignal(SSdcs, COMSIG_GLOB_EXPANDED_WORLD_BOUNDS, nameof(after_world_bounds_expanded));
        return SS_INIT_SUCCESS;
    }

    ///add a movable to the pre init queue for whichever type is specified so that when the subsystem initializes they get added to the grid
    // tg: code/controllers/subsystem/spatial_gridmap.dm:144
    public virtual void enter_pre_init_queue(atom_movable waiting_movable = null, object type = null)
    {
        RegisterSignal(waiting_movable, COMSIG_QDELETING, nameof(queued_item_deleted), @override: TRUE);
        //override because something can enter the queue for two different types but that is done through unrelated procs that shouldnt know about eachother
        Dm.append_at(waiting_to_add_by_type, type, waiting_movable);
    }

    ///removes an initialized and probably deleted movable from our pre init queue before we're initialized
    // tg: code/controllers/subsystem/spatial_gridmap.dm:150
    public virtual void remove_from_pre_init_queue(atom_movable movable_to_remove = null, object exclusive_type = null)
    {
        if (exclusive_type == null)
        {
            UnregisterSignal(movable_to_remove, COMSIG_QDELETING);
            foreach (object type in Dm.each(waiting_to_add_by_type))
                Dm.remove_at(waiting_to_add_by_type, type, movable_to_remove);
            return;
        }

        Dm.remove_at(waiting_to_add_by_type, exclusive_type, movable_to_remove);

        // We need to check if this movable is inside the other queues
        foreach ((object type, object queue) in Dm.pairs(waiting_to_add_by_type))
        {
            if (Dm.@in(movable_to_remove, queue))
                return;
        }

        UnregisterSignal(movable_to_remove, COMSIG_QDELETING);
    }

    ///if a movable is inside our pre init queue before we're initialized and it gets deleted we need to remove that reference with this proc
    // tg: code/controllers/subsystem/spatial_gridmap.dm:167
    [SignalHandler]
    public virtual void queued_item_deleted(atom_movable movable_being_deleted = null)
    {
        remove_from_pre_init_queue(movable_being_deleted, null);
    }

    ///creates the spatial grid for a new z level
    // tg: code/controllers/subsystem/spatial_gridmap.dm:172
    [SignalHandler]
    public virtual void propogate_spatial_grid_to_new_z(datum_controller_subsystem_processing_dcs fucking_dcs = null, datum_space_level z_level = null)
    {
        DmList new_cell_grid = Dm.list();

        Dm.append(ref grids_by_z_level, Dm.list(new_cell_grid));

        for (float y = 1, y__end = cells_on_y_axis; y <= y__end; y++)
        {
            Dm.append(ref new_cell_grid, Dm.list(Dm.list()));
            for (float x = 1, x__end = cells_on_x_axis; x <= x__end; x++)
            {
                datum_spatial_grid_cell cell = Dm.@new<datum_spatial_grid_cell>(x, y, z_level.z_value);
                Dm.append_at(new_cell_grid, y, cell);
            }
        }
    }

    // PORT: propogate_spatial_grid_to_new_z (:172-184) for a level of a host-built world, which has no /datum/space_level
    // (see Initialize): the same statements with the level's number for z_level.z_value.
    private void propogate_spatial_grid_to_host_built_z(float z_value)
    {
        DmList new_cell_grid = Dm.list();

        Dm.append(ref grids_by_z_level, Dm.list(new_cell_grid));

        for (float y = 1, y__end = cells_on_y_axis; y <= y__end; y++)
        {
            Dm.append(ref new_cell_grid, Dm.list(Dm.list()));
            for (float x = 1, x__end = cells_on_x_axis; x <= x__end; x++)
            {
                datum_spatial_grid_cell cell = Dm.@new<datum_spatial_grid_cell>(x, y, z_value);
                Dm.append_at(new_cell_grid, y, cell);
            }
        }
    }

    ///adds cells to the grid for every z level when world.maxx or world.maxy is expanded after this subsystem is initialized. hopefully this is never needed.
    ///because i never tested this.
    // tg: code/controllers/subsystem/spatial_gridmap.dm:187
    [SignalHandler]
    public virtual void after_world_bounds_expanded(datum_controller_subsystem_processing_dcs fucking_dcs = null, object has_expanded_world_maxx = null, object has_expanded_world_maxy = null)
    {
        float old_x_axis = cells_on_x_axis;
        float old_y_axis = cells_on_y_axis;

        cells_on_x_axis = SPATIAL_GRID_CELLS_PER_SIDE(world.maxx);
        cells_on_y_axis = SPATIAL_GRID_CELLS_PER_SIDE(world.maxy);

        for (float z_level = 1, z_level__end = length(grids_by_z_level); z_level <= z_level__end; z_level++)
        {
            DmList z_level_gridmap = grids_by_z_level[z_level] as DmList;

            for (float cell_row_for_expanded_y_axis = 1, cell_row_for_expanded_y_axis__end = cells_on_y_axis; cell_row_for_expanded_y_axis <= cell_row_for_expanded_y_axis__end; cell_row_for_expanded_y_axis++)
            {

                if (cell_row_for_expanded_y_axis > old_y_axis)//we are past the old length of the number of rows, so add to the list
                    Dm.append(ref z_level_gridmap, Dm.list(Dm.list()));

                //now we know theres a row at this position, so add cells to it that need to be added and update the ones that already exist
                DmList cell_row = z_level_gridmap[cell_row_for_expanded_y_axis] as DmList;

                for (float grid_cell_for_expanded_x_axis = 1, grid_cell_for_expanded_x_axis__end = cells_on_x_axis; grid_cell_for_expanded_x_axis <= grid_cell_for_expanded_x_axis__end; grid_cell_for_expanded_x_axis++)
                {

                    if (grid_cell_for_expanded_x_axis > old_x_axis)
                    {
                        datum_spatial_grid_cell new_cell_inserted = Dm.@new<datum_spatial_grid_cell>(grid_cell_for_expanded_x_axis, cell_row_for_expanded_y_axis, z_level);
                        Dm.append(ref cell_row, new_cell_inserted);
                        continue;
                    }

                    //now we know the cell index we're at contains an already existing cell that needs its x and y values updated
                    datum_spatial_grid_cell old_cell_that_needs_updating = cell_row[grid_cell_for_expanded_x_axis] as datum_spatial_grid_cell;
                    old_cell_that_needs_updating.cell_x = grid_cell_for_expanded_x_axis;
                    old_cell_that_needs_updating.cell_y = cell_row_for_expanded_y_axis;
                }
            }
        }
    }

    // tg: code/controllers/subsystem/spatial_gridmap.dm:219  #define BOUNDING_BOX_MIN(center_coord) max(GET_SPATIAL_INDEX(center_coord - range), 1)
    // tg: code/controllers/subsystem/spatial_gridmap.dm:222  #define BOUNDING_BOX_MAX(center_coord, axis_size) min(GET_SPATIAL_INDEX(center_coord + range), axis_size)
    // (file-local, #undef at :879; they read the caller's range, so each use is expanded inline, rules 11.1 (d))

    /**
     * https://en.wikipedia.org/wiki/Range_searching#Orthogonal_range_searching
     *
     * searches through the grid cells intersecting a rectangular search space (with sides of length 2 * range) then returns all contents of type inside them.
     * much faster than iterating through view() to find all of what you want.
     *
     * this does NOT return things only in range distance from center! the search space is a square not a circle, if you want only things in a certain distance
     * then you need to filter that yourself
     *
     * * center - the atom that is the center of the searched circle
     * * type - the type of grid contents you are looking for, see __DEFINES/spatial_grid.dm
     * * range - the bigger this is, the more spatial grid cells the search space intersects
     */
    // tg: code/controllers/subsystem/spatial_gridmap.dm:237
    public virtual object orthogonal_range_search(atom center = null, object type = null, object range = null)
    {
        DmList dot = null;
        turf center_turf = get_turf(center);

        float center_x = center_turf.x;//used inside the macros
        float center_y = center_turf.y;

        dot = Dm.list();

        //technically THIS list only contains lists, but inside those lists are grid cell datums and we can go without a SINGLE var init if we do this
        DmList grid_level = grids_by_z_level[center_turf.z] as DmList;

        object sw__1 = type;
        if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS))
        {
            for (float row = max(GET_SPATIAL_INDEX(center_y - Dm.num(range)), 1), row__end = min(GET_SPATIAL_INDEX(center_y + Dm.num(range)), cells_on_y_axis); row <= row__end; row++)
            {
                for (float x_index = max(GET_SPATIAL_INDEX(center_x - Dm.num(range)), 1), x_index__end = min(GET_SPATIAL_INDEX(center_x + Dm.num(range)), cells_on_x_axis); x_index <= x_index__end; x_index++)
                {

                    Dm.append(ref dot, ((datum_spatial_grid_cell)((DmList)grid_level[row])[x_index]).client_contents);
                }
            }
        }

        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_HEARING))
        {
            for (float row = max(GET_SPATIAL_INDEX(center_y - Dm.num(range)), 1), row__end = min(GET_SPATIAL_INDEX(center_y + Dm.num(range)), cells_on_y_axis); row <= row__end; row++)
            {
                for (float x_index = max(GET_SPATIAL_INDEX(center_x - Dm.num(range)), 1), x_index__end = min(GET_SPATIAL_INDEX(center_x + Dm.num(range)), cells_on_x_axis); x_index <= x_index__end; x_index++)
                {

                    Dm.append(ref dot, ((datum_spatial_grid_cell)((DmList)grid_level[row])[x_index]).hearing_contents);
                }
            }
        }

        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_ATMOS))
        {
            for (float row = max(GET_SPATIAL_INDEX(center_y - Dm.num(range)), 1), row__end = min(GET_SPATIAL_INDEX(center_y + Dm.num(range)), cells_on_y_axis); row <= row__end; row++)
            {
                for (float x_index = max(GET_SPATIAL_INDEX(center_x - Dm.num(range)), 1), x_index__end = min(GET_SPATIAL_INDEX(center_x + Dm.num(range)), cells_on_x_axis); x_index <= x_index__end; x_index++)
                    Dm.append(ref dot, ((datum_spatial_grid_cell)((DmList)grid_level[row])[x_index]).atmos_contents);
            }
        }

        return dot;
    }

    ///get the grid cell encomapassing targets coordinates
    // tg: code/controllers/subsystem/spatial_gridmap.dm:269
    public virtual object get_cell_of(atom target = null)
    {
        turf target_turf = get_turf(target);
        if (!Dm.truthy(target_turf))
            return null;

        return ((DmList)((DmList)grids_by_z_level[target_turf.z])[GET_SPATIAL_INDEX(target_turf.y)])[GET_SPATIAL_INDEX(target_turf.x)];
    }

    ///get all grid cells intersecting the bounding box around center with sides of length 2 * range
    // tg: code/controllers/subsystem/spatial_gridmap.dm:277
    public virtual object get_cells_in_range(atom center = null, object range = null)
    {
        return get_cells_in_bounds(center, range, range);
    }

    ///get all grid cells intersecting the bounding box around center with sides of length (2 * range_x, 2 * range_y)
    // tg: code/controllers/subsystem/spatial_gridmap.dm:281
    public virtual object get_cells_in_bounds(atom center = null, object range_x = null, object range_y = null)
    {
        turf center_turf = get_turf(center);

        float center_x = center_turf.x;
        float center_y = center_turf.y;

        DmList intersecting_grid_cells = Dm.list();

        //the minimum x and y cell indexes to test
        float min_x = max(GET_SPATIAL_INDEX(center_x - Dm.num(range_x)), 1);
        float min_y = max(GET_SPATIAL_INDEX(center_y - Dm.num(range_y)), 1);//calculating these indices only takes around 2 microseconds

        //the maximum x and y cell indexes to test
        float max_x = min(GET_SPATIAL_INDEX(center_x + Dm.num(range_x)), cells_on_x_axis);
        float max_y = min(GET_SPATIAL_INDEX(center_y + Dm.num(range_y)), cells_on_y_axis);

        DmList grid_level = grids_by_z_level[center_turf.z] as DmList;

        for (float row = min_y, row__end = max_y; row <= row__end; row++)
        {
            DmList grid_row = grid_level[row] as DmList;

            for (float x_index = min_x, x_index__end = max_x; x_index <= x_index__end; x_index++)
                Dm.append(ref intersecting_grid_cells, grid_row[x_index]);
        }

        return intersecting_grid_cells;
    }

    /// Adds grid awareness to the passed in atom, of the passed in type
    /// Basically, when this atom moves between grids, it wants to have enter/exit cell called on it
    // tg: code/controllers/subsystem/spatial_gridmap.dm:309
    public virtual void add_grid_awareness(atom_movable add_to = null, object type = null)
    {
        // We need to ensure we have a new list reference, to build our new key out of
        DmList current_list = spatial_grid_categories[add_to.spatial_grid_key] as DmList;
        if (Dm.truthy(current_list))
            current_list = current_list.Copy();
        else
            current_list = Dm.list();
        // Now we do a binary insert, to ensure it's sorted (don't wanna overcache)
        // BINARY_INSERT_DEFINE(type, current_list, SORT_VAR_NO_TYPE, type, SORT_COMPARE_DIRECTLY, COMPARE_KEY)
        // (__HELPERS/_lists.dm:216, inline by rules 11.1 (a), (b)): SORT_VAR_NO_TYPE declares an untyped item, SORT_COMPARE_DIRECTLY
        // compares the items themselves (text here: the channel names), COMPARE_KEY reads __BIN_LIST[__BIN_MID].
        do
        {
            DmList __BIN_LIST = current_list;
            float __BIN_CTTL = length(__BIN_LIST);
            if (!Dm.truthy(__BIN_CTTL))
                Dm.append(ref __BIN_LIST, type);
            else
            {
                float __BIN_LEFT = 1;
                float __BIN_RIGHT = __BIN_CTTL;
                float __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                object __BIN_ITEM = null;
                while (__BIN_LEFT < __BIN_RIGHT)
                {
                    __BIN_ITEM = __BIN_LIST[__BIN_MID];
                    if (Dm.le((__BIN_ITEM), (type)))
                        __BIN_LEFT = __BIN_MID + 1;
                    else
                        __BIN_RIGHT = __BIN_MID;
                    __BIN_MID = Dm.shr(__BIN_LEFT + __BIN_RIGHT, 1);
                }
                __BIN_ITEM = __BIN_LIST[__BIN_MID];
                __BIN_MID = Dm.gt((__BIN_ITEM), (type)) ? __BIN_MID : __BIN_MID + 1;
                __BIN_LIST.Insert(__BIN_MID, type);
            }
        }
        while (Dm.truthy(FALSE));
        update_grid_awareness(add_to, current_list);
    }

    /// Removes grid awareness from the passed in atom, of the passed in type
    // tg: code/controllers/subsystem/spatial_gridmap.dm:321
    public virtual void remove_grid_awareness(atom_movable remove_from = null, object type = null)
    {
        // We need to ensure we have a new list reference, to build our new key out of
        DmList current_list = spatial_grid_categories[remove_from.spatial_grid_key] as DmList;
        if (Dm.truthy(current_list))
            current_list = current_list.Copy();
        else
            current_list = Dm.list();
        Dm.remove(ref current_list, type);
        update_grid_awareness(remove_from, current_list);
    }

    /// Alerts the atom's current cell that it wishes to be treated as a member
    /// This functionally amounts to "hey, I was recently made aware by [add_grid_awareness], please insert me into my current cell"
    // tg: code/controllers/subsystem/spatial_gridmap.dm:333
    public virtual void add_grid_membership(atom_movable add_to = null, turf target_turf = null, object type = null)
    {
        if (!Dm.truthy(target_turf))
            return;
        if (Dm.truthy(initialized))
            add_single_type(add_to, target_turf, type);
        else //SSspatial_grid isnt init'd yet, add ourselves to the queue
            enter_pre_init_queue(add_to, type);
    }

    /// Removes grid membership from the passed in atom, of the passed in type
    // tg: code/controllers/subsystem/spatial_gridmap.dm:342
    public virtual void remove_grid_membership(atom_movable remove_from = null, turf target_turf = null, object type = null)
    {
        if (!Dm.truthy(target_turf))
            return;
        if (Dm.truthy(initialized))
            remove_single_type(remove_from, target_turf, type);
        else //SSspatial_grid isnt init'd yet, remove ourselves from the queue
            remove_from_pre_init_queue(remove_from, type);
    }

    /// Updates the string that atoms hold that stores their grid awareness
    /// We will use it to key into their spatial grid categories later
    // tg: code/controllers/subsystem/spatial_gridmap.dm:352
    public virtual void update_grid_awareness(atom_movable update = null, DmList new_list = null)
    {
        // We locally store a stringified version of the list, to prevent people trying to mutate it
        update.spatial_grid_key = new_list.Join("-");
        // Ensure the global representation is cached
        if (!Dm.truthy(spatial_grid_categories[update.spatial_grid_key]))
            spatial_grid_categories[update.spatial_grid_key] = new_list;
    }

    ///find the spatial map cell that target belongs to, then add the target to it, as its type prefers.
    ///make sure to provide the turf new_target is "in"
    // tg: code/controllers/subsystem/spatial_gridmap.dm:361
    public virtual void enter_cell(atom_movable new_target = null, turf target_turf = null)
    {
        if (!Dm.truthy(initialized))
            return;
        if (QDELETED(new_target))
            throw Dm.CRASH("qdeleted or null target trying to enter the spatial grid!", "code/controllers/subsystem/spatial_gridmap.dm", 365);

        if (!Dm.truthy(target_turf) || !Dm.truthy(new_target.spatial_grid_key))
            throw Dm.CRASH("null turf loc or a new_target that doesn't support it trying to enter the spatial grid!", "code/controllers/subsystem/spatial_gridmap.dm", 368);

        float x_index = GET_SPATIAL_INDEX(target_turf.x);
        float y_index = GET_SPATIAL_INDEX(target_turf.y);
        float z_index = target_turf.z;

        datum_spatial_grid_cell intersecting_cell = ((DmList)((DmList)grids_by_z_level[z_index])[y_index])[x_index] as datum_spatial_grid_cell;
        foreach (object type in Dm.each(spatial_grid_categories[new_target.spatial_grid_key]))
        {
            object sw__1 = type;
            if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS))
            {
                DmList new_target_contents = new_target.important_recursive_contents; //cache for sanic speeds (lists are references anyways)
                GRID_CELL_SET(ref intersecting_cell.client_contents, new_target_contents[SPATIAL_GRID_CONTENTS_TYPE_CLIENTS]);
                if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS)]))
                    intersecting_cell._SendSignal(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), Dm.list(intersecting_cell, new_target_contents[SPATIAL_GRID_CONTENTS_TYPE_CLIENTS]));
            }

            else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_HEARING))
            {
                DmList new_target_contents = new_target.important_recursive_contents;
                GRID_CELL_SET(ref intersecting_cell.hearing_contents, new_target.important_recursive_contents[SPATIAL_GRID_CONTENTS_TYPE_HEARING]);
                if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_HEARING)]))
                    intersecting_cell._SendSignal(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_HEARING), Dm.list(intersecting_cell, new_target_contents[SPATIAL_GRID_CONTENTS_TYPE_HEARING]));
            }

            else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_ATMOS))
            {
                GRID_CELL_SET(ref intersecting_cell.atmos_contents, new_target);
                if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_ATMOS)]))
                    intersecting_cell._SendSignal(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_ATMOS), Dm.list(intersecting_cell, new_target));
            }
        }
    }

    ///acts like enter_cell() but only adds the target to a specified type of grid cell contents list
    // tg: code/controllers/subsystem/spatial_gridmap.dm:392
    public virtual object add_single_type(atom_movable new_target = null, turf target_turf = null, object exclusive_type = null)
    {
        if (!Dm.truthy(initialized))
            return null;
        if (QDELETED(new_target))
            throw Dm.CRASH("qdeleted or null target trying to enter the spatial grid!", "code/controllers/subsystem/spatial_gridmap.dm", 396);

        if (!Dm.truthy(target_turf) || !Dm.@in(exclusive_type, spatial_grid_categories[new_target.spatial_grid_key]))
            throw Dm.CRASH(Dm.text("null turf loc or a new_target that doesn't support it trying to enter the spatial grid as a []!", exclusive_type), "code/controllers/subsystem/spatial_gridmap.dm", 399);

        float x_index = GET_SPATIAL_INDEX(target_turf.x);
        float y_index = GET_SPATIAL_INDEX(target_turf.y);
        float z_index = target_turf.z;

        datum_spatial_grid_cell intersecting_cell = ((DmList)((DmList)grids_by_z_level[z_index])[y_index])[x_index] as datum_spatial_grid_cell;
        object sw__1 = exclusive_type;
        if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS))
        {
            DmList new_target_contents = new_target.important_recursive_contents; //cache for sanic speeds (lists are references anyways)
            GRID_CELL_SET(ref intersecting_cell.client_contents, new_target_contents[SPATIAL_GRID_CONTENTS_TYPE_CLIENTS]);
            if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS)]))
                intersecting_cell._SendSignal(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), Dm.list(intersecting_cell, new_target_contents[SPATIAL_GRID_CONTENTS_TYPE_CLIENTS]));
        }

        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_HEARING))
        {
            DmList new_target_contents = new_target.important_recursive_contents;
            GRID_CELL_SET(ref intersecting_cell.hearing_contents, new_target.important_recursive_contents[SPATIAL_GRID_CONTENTS_TYPE_HEARING]);
            if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_HEARING)]))
                intersecting_cell._SendSignal(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_HEARING), Dm.list(intersecting_cell, new_target_contents[SPATIAL_GRID_CONTENTS_TYPE_HEARING]));
        }

        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_ATMOS))
        {
            GRID_CELL_SET(ref intersecting_cell.atmos_contents, new_target);
            if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_ATMOS)]))
                intersecting_cell._SendSignal(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_ATMOS), Dm.list(intersecting_cell, new_target));
        }

        return intersecting_cell;
    }

    /**
     * find the spatial map cell that target used to belong to, then remove the target (and sometimes its important_recusive_contents) from it.
     * make sure to provide the turf old_target used to be "in"
     *
     * * old_target - the thing we want to remove from the spatial grid cell
     * * target_turf - the turf we use to determine the cell we're removing from
     * * exclusive_type - either null or a valid contents channel. if you just want to remove a single type from the grid cell then use this
     */
    // tg: code/controllers/subsystem/spatial_gridmap.dm:431
    public virtual object exit_cell(atom_movable old_target = null, turf target_turf = null, object exclusive_type = null)
    {
        if (!Dm.truthy(initialized))
            return null;

        if (!Dm.truthy(target_turf) || !Dm.truthy(old_target.spatial_grid_key))
        {
            stack_trace("/datum/controller/subsystem/spatial_grid/proc/exit_cell() was given null arguments or a old_target that doesn't use the spatial grid!", "code/controllers/subsystem/spatial_gridmap.dm", 436);
            return FALSE;
        }

        float x_index = GET_SPATIAL_INDEX(target_turf.x);
        float y_index = GET_SPATIAL_INDEX(target_turf.y);
        float z_index = target_turf.z;

        datum_spatial_grid_cell intersecting_cell = ((DmList)((DmList)grids_by_z_level[z_index])[y_index])[x_index] as datum_spatial_grid_cell;
        foreach (object type in Dm.each(spatial_grid_categories[old_target.spatial_grid_key]))
        {
            object sw__1 = type;
            if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS))
            {
                object old_target_contents = Dm.truthy(old_target.important_recursive_contents?[type]) ? old_target.important_recursive_contents[type] : old_target;
                GRID_CELL_REMOVE(ref intersecting_cell.client_contents, old_target_contents);
                if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_EXITED(type)]))
                    intersecting_cell._SendSignal(SPATIAL_GRID_CELL_EXITED(type), Dm.list(intersecting_cell, old_target_contents));
            }

            else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_HEARING))
            {
                object old_target_contents = Dm.truthy(old_target.important_recursive_contents?[type]) ? old_target.important_recursive_contents[type] : old_target;
                GRID_CELL_REMOVE(ref intersecting_cell.hearing_contents, old_target_contents);
                if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_EXITED(type)]))
                    intersecting_cell._SendSignal(SPATIAL_GRID_CELL_EXITED(type), Dm.list(intersecting_cell, old_target_contents));
            }

            else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_ATMOS))
            {
                GRID_CELL_REMOVE(ref intersecting_cell.atmos_contents, old_target);
                if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_EXITED(type)]))
                    intersecting_cell._SendSignal(SPATIAL_GRID_CELL_EXITED(type), Dm.list(intersecting_cell, old_target));
            }
        }

        return TRUE;
    }

    ///acts like exit_cell() but only removes the target from the specified type of grid cell contents list
    // tg: code/controllers/subsystem/spatial_gridmap.dm:463
    public virtual object remove_single_type(atom_movable old_target = null, turf target_turf = null, object exclusive_type = null)
    {
        if (!Dm.truthy(target_turf) || !Dm.truthy(exclusive_type) || !Dm.truthy(old_target.spatial_grid_key))
        {
            stack_trace("/datum/controller/subsystem/spatial_grid/proc/remove_single_type() was given null arguments or an old_target that doesn't use the spatial grid!", "code/controllers/subsystem/spatial_gridmap.dm", 465);
            return FALSE;
        }

        if (!Dm.@in(exclusive_type, spatial_grid_categories[old_target.spatial_grid_key]))
            return FALSE;

        float x_index = GET_SPATIAL_INDEX(target_turf.x);
        float y_index = GET_SPATIAL_INDEX(target_turf.y);
        float z_index = target_turf.z;

        datum_spatial_grid_cell intersecting_cell = ((DmList)((DmList)grids_by_z_level[z_index])[y_index])[x_index] as datum_spatial_grid_cell;

        object sw__1 = exclusive_type;
        if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS))
        {
            object old_target_contents = Dm.truthy(old_target.important_recursive_contents?[exclusive_type]) ? old_target.important_recursive_contents[exclusive_type] : old_target; //cache for sanic speeds (lists are references anyways)
            GRID_CELL_REMOVE(ref intersecting_cell.client_contents, old_target_contents);
            if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_EXITED(exclusive_type)]))
                intersecting_cell._SendSignal(SPATIAL_GRID_CELL_EXITED(exclusive_type), Dm.list(intersecting_cell, old_target_contents));
        }

        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_HEARING))
        {
            object old_target_contents = Dm.truthy(old_target.important_recursive_contents?[exclusive_type]) ? old_target.important_recursive_contents[exclusive_type] : old_target;
            GRID_CELL_REMOVE(ref intersecting_cell.hearing_contents, old_target_contents);
            if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_EXITED(exclusive_type)]))
                intersecting_cell._SendSignal(SPATIAL_GRID_CELL_EXITED(exclusive_type), Dm.list(intersecting_cell, old_target_contents));
        }

        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_ATMOS))
        {
            GRID_CELL_REMOVE(ref intersecting_cell.atmos_contents, old_target);
            if (Dm.truthy(intersecting_cell._listen_lookup?[SPATIAL_GRID_CELL_EXITED(exclusive_type)]))
                intersecting_cell._SendSignal(SPATIAL_GRID_CELL_EXITED(exclusive_type), Dm.list(intersecting_cell, old_target));
        }

        return TRUE;
    }

    /// if for whatever reason this movable is "untracked" e.g. it breaks the assumption that a movable is only inside the contents of any grid cell associated with its loc,
    /// this will error. this checks every grid cell in the world so dont call this on live unless you have to.
    /// returns TRUE if this movable is untracked, FALSE otherwise
    // tg: code/controllers/subsystem/spatial_gridmap.dm:497
    public virtual object untracked_movable_error(atom_movable movable_to_check = null)
    {
        if (!Dm.truthy(movable_to_check?.spatial_grid_key))
            return FALSE;

        if (!Dm.truthy(initialized))
            return FALSE;

        datum_spatial_grid_cell loc_cell = get_cell_of(movable_to_check) as datum_spatial_grid_cell;
        DmList containing_cells = find_hanging_cell_refs_for_movable(movable_to_check, remove_from_cells: FALSE) as DmList;
        //if we're in multiple cells, throw an error.
        //if we're in 1 cell but it cant be deduced by our location, throw an error.
        if (length(containing_cells) > 1 || (length(containing_cells) == 1 && Dm.truthy(loc_cell) && !Dm.eq(containing_cells[1], loc_cell) && containing_cells[1] != null))
        {
            object error_data = "";

            object location_string = "which is in nullspace, and thus not be within the contents of any spatial grid cell";
            if (Dm.truthy(loc_cell))
                location_string = Dm.text("which is supposed to only be in the contents of a spatial grid cell at coords: ([], [], [])", GRID_INDEX_TO_COORDS(loc_cell.cell_x), GRID_INDEX_TO_COORDS(loc_cell.cell_y), loc_cell.cell_z);

            object error_explanation = Dm.text("was in the contents of [] spatial grid cells when it was only supposed to be in one!", length(containing_cells));
            if (length(containing_cells) == 1)
            {
                error_explanation = "was in the contents of 1 spatial grid cell but it was inside the area handled by another grid cell!";
                datum_spatial_grid_cell bad_cell = containing_cells[1] as datum_spatial_grid_cell;

                error_data = Dm.text("within the contents of a cell at coords: ([], [], [])", GRID_INDEX_TO_COORDS(bad_cell.cell_x), GRID_INDEX_TO_COORDS(bad_cell.cell_y), bad_cell.cell_z);
            }

            if (!Dm.truthy(error_data))
            {
                foreach (datum_spatial_grid_cell cell in Dm.each<datum_spatial_grid_cell>(containing_cells))
                {
                    object coords = Dm.text("([], [], [])", GRID_INDEX_TO_COORDS(cell.cell_x), GRID_INDEX_TO_COORDS(cell.cell_y), cell.cell_z);
                    object contents = "";

                    if (Dm.@in(movable_to_check, cell.hearing_contents))
                        contents = "hearing";

                    if (Dm.@in(movable_to_check, cell.client_contents))
                    {
                        if (length(contents) > 0)
                            contents = Dm.text("[], client", contents);
                        else
                            contents = "client";
                    }

                    if (Dm.@in(movable_to_check, cell.atmos_contents))
                    {
                        if (length(contents) > 0)
                            contents = Dm.text("[], atmos", contents);
                        else
                            contents = "atmos";
                    }

                    if (length(error_data) > 0)
                        error_data = Dm.text("[], {coords: [], within channels: []}", error_data, coords, contents);
                    else
                        error_data = Dm.text("within the contents of the following cells: {coords: [], within channels: []}", coords, contents);
                }
            }

            /**
             * example:
             *
             * /mob/living/trolls_the_maintainer instance, which is supposed to only be in the contents of a spatial grid cell at coords: (136, 136, 14),
             * was in the contents of 3 spatial grid cells when it was only supposed to be in one! within the contents of the following cells:
             * {(68, 153, 2), within channels: hearing},
             * {coords: (221, 170, 3), within channels: hearing},
             * {coords: (255, 153, 11), within channels: hearing},
             * {coords: (136, 136, 14), within channels: hearing}.
             */
            stack_trace(Dm.text("[] instance, [], [] [].", movable_to_check.type, location_string, error_explanation, error_data), "code/controllers/subsystem/spatial_gridmap.dm", 557);

            return TRUE;
        }

        return FALSE;
    }

    /**
     * remove this movable from the grid by finding the grid cell its in and removing it from that.
     * if it cant infer a grid cell its located in (e.g. if its in nullspace but it can happen if the grid isnt expanded to a z level), search every grid cell.
     */
    // tg: code/controllers/subsystem/spatial_gridmap.dm:567
    public virtual void force_remove_from_grid(atom_movable to_remove = null)
    {
        if (!Dm.truthy(to_remove?.spatial_grid_key))
            return;

        if (!Dm.truthy(initialized))
        {
            remove_from_pre_init_queue(to_remove);//the spatial grid doesnt exist yet, so just take it out of the queue
            return;
        }

        // tg: :575-579  #ifdef UNIT_TESTS (untracked_movable_error, the second find_hanging_cell_refs_for_movable) #endif: inactive in
        // the production build (rules 1.7)

        datum_spatial_grid_cell loc_cell = get_cell_of(to_remove) as datum_spatial_grid_cell;

        if (Dm.truthy(loc_cell))
            GRID_CELL_REMOVE_ALL(loc_cell, to_remove);
        else
            find_hanging_cell_refs_for_movable(to_remove, remove_from_cells: TRUE);
    }

    ///remove this movable from the given spatial_grid_cell
    // tg: code/controllers/subsystem/spatial_gridmap.dm:589
    public virtual void force_remove_from_cell(atom_movable to_remove = null, datum_spatial_grid_cell input_cell = null)
    {
        if (!Dm.truthy(input_cell))
            return;

        GRID_CELL_REMOVE_ALL(input_cell, to_remove);
    }

    ///if shit goes south, this will find hanging references for qdeleting movables inside the spatial grid
    // tg: code/controllers/subsystem/spatial_gridmap.dm:596
    public virtual object find_hanging_cell_refs_for_movable(atom_movable to_remove = null, float? remove_from_cells = null)
    {
        remove_from_cells ??= TRUE;

        DmList queues_containing_movable = Dm.list();
        foreach (object queue_channel in Dm.each(waiting_to_add_by_type))
        {
            DmList queue_list = waiting_to_add_by_type[queue_channel] as DmList;
            if (Dm.@in(to_remove, queue_list))
            {
                Dm.append(ref queues_containing_movable, queue_channel);//just add the associative key
                if (Dm.truthy(remove_from_cells))
                    Dm.remove(ref queue_list, to_remove);
            }
        }

        if (!Dm.truthy(initialized))
            return queues_containing_movable;

        DmList containing_cells = Dm.list();
        foreach (DmList z_level_grid in Dm.each_cast<DmList>(grids_by_z_level))
        {
            foreach (DmList cell_row in Dm.each_cast<DmList>(z_level_grid))
            {
                foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(cell_row))
                {
                    if (Dm.@in(to_remove, Dm.bitor(Dm.bitor(cell.hearing_contents, cell.client_contents), cell.atmos_contents)))
                    {
                        Dm.append(ref containing_cells, cell);
                        if (Dm.truthy(remove_from_cells))
                            force_remove_from_cell(to_remove, cell);
                    }
                }
            }
        }

        return containing_cells;
    }

    ///creates number_to_generate new oranges_ear's and adds them to the subsystems list of ears.
    ///i really fucking hope this never gets called after init :clueless:
    // tg: code/controllers/subsystem/spatial_gridmap.dm:636
    public virtual void pregenerate_more_oranges_ears(object number_to_generate = null)
    {
        for (float new_ear = 1, new_ear__end = Dm.num(number_to_generate); new_ear <= new_ear__end; new_ear++)
            Dm.append(ref pregenerated_oranges_ears, Dm.@new<mob_oranges_ear>((object)null));

        number_of_oranges_ears = length(pregenerated_oranges_ears);
    }

    ///allocate one [/mob/oranges_ear] mob per turf containing atoms_that_need_ears and give them a reference to every listed atom in their turf.
    ///if an oranges_ear is allocated to a turf that already has an oranges_ear then the second one fails to allocate (and gives the existing one the atom it was assigned to)
    // tg: code/controllers/subsystem/spatial_gridmap.dm:644
    public virtual object assign_oranges_ears(DmList atoms_that_need_ears = null)
    {
        DmList dot = null;
        float input_length = length(atoms_that_need_ears);

        if (input_length > number_of_oranges_ears)
        {
            stack_trace(Dm.text("somehow, for some reason, more than the preset generated number of oranges ears was requested. thats fucking []. this is not good that should literally never happen", number_of_oranges_ears), "code/controllers/subsystem/spatial_gridmap.dm", 648);
            pregenerate_more_oranges_ears(input_length - number_of_oranges_ears);//im still gonna DO IT but ill complain about it
        }

        dot = Dm.list();

        ///the next unallocated /mob/oranges_ear that we try to allocate to assigned_atom's turf
        mob_oranges_ear current_ear = null;
        ///the next atom in atoms_that_need_ears an ear assigned to it
        atom assigned_atom = null;
        ///the turf loc of the current assigned_atom. turfs are used to track oranges_ears already assigned to one location so we dont allocate more than one
        ///because allocating more than one oranges_ear to a given loc wastes view iterations
        turf turf_loc = null;

        for (float current_ear_index = 1, current_ear_index__end = input_length; current_ear_index <= current_ear_index__end; current_ear_index++)
        {
            assigned_atom = atoms_that_need_ears[current_ear_index] as atom;

            turf_loc = get_turf(assigned_atom);
            if (!Dm.truthy(turf_loc))
                continue;

            current_ear = pregenerated_oranges_ears[current_ear_index] as mob_oranges_ear;

            if (Dm.truthy(turf_loc.assigned_oranges_ear))
            {
                Dm.append(ref turf_loc.assigned_oranges_ear.references, assigned_atom);
                continue; //if theres already an oranges_ear mob at assigned_movable's turf we give assigned_movable to it instead and dont allocate ourselves
            }

            Dm.append(ref current_ear.references, assigned_atom);

            current_ear.loc = turf_loc; //normally this is bad, but since this is meant to be as fast as possible we literally just need to exist there for view() to see us
            turf_loc.assigned_oranges_ear = current_ear;

            Dm.append(ref dot, current_ear);
        }
        return dot;
    }

    //A debugging proc that colors objects based on what grid they belong to
    // tg: code/controllers/subsystem/spatial_gridmap.dm:854
    public virtual async DmTask paint_grids()
    {
        Dm.NotTranslated("code/controllers/subsystem/spatial_gridmap.dm:854", "paint_grids: the debug painter (admin colouring of every atom by its grid cell; M10+ admin tooling)");
    }

    //A debugging proc that colors objects based on what grid they belong to
    // tg: code/controllers/subsystem/spatial_gridmap.dm:870
    [SignalHandler]
    public virtual void update_color(atom_movable thing = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/spatial_gridmap.dm:870", "update_color: the debug painter's signal handler (M10+ admin tooling)");
    }
}

// tg: code/controllers/subsystem/spatial_gridmap.dm:621  (/atom procs defined in this file, rules 1.9 "interleaved types")
public partial class atom
{
    ///debug proc for checking if a movable is in multiple cells when it shouldnt be (ie always unless multitile entering is implemented)
    // tg: code/controllers/subsystem/spatial_gridmap.dm:621
    public virtual void find_all_cells_containing(float? remove_from_cells = null)
    {
        remove_from_cells ??= FALSE;
        datum_spatial_grid_cell real_cell = SSspatial_grid.get_cell_of(this) as datum_spatial_grid_cell;
        DmList containing_cells = SSspatial_grid.find_hanging_cell_refs_for_movable(this as atom_movable, remove_from_cells) as DmList;

        message_admins(Dm.text("[] is located in the contents of [] spatial grid cells", this, length(containing_cells)));

        object cell_coords = Dm.text("the following cells contain []: ", this);
        foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(containing_cells))
            Dm.append(ref cell_coords, Dm.text("([], [], []), ", cell.cell_x, cell.cell_y, cell.cell_z));

        message_admins(cell_coords);
        message_admins(Dm.text("[] is supposed to only be contained in the cell at indexes ([], [], []). but is contained at the cells at []", this, real_cell.cell_x, real_cell.cell_y, real_cell.cell_z, cell_coords));
    }

    ///debug proc for finding how full the cells of src's z level are
    // tg: code/controllers/subsystem/spatial_gridmap.dm:682
    public virtual async DmTask find_grid_statistics_for_z_level(float? insert_clients = null)
    {
        insert_clients ??= 0;
        float raw_clients = 0;
        float raw_hearables = 0;
        float raw_atmos = 0;

        float cells_with_clients = 0;
        float cells_with_hearables = 0;
        float cells_with_atmos = 0;

        DmList client_list = Dm.list();
        DmList hearable_list = Dm.list();
        DmList atmos_list = Dm.list();

        float x_cell_count = world.maxx / SPATIAL_GRID_CELLSIZE;
        float y_cell_count = world.maxy / SPATIAL_GRID_CELLSIZE;

        float total_cells = Dm.pow(x_cell_count, 2);

        float average_clients_per_cell = 0;
        float average_hearables_per_cell = 0;
        float average_atmos_mech_per_call = 0;

        float hearable_min_x = x_cell_count;
        float hearable_max_x = 1;

        float hearable_min_y = y_cell_count;
        float hearable_max_y = 1;

        float client_min_x = x_cell_count;
        float client_max_x = 1;

        float client_min_y = y_cell_count;
        float client_max_y = 1;

        float atmos_min_x = x_cell_count;
        float atmos_max_x = 1;

        float atmos_min_y = y_cell_count;
        float atmos_max_y = 1;

        DmList inserted_clients = Dm.list();

        if (Dm.truthy(insert_clients))
        {
            DmList turfs = null;
            object level = SSmapping.get_level(z);
            if (Dm.truthy(is_station_level(level)))
                turfs = GLOB.station_turfs;

            else
                turfs = Z_TURFS(z);

            for (float client_to_insert = 0, client_to_insert__end = Dm.num(insert_clients); client_to_insert <= client_to_insert__end; client_to_insert++)
            {
                turf random_turf = pick(turfs) as turf;
                mob fake_client = Dm.@new<mob>();
                fake_client.important_recursive_contents = Dm.list(Dm.kv(SPATIAL_GRID_CONTENTS_TYPE_HEARING, Dm.list(fake_client)), Dm.kv(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS, Dm.list(fake_client)));
                await fake_client.forceMove(random_turf);
                Dm.append(ref inserted_clients, fake_client);
            }
        }

        DmList all_z_level_cells = SSspatial_grid.get_cells_in_range(this, 1000) as DmList;

        foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(all_z_level_cells))
        {
            float client_length = length(cell.client_contents);
            float hearable_length = length(cell.hearing_contents);
            float atmos_length = length(cell.atmos_contents);

            raw_clients += client_length;
            raw_hearables += hearable_length;
            raw_atmos += atmos_length;

            if (Dm.truthy(client_length))
            {
                cells_with_clients++;

                Dm.append(ref client_list, cell.client_contents);

                if (Dm.lt(cell.cell_x, client_min_x))
                    client_min_x = Dm.num(cell.cell_x);

                if (Dm.gt(cell.cell_x, client_max_x))
                    client_max_x = Dm.num(cell.cell_x);

                if (Dm.lt(cell.cell_y, client_min_y))
                    client_min_y = Dm.num(cell.cell_y);

                if (Dm.gt(cell.cell_y, client_max_y))
                    client_max_y = Dm.num(cell.cell_y);
            }

            if (Dm.truthy(hearable_length))
            {
                cells_with_hearables++;

                Dm.append(ref hearable_list, cell.hearing_contents);

                if (Dm.lt(cell.cell_x, hearable_min_x))
                    hearable_min_x = Dm.num(cell.cell_x);

                if (Dm.gt(cell.cell_x, hearable_max_x))
                    hearable_max_x = Dm.num(cell.cell_x);

                if (Dm.lt(cell.cell_y, hearable_min_y))
                    hearable_min_y = Dm.num(cell.cell_y);

                if (Dm.gt(cell.cell_y, hearable_max_y))
                    hearable_max_y = Dm.num(cell.cell_y);
            }

            if (Dm.truthy(raw_atmos))
            {
                cells_with_atmos++;

                Dm.append(ref atmos_list, cell.atmos_contents);

                if (Dm.lt(cell.cell_x, atmos_min_x))
                    atmos_min_x = Dm.num(cell.cell_x);

                if (Dm.gt(cell.cell_x, atmos_max_x))
                    atmos_max_x = Dm.num(cell.cell_x);

                if (Dm.lt(cell.cell_y, atmos_min_y))
                    atmos_min_y = Dm.num(cell.cell_y);

                if (Dm.gt(cell.cell_y, atmos_max_y))
                    atmos_max_y = Dm.num(cell.cell_y);
            }
        }

        float total_client_distance = 0;
        float total_hearable_distance = 0;
        float total_atmos_distance = 0;

        float average_client_distance = 0;
        float average_hearable_distance = 0;
        float average_atmos_distance = 0;

        foreach (object hearable in Dm.each(hearable_list))//n^2 btw
        {
            foreach (object other_hearable in Dm.each(hearable_list))
            {
                if (Dm.eq(hearable, other_hearable))
                    continue;
                total_hearable_distance += get_dist(hearable, other_hearable);
            }
        }

        foreach (object client in Dm.each(client_list))//n^2 btw
        {
            foreach (object other_client in Dm.each(client_list))
            {
                if (Dm.eq(client, other_client))
                    continue;
                total_client_distance += get_dist(client, other_client);
            }
        }

        foreach (object atmos in Dm.each(atmos_list))//n^2 btw
        {
            foreach (object other_atmos in Dm.each(atmos_list))
            {
                if (Dm.eq(atmos, other_atmos))
                    continue;
                total_atmos_distance += get_dist(atmos, other_atmos);
            }
        }

        if (Dm.truthy(length(hearable_list)))
            average_hearable_distance = Dm.div(total_hearable_distance, length(hearable_list));
        if (Dm.truthy(length(client_list)))
            average_client_distance = Dm.div(total_client_distance, length(client_list));
        if (Dm.truthy(length(atmos_list)))
            average_atmos_distance = Dm.div(total_atmos_distance, length(atmos_list));

        average_clients_per_cell = Dm.div(raw_clients, total_cells);
        average_hearables_per_cell = Dm.div(raw_hearables, total_cells);
        average_atmos_mech_per_call = Dm.div(raw_atmos, total_cells);

        foreach (mob inserted_client in Dm.each_cast<mob>(inserted_clients))
            qdel(inserted_client);

        message_admins(Dm.text("on z level [] there are [] clients ([] of whom are fakes inserted to random station turfs), [] hearables, and [] atmos machines. all of whom are inside the bounding box given by clients: ([], []) x ([], []), hearables: ([], []) x ([], []) and atmos machines: ([], []) x ([], []), on average there are [] clients per cell, [] hearables per cell, and [] per cell, [] cells have clients, [] have hearables, and [] have atmos machines the average client distance is: [], the average hearable_distance is [], and the average atmos distance is [] ",
            z, raw_clients, insert_clients, raw_hearables, raw_atmos,
            client_min_x, client_min_y, client_max_x, client_max_y,
            hearable_min_x, hearable_min_y, hearable_max_x, hearable_max_y,
            atmos_min_x, atmos_min_y, atmos_max_x, atmos_max_y,
            average_clients_per_cell, average_hearables_per_cell,
            average_atmos_mech_per_call,
            cells_with_clients, cells_with_hearables, cells_with_atmos,
            average_client_distance, average_hearable_distance,
            average_atmos_distance));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/spatial_gridmap.dm:86  SUBSYSTEM_DEF(spatial_grid): GLOBAL_REAL(SSspatial_grid, /datum/controller/subsystem/spatial_grid)
    internal datum_controller_subsystem_spatial_grid SSspatial_grid;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_spatial_grid SSspatial_grid => ref TgState.Current.SSspatial_grid;
}
