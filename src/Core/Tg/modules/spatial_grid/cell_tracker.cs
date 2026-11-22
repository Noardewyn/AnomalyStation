// Translated from tgstation (snapshot 1d99bd7f): code/modules/spatial_grid/cell_tracker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1, with the spatial grid). The var block is generated (D111).

/**
 * Spatial gridmap, cell tracking
 *
 * This datum exists to make the large, repeated "everything in some range" pattern faster
 * Rather then just refreshing against everything, we track all the cells in range of the passed in "window"
 * This lets us do entered/left logic, and make ordinarially quite expensive logic much cheaper
 *
 * Note: This system should not be used for things who have strict requirements about what is NOT in their processed entries
 * It should instead only be used for logic that only really cares about limiting how much gets "entered" in any one call
 * Because we apply this limitation, we can do things to make our code much less prone to unneeded work
 */
// tg: code/modules/spatial_grid/cell_tracker.dm:12
[DmPath("/datum/cell_tracker")]
public partial class datum_cell_tracker : datum
{
    /// Accepts a width and height to use for this tracker
    /// Also accepts the ratio to use between inner and outer window. Optional, defaults to 2
    // tg: code/modules/spatial_grid/cell_tracker.dm:31
    [DmParams("width", "height", "inner_outer_ratio")]
    public override async DmTask New(params object[] __extra)
    {
        float? width = Dm.arg<float?>(__extra, 0);
        float? height = Dm.arg<float?>(__extra, 1);
        float? inner_outer_ratio = Dm.arg<float?>(__extra, 2);
        set_bounds(width, height, inner_outer_ratio);
        await base.New(__extra);
    }

    // tg: code/modules/spatial_grid/cell_tracker.dm:35
    public override object Destroy(float? force = null)
    {
        stack_trace("Attempted to delete a cell tracker. They don't hold any refs outside of cells, what are you doing", "code/modules/spatial_grid/cell_tracker.dm", 36);
        if (!Dm.truthy(force))
            return QDEL_HINT_LETMELIVE;
        member_cells.Cut();
        return base.Destroy(force);
    }

    /// Takes a width and height, and uses them to set the inner window, and interpolate the outer window
    // tg: code/modules/spatial_grid/cell_tracker.dm:43
    public virtual void set_bounds(float? width = null, float? height = null, float? ratio = null)
    {
        width ??= 0;
        height ??= 0;
        ratio ??= 2;
        // We want to store these as radii, rather then width and height, since that's convineient for spatial grid code
        float x_radius = CEILING(Dm.num(width), 2);
        float y_radius = CEILING(Dm.num(height), 2);
        inner_window_x_radius = x_radius;
        inner_window_y_radius = y_radius;

        outer_window_x_radius = x_radius * Dm.num(ratio);
        outer_window_y_radius = y_radius * Dm.num(ratio);
    }

    /// Returns a list of newly and formerly joined spatial grid managed objects of type [type] in the form list(new, old)
    /// Takes the center of our window as input
    // tg: code/modules/spatial_grid/cell_tracker.dm:55
    public virtual object recalculate_type_members(turf center = null, object type = null)
    {
        DmList new_and_old = recalculate_cells(center) as DmList;

        DmList new_members = Dm.list();
        DmList former_members = Dm.list();
        /// Pull out all the new and old memebers we want
        object sw__1 = type;
        if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS))
        {
            foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(new_and_old[1]))
                Dm.append(ref new_members, cell.client_contents);
            foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(new_and_old[2]))
                Dm.append(ref former_members, cell.client_contents);
        }
        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_HEARING))
        {
            foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(new_and_old[1]))
                Dm.append(ref new_members, cell.hearing_contents);
            foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(new_and_old[2]))
                Dm.append(ref former_members, cell.hearing_contents);
        }
        else if (Dm.eq(sw__1, SPATIAL_GRID_CONTENTS_TYPE_ATMOS))
        {
            foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(new_and_old[1]))
                Dm.append(ref new_members, cell.atmos_contents);
            foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(new_and_old[2]))
                Dm.append(ref former_members, cell.atmos_contents);
        }

        return Dm.list(new_members, former_members);
    }

    /// Recalculates our member list, returns a list in the form list(new members, old members) for reaction
    /// Accepts the turf to use as our "center"
    // tg: code/modules/spatial_grid/cell_tracker.dm:82
    public virtual object recalculate_cells(turf center = null)
    {
        if (!Dm.truthy(center))
            throw Dm.CRASH("/datum/cell_tracker had an invalid location on refresh, ya done fucked", "code/modules/spatial_grid/cell_tracker.dm", 84);
        // This is a mild waste of cpu time. Consider optimizing by adding a new helper function to get just the space between two bounds
        // Assuming it ever becomes a real problem
        DmList inner_window = SSspatial_grid.get_cells_in_bounds(center, inner_window_x_radius, inner_window_y_radius) as DmList;
        DmList outer_window = SSspatial_grid.get_cells_in_bounds(center, outer_window_x_radius, outer_window_y_radius) as DmList;

        DmList new_cells = Dm.sub(inner_window, member_cells) as DmList;
        // The outer window may contain cells we don't actually have, so we do it like this
        DmList old_cells = Dm.sub(member_cells, outer_window) as DmList;

        // This whole thing is a naive implementation,
        // if it turns out to be expensive because of all the list operations I'll look closer at it
        Dm.remove(ref member_cells, old_cells);
        Dm.append(ref member_cells, new_cells);

        return Dm.list(new_cells, old_cells);
    }
}
