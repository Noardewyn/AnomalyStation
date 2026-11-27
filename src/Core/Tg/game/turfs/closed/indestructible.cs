// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/indestructible.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the base type /turf/closed/indestructible (the toy wall) translated: its var overrides (W4.0 contracts,
// rules 10.2) and its procs (:11-23, package B, wave 4); the subtypes of indestructible.dm (:25-409) come with the map
// (M3). Gaps BR1 (D339): the dense rock's Initialize (:268).

// tg: code/game/turfs/closed/indestructible.dm:1
[DmPath("/turf/closed/indestructible")]
public partial class turf_closed_indestructible : turf_closed
{
    // tg: code/game/turfs/closed/indestructible.dm:2-8
    public turf_closed_indestructible()
    {
        name = "wall";
        desc = "Effectively impervious to conventional methods of destruction.";
        icon = Dm.resource("icons/turf/walls.dmi");
        explosive_resistance = 50;
        rust_resistance = RUST_RESISTANCE_ABSOLUTE;
        turf_flags = Dm.bitor(IS_SOLID, NO_RUST);
        baseturfs = typeof(turf_open_indestructible_plating);
    }

    // tg: code/game/turfs/closed/indestructible.dm:11  TerraformTurf(path, new_baseturf, flags, defer_change, ignore_air):
    // not declared, its root /turf/proc/TerraformTurf (game/turfs/change_turf.dm) is translated with ChangeTurf (M3, M5).
    // tg: code/game/turfs/closed/indestructible.dm:14
    [DmParams("acidpwr", "acid_volume", "acid_id")]
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/turfs/closed/indestructible.dm:17
    public override async DmTask<object> Melt()
    {
        to_be_destroyed = FALSE;
        return this;
    }

    // tg: code/game/turfs/closed/indestructible.dm:21  singularity_act(): not declared yet, translated with the singularity
    // (M5x slice 5, D149; its /atom root is declared, M5x slice 1).
}

// tg: code/game/turfs/closed/indestructible.dm:21 (interleaved types, rules 1.9; M5x slice 5)
public partial class turf_closed_indestructible : turf_closed
{
    // tg: code/game/turfs/closed/indestructible.dm:21
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}

// Gaps BR1 (D339): the dense rock of the virtual domains (grasslands hunt) draws its large wall overlay
// tg: code/game/turfs/closed/indestructible.dm:246
public partial class turf_closed_indestructible_rock : turf_closed_indestructible
{
    // tg: code/game/turfs/closed/indestructible.dm:268
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_large_wall_overlay(Dm.resource("icons/turf/mining.dmi"), wall_icon_state);
        return dot;
    }
}

// Lavaland L1 (D304): the necropolis walls of the boss arenas show basalt where they meet the floor
// tg: code/game/turfs/closed/indestructible.dm:329
public partial class turf_closed_indestructible_riveted_boss : turf_closed_indestructible_riveted
{
    // tg: code/game/turfs/closed/indestructible.dm:347
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi");
        underlay_appearance.icon_state = "basalt";
        return TRUE;
    }
}

// tg: code/game/turfs/closed/indestructible.dm:305  (Lavaland's gluttony ruin)
public partial class turf_closed_indestructible_necropolis : turf_closed_indestructible
{
    // tg: code/game/turfs/closed/indestructible.dm:312
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi");
        underlay_appearance.icon_state = "necro1";
        return TRUE;
    }
}

// tg: code/game/turfs/closed/indestructible.dm:352  (the hierophant's arena)
public partial class turf_closed_indestructible_riveted_hierophant : turf_closed_indestructible_riveted
{
    // tg: code/game/turfs/closed/indestructible.dm:361
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        object dot = base.set_smoothed_icon_state(new_junction);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/turfs/closed/indestructible.dm:365
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // . += emissive_appearance('icons/turf/walls/hierophant_wall_e.dmi', icon_state, src): the runes' glow on the emissive plane
        // (DEVIATION DV12)
        return dot;
    }
}
