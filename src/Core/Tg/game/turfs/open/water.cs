// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/water.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The water's own procs with its lazy fishing spot and the Lavaland water's smoothing (Lavaland L1, D304). The
// watery tile's wetting, the scooping and the deep water's swimming are elements that wait for the water package (Dm.Deferred,
// not-translated.md section 251); the immersion mask is BYOND presentation the 3D client draws its own way (DV12, as lava's).
// The hot spring's procs stay inert. The var blocks are generated.

// tg: code/game/turfs/open/water.dm:1
[DmPath("/turf/open/water")]
public partial class turf_open_water : turf_open
{
    // tg: code/game/turfs/open/water.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_inited));
        Dm.Deferred("code/game/turfs/open/water.dm:48", "Initialize: AddElement(/datum/element/watery_tile), the water's wetting (the water package)");
        if (fishing_datum != null)
            add_lazy_fishing(fishing_datum);
        ADD_TRAIT(this, TRAIT_CATCH_AND_RELEASE, INNATE_TRAIT);
        if (reagent_to_extract != null)
            Dm.Deferred("code/game/turfs/open/water.dm:53", "Initialize: AddElement(/datum/element/reagent_scoopable_atom, reagent_to_extract), scooping the water (the water package)");
        return dot;
    }

    ///We lazily add the immerse element when something is spawned or crosses this turf and not before.
    // tg: code/game/turfs/open/water.dm:56
    [SignalHandler]
    public virtual void on_atom_inited(datum source = null, atom_movable movable = null)
    {
        UnregisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON);
        make_immersed(movable);
    }

    // tg: code/game/turfs/open/water.dm:61
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        make_immersed(arrived);
        return dot;
    }

    // tg: code/game/turfs/open/water.dm:65
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        make_immersed(occupant);
    }

    ///Makes this turf immersable, return true if we actually did anything so child procs don't have to repeat our checks
    // tg: code/game/turfs/open/water.dm:70
    public virtual object make_immersed(atom_movable triggering_atom = null)
    {
        if (Dm.truthy(immerse_added))
            return FALSE;
        // DEVIATION(DV12): is_type_in_typecache(triggering_atom, GLOB.immerse_ignored_movable) and
        // AddElement(/datum/element/immerse, immerse_overlay, immerse_overlay_alpha), the vis_contents sinking mask
        immerse_added = TRUE;
        if (Dm.truthy(is_swimming_tile))
            Dm.Deferred("code/game/turfs/open/water.dm:76", "make_immersed: AddElement(/datum/element/swimming_tile, ...), the deep water's swimming and drowning (the water package)");
        return TRUE;
    }

    // tg: code/game/turfs/open/water.dm:79
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON);
        return base.Destroy(force);
    }
}

// Lavaland L1 (D304): Lavaland's water smooths with the basalt, siderite and shale around it and draws their outlines, as the
// smooth lava does (lava.cs).
// tg: code/game/turfs/open/water.dm:141
public partial class turf_open_water_lavaland_atmos_basalt : turf_open_water_lavaland_atmos
{
    // tg: code/game/turfs/open/water.dm:157
    public override void bitmask_smooth()
    {
        base.bitmask_smooth();
        basalt_junction = ALL_SMOOTHING_JUNCTIONS;
        siderite_junction = ALL_SMOOTHING_JUNCTIONS;
        shale_junction = ALL_SMOOTHING_JUNCTIONS;
        // We need to convert basalt/siderite/shale groups into a readable format
        TgState state = TgState.Current;
        if (state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__basalt_group == null)
        {
            SET_SMOOTHING_GROUPS(Dm.add(SMOOTH_GROUP_FLOOR_BASALT, SMOOTH_GROUP_MINERAL_WALLS), ref state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__basalt_group);
            SET_SMOOTHING_GROUPS(Dm.add(SMOOTH_GROUP_FLOOR_SIDERITE, SMOOTH_GROUP_RED_ROCK_WALLS), ref state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__siderite_group);
            SET_SMOOTHING_GROUPS(Dm.add(SMOOTH_GROUP_FLOOR_SHALE, SMOOTH_GROUP_SHALE_WALLS), ref state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__shale_group);
        }
        var basalt_group = (DmList)state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__basalt_group;
        var siderite_group = (DmList)state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__siderite_group;
        var shale_group = (DmList)state.turf_open_water_lavaland_atmos_basalt_bitmask_smooth__shale_group;
        // After smoothing normally we can check our smoothed directions for possible basalt/siderite/shale tiles
        foreach (object check_dir in Dm.each(GLOB.alldirs))
        {
            float junction = Dm.num(Dm.bitor(dir_to_junction(check_dir), all_junctions_of_dir(check_dir)));
            if (!Dm.truthy(Dm.bitand(junction, Dm.num(smoothing_junction))))
                continue;
            var to_smooth = get_step(this, Dm.num(check_dir)) as turf;
            if (to_smooth == null || !Dm.truthy(to_smooth.smoothing_groups))
                continue;
            DmList groups = (DmList)to_smooth.smoothing_groups;
            foreach (object key in Dm.each(groups))
            {
                object group = groups[key];
                if (Dm.truthy(Dm.bitand(group, basalt_group[key])))
                    basalt_junction = Dm.bitand(basalt_junction, Dm.bitnot(junction));
                else if (Dm.truthy(Dm.bitand(group, siderite_group[key])))
                    siderite_junction = Dm.bitand(siderite_junction, Dm.bitnot(junction));
                else if (Dm.truthy(Dm.bitand(group, shale_group[key])))
                    shale_junction = Dm.bitand(shale_junction, Dm.bitnot(junction));
            }
        }
    }

    // tg: code/game/turfs/open/water.dm:183
    public override void smooth_icon__proc()
    {
        base.smooth_icon__proc();
        update_appearance(Dm.bitnot(UPDATE_SMOOTHING));
    }

    // tg: code/game/turfs/open/water.dm:187
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (basalt_junction != ALL_SMOOTHING_JUNCTIONS)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/turf/floors/basalt_outline.dmi"), Dm.text("basalt_outline-[]", basalt_junction)));
        if (siderite_junction != ALL_SMOOTHING_JUNCTIONS)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/turf/floors/siderite_outline.dmi"), Dm.text("siderite_outline-[]", siderite_junction)));
        if (shale_junction != ALL_SMOOTHING_JUNCTIONS)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/turf/floors/shale_outline.dmi"), Dm.text("shale_outline-[]", shale_junction)));
        return dot;
    }

    // tg: code/game/turfs/open/water.dm:196
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi"); // /turf/open/misc/asteroid/basalt::icon
        underlay_appearance.icon_state = "basalt"; // /turf/open/misc/asteroid/basalt::icon_state
        return TRUE;
    }
}

// rules 10.6: the proc-level statics above, made on first use
public sealed partial class TgState
{
    internal object turf_open_water_lavaland_atmos_basalt_bitmask_smooth__basalt_group;
    internal object turf_open_water_lavaland_atmos_basalt_bitmask_smooth__siderite_group;
    internal object turf_open_water_lavaland_atmos_basalt_bitmask_smooth__shale_group;
}

// tg: code/game/turfs/open/water.dm:107  (Lavaland's beach biodome)
public partial class turf_open_water_beach : turf_open_water
{
    // tg: code/game/turfs/open/water.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION, INNATE_TRAIT);
        return dot;
    }
}
