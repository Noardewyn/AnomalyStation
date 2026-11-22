// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/lighting_turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lumcount: the whole file. The lighting object is the turf's mark dm_lighting_object (DEVIATION DV85,
// lighting_object.cs); transfer_area_lighting's overlays (the offset planes' area lighting and the starlight overlay) are
// presentation the client draws (D24).

// tg: code/modules/lighting/lighting_turf.dm:2
public partial class turf : atom
{
    // Causes any affecting light sources to be queued for a visibility update, for example a door got opened.
    // tg: code/modules/lighting/lighting_turf.dm:2
    public virtual void reconsider_lights()
    {
        lighting_corner_NE?.vis_update();
        lighting_corner_SE?.vis_update();
        lighting_corner_SW?.vis_update();
        lighting_corner_NW?.vis_update();
    }

    // tg: code/modules/lighting/lighting_turf.dm:8
    public virtual void lighting_clear_overlay()
    {
        if (Dm.truthy(dm_lighting_object)) // DEVIATION(DV85): the mark stands for lighting_object
            dm_lighting_object_del();
    }

    // Builds a lighting object for us, but only if our area is dynamic.
    // tg: code/modules/lighting/lighting_turf.dm:13
    public virtual void lighting_build_overlay()
    {
        if (Dm.truthy(dm_lighting_object)) // DEVIATION(DV85)
            dm_lighting_object_del(); //Shitty fix for lighting objects persisting after death

        dm_lighting_object_new(); // new /atom/movable/lighting_object(null, src) (DEVIATION DV85)
    }

    /// Used to get a scaled lumcount.
    // tg: code/modules/lighting/lighting_turf.dm:20
    public virtual object get_lumcount(float? minlum = null, float? maxlum = null)
    {
        minlum ??= 0;
        maxlum ??= 1;
        if (!Dm.truthy(dm_lighting_object)) // DEVIATION(DV85)
            return 1f;

        float totallums = Dm.num(get_static_lumcount(minlum, maxlum));
        totallums += Dm.num(get_dynamic_lumcount());
        return CLAMP01(totallums);
    }

    // Kyler was worried about cost of having these use assoc args, so they're split ~smartkar
    // Also easier to use this way
    /// Optimized get_lumcount for when you only care if the lumcount is below a certain value, and not what the actual value is
    // tg: code/modules/lighting/lighting_turf.dm:32
    public virtual object check_lumcount_below(object value = null)
    {
        if (!Dm.truthy(dm_lighting_object)) // DEVIATION(DV85)
            return FALSE;

        float lums = Dm.num(get_static_lumcount());
        if (lums >= Dm.num(value))
            return FALSE;

        foreach (datum_component_overlay_lighting light in Dm.each<datum_component_overlay_lighting>(collect_dynamic_lightsources()))
        {
            lums += light.lum_power;
            if (lums >= Dm.num(value))
                return FALSE;
        }
        return TRUE;
    }

    /// Optimized get_lumcount for when you only care if the lumcount is above a certain value, and not what the actual value is
    // tg: code/modules/lighting/lighting_turf.dm:46
    public virtual object check_lumcount_above(object value = null)
    {
        if (!Dm.truthy(dm_lighting_object)) // DEVIATION(DV85)
            return TRUE;

        float lums = Dm.num(get_static_lumcount());
        if (lums > Dm.num(value))
            return TRUE;

        foreach (datum_component_overlay_lighting light in Dm.each<datum_component_overlay_lighting>(collect_dynamic_lightsources()))
        {
            lums += light.lum_power;
            if (lums > Dm.num(value))
                return TRUE;
        }
        return FALSE;
    }

    /// Returns lumcount from turf lighting
    // tg: code/modules/lighting/lighting_turf.dm:60
    public virtual object get_static_lumcount(float? minlum = null, float? maxlum = null)
    {
        minlum ??= 0;
        maxlum ??= 1;
        float totallums = 0;
        datum_lighting_corner L;
        L = lighting_corner_NE;
        if (L != null)
            totallums += L.lum_r + L.lum_b + L.lum_g;
        L = lighting_corner_SE;
        if (L != null)
            totallums += L.lum_r + L.lum_b + L.lum_g;
        L = lighting_corner_SW;
        if (L != null)
            totallums += L.lum_r + L.lum_b + L.lum_g;
        L = lighting_corner_NW;
        if (L != null)
            totallums += L.lum_r + L.lum_b + L.lum_g;

        totallums /= 12; // 4 corners, each with 3 channels, get the average.
        totallums = (totallums - minlum.Value) / (maxlum.Value - minlum.Value);
        return totallums;
    }

    /// Fetches dynamic lumcount from lightsources potentially in view of this turf from our spatial grid
    // tg: code/modules/lighting/lighting_turf.dm:81
    public virtual object get_dynamic_lumcount()
    {
        float dot = 0;
        foreach (datum_component_overlay_lighting light in Dm.each<datum_component_overlay_lighting>(collect_dynamic_lightsources()))
            dot += light.lum_power;
        return dot;
    }

    // You've heard of oranges_ear, prepare for oranges_eye
    /// Uses the same optimization via viewers() that get_hearers_in_view does by allocating oranges ears to overlay lights
    /// And collecting viewers rather than checking view() for each one of them
    // tg: code/modules/lighting/lighting_turf.dm:89
    public virtual object collect_dynamic_lightsources()
    {
        DmList dot = Dm.list();

        datum_spatial_grid_cell grid_cell = SSspatial_grid.get_cell_of(this) as datum_spatial_grid_cell;
        DmList light_sources = Dm.list();
        float furthest_range = 0;
        foreach (datum_component_overlay_lighting light in Dm.each<datum_component_overlay_lighting>(grid_cell.dynamic_light_sources))
        {
            furthest_range = max(furthest_range, light.lumcount_range);
            if (light_sources[light.current_holder] == null)
                light_sources[light.current_holder] = light;
            else if (islist(light_sources[light.current_holder]))
                Dm.combine_at(light_sources, light.current_holder, light);
            else
                light_sources[light.current_holder] = Dm.list(light_sources[light.current_holder], light);
        }

        DmList assigned_oranges_ears = SSspatial_grid.assign_oranges_ears(light_sources) as DmList;
        foreach (mob_oranges_ear ear in Dm.each<mob_oranges_ear>(hearers(furthest_range, this)))
        {
            foreach (atom glowie in Dm.each<atom>(ear.references))
                Dm.append(dot, light_sources[glowie]);
        }

        foreach (mob_oranges_ear remaining_ear in Dm.each<mob_oranges_ear>(assigned_oranges_ears))
            remaining_ear.unassign();
        return dot;
    }

    // Returns a boolean whether the turf is on soft lighting.
    // Soft lighting being the threshold at which point the overlay considers
    // itself as too dark to allow sight and see_in_dark becomes useful.
    // So basically if this returns true the tile is unlit black.
    // tg: code/modules/lighting/lighting_turf.dm:117
    public virtual object is_softly_lit()
    {
        if (!Dm.truthy(dm_lighting_object)) // DEVIATION(DV85)
            return FALSE;

        return (Dm.truthy(luminosity) || Dm.truthy(get_dynamic_lumcount())) ? FALSE : TRUE;
    }

    ///Proc to add movable sources of opacity on the turf and let it handle lighting code.
    // tg: code/modules/lighting/lighting_turf.dm:125
    public virtual void add_opacity_source(atom_movable new_source = null)
    {
        LAZYADD(ref opacity_sources, new_source);
        if (Dm.truthy(opacity))
            return;
        recalculate_directional_opacity();
    }

    ///Proc to remove movable sources of opacity on the turf and let it handle lighting code.
    // tg: code/modules/lighting/lighting_turf.dm:133
    public virtual void remove_opacity_source(atom_movable old_source = null)
    {
        LAZYREMOVE(ref opacity_sources, old_source);
        if (Dm.truthy(opacity)) //Still opaque, no need to worry on updating.
            return;
        recalculate_directional_opacity();
    }

    ///Calculate on which directions this turfs block view.
    // tg: code/modules/lighting/lighting_turf.dm:141
    public virtual object recalculate_directional_opacity()
    {
        float dot = directional_opacity;
        if (Dm.truthy(opacity))
        {
            directional_opacity = ALL_CARDINALS;
            if (dot != directional_opacity)
                reconsider_lights();
            return dot;
        }
        directional_opacity = NONE;
        if (opacity_sources != null)
        {
            foreach (atom_movable opacity_source in Dm.each<atom_movable>(opacity_sources))
            {
                if (Dm.truthy(Dm.bitand(opacity_source.flags_1, ON_BORDER_1)))
                    Dm.combine(ref directional_opacity, opacity_source.dir);
                else //If fulltile and opaque, then the whole tile blocks view, no need to continue checking.
                {
                    directional_opacity = ALL_CARDINALS;
                    break;
                }
            }
        }
        else
        {
            foreach (atom_movable content in Dm.each<atom_movable>(contents))
            {
                if (Dm.truthy(content._listen_lookup?[COMSIG_TURF_NO_LONGER_BLOCK_LIGHT]))
                    content._SendSignal(COMSIG_TURF_NO_LONGER_BLOCK_LIGHT, Dm.list(content));
            }
        }
        if (dot != directional_opacity && (dot == ALL_CARDINALS || directional_opacity == ALL_CARDINALS))
            reconsider_lights(); //The lighting system only cares whether the tile is fully concealed from all directions or not.
        return dot;
    }

    ///Transfer the lighting of one area to another
    // tg: code/modules/lighting/lighting_turf.dm:163
    public virtual void transfer_area_lighting(area old_area = null, area new_area = null)
    {
        if (Dm.truthy(SSlighting?.initialized) && !Dm.truthy(space_lit))
        {
            if (new_area.static_lighting != old_area.static_lighting)
            {
                if (Dm.truthy(new_area.static_lighting))
                    lighting_build_overlay();
                else
                    lighting_clear_overlay();
            }
        }

        // tg: :172-186  the area's lighting_effects overlay on an offset plane and the starlight overlay of a space_lit turf:
        // presentation the client draws (D24; area_has_base_lighting stays FALSE, lighting_area.cs)
    }
}
