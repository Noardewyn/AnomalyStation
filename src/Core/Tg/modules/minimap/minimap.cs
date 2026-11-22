// Translated from tgstation (snapshot 1d99bd7f): code/modules/minimap/minimap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /datum/minimap/proc/load_z translated, for MinimapSeam (D315): tg's cache of the made minimaps (GLOBAL_ALIST_EMPTY(minimaps),
// get_minimap_for_z) is the server's cache of the seam's pictures, made outside the world's step (DEVIATION(DV266)).

public partial class datum_minimap : datum
{
    // tg: code/modules/minimap/minimap.dm:17
    public virtual object load_z(object z = null)
    {
        // . = FALSE: never read, every exit returns TRUE or crashes
        if (!isnum(z) || Dm.num(z) > length(SSmapping.z_list))
            throw CRASH(Dm.text("Tried to create minimap for invalid Z-level ([])", z), "code/modules/minimap/minimap.dm", 20);

        base_map = Dm.icon("icons/ui_icons/minimap/minimap.dmi");
        this.z = z;

        float min_x = world.maxx;
        float min_y = world.maxy;
        float max_x = 1;
        float max_y = 1;

        map_position_to_name.Cut();
        foreach (turf location in Dm.each_cast<turf>(Z_TURFS(Dm.num(z))))
        {
            if (Dm.truthy(location.skip_minimap_rendering) || isshuttleturf(location))
                continue;
            area arealoc = (area)location.loc;
            if (Dm.truthy(arealoc.skip_minimap_rendering))
                continue;
            float x = location.x;
            float y = location.y;
            min_x = Dm.min(min_x, x);
            min_y = Dm.min(min_y, y);
            max_x = Dm.max(max_x, x);
            max_y = Dm.max(max_y, y);
            if (Dm.truthy(location.density))
            {
                base_map.DrawBox(location.tacmap_color, x, y);
                continue;
            }
            atom_movable alttarget = (atom_movable)(Dm.locate<obj_machinery_door>(location) is var or__1 && Dm.truthy(or__1) ? or__1
                : (Dm.locate<obj_structure_window>(location) is var or__2 && Dm.truthy(or__2) ? or__2 : (object)Dm.locate<obj_structure_fence>(location)));
            if (Dm.truthy(alttarget))
            {
                base_map.DrawBox(alttarget.tacmap_color, x, y);
                continue;
            }
            if (Dm.truthy(arealoc.tacmap_color))
            {
                base_map.DrawBox(BlendRGB(location.tacmap_color, arealoc.tacmap_color, 0.5f), x, y);
                continue;
            }
            if (istype(location, typeof(turf_open_floor_engine_hull)))
            {
                turf turf_below = (turf)GET_TURF_BELOW(location);
                area below_area = turf_below?.loc as area;
                // we'll draw the below area's color but transparent
                if (Dm.truthy(below_area?.tacmap_color))
                {
                    DmList below_color = rgb2num(below_area.tacmap_color);
                    base_map.DrawBox(rgb(Dm.num(below_color[1]), Dm.num(below_color[2]), Dm.num(below_color[3]), 64), x, y);
                    continue;
                }
            }
            base_map.DrawBox(location.tacmap_color, x, y);
        }

        this.min_x = min_x;
        this.min_y = min_y;

        base_map.Crop(min_x, min_y, max_x, max_y);
        // DEVIATION(DV266): no base_map.Scale(Width() * MINIMAP_PIXEL_MULTIPLIER, ...): the client draws the picture at its own zoom.

        return TRUE;
    }
}
