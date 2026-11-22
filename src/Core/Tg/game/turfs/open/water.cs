// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/water.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md). Only the type is declared, but for the Lavaland water below.

// tg: code/game/turfs/open/water.dm:1
[DmPath("/turf/open/water")]
public partial class turf_open_water : turf_open
{
}

// Lavaland L1 (D304): Lavaland's water smooths with the basalt, siderite and shale around it and draws their outlines, as the
// smooth lava does (lava.cs). The water's own procs (the watery tile, immersion, swimming, scooping, fishing) wait for the water
// package; the other water types lack them too.
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
