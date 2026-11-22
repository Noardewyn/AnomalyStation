// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/_closed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of the wave-4 scope (package B): the W4.0 contracts wrote the var overrides of
// /turf/closed (rules 10.2); its procs need modules of later milestones. The [DmPath] of /turf/closed is on
// __HELPERS/icon_smoothing.cs, the earliest .dme file that declares something on /turf/closed (rules 2.3).

// tg: code/game/turfs/closed/_closed.dm:1
public partial class turf_closed : turf
{
    // tg: code/game/turfs/closed/_closed.dm:2-12
    public turf_closed()
    {
        layer = CLOSED_TURF_LAYER;
        plane = WALL_PLANE;
        abstract_type = typeof(turf_closed);
        turf_flags = IS_SOLID;
        opacity = TRUE;
        density = TRUE;
        blocks_air = TRUE;
        init_air = FALSE;
        rad_insulation = RAD_MEDIUM_INSULATION;
        pass_flags_self = PASSCLOSEDTURF;
        tacmap_color = TACMAP_BLACK;
    }

    // tg: code/game/turfs/closed/_closed.dm:14  AfterChange(): not declared, its root /turf/proc/AfterChange
    // (game/turfs/change_turf.dm) is translated with ChangeTurf (M3, M5).

    // tg: code/game/turfs/closed/_closed.dm:18
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        return FALSE;
    }

    // tg: code/game/turfs/closed/_closed.dm:21
    public override object examine_descriptor(mob user = null)
    {
        return "wall";
    }

    /**
     * Some turfs (mineral) are big, so they need to be on the game plane at a high layer
     * But they're also turfs, so we need to cut them out from the light mask plane
     * So we draw them as if they were on the game plane, and then overlay a copy onto
     * The wall plane (so emissives/light masks behave)
     * I am so sorry
     */
    // tg: code/game/turfs/closed/_closed.dm:31
    public virtual void add_large_wall_overlay(object wall_icon = null, object wall_state = null)
    {
        mutable_appearance wall_overlay = add_large_wall_overlay__wall_overlays[Dm.text("[]-[]", wall_icon, wall_state)] as mutable_appearance;
        if (!Dm.truthy(wall_overlay))
        {
            wall_overlay = mutable_appearance(Dm.resource("icons/turf/mining.dmi"), (string)wall_state, appearance_flags: RESET_TRANSFORM) as mutable_appearance;
            add_large_wall_overlay__wall_overlays[Dm.text("[]-[]", wall_icon, wall_state)] = wall_overlay;
        }
        wall_overlay.plane = MUTATE_PLANE(WALL_PLANE, this);
        Dm.append(overlays, wall_overlay);
    }

    // tg: code/game/turfs/closed/_closed.dm:32  add_large_wall_overlay's var/static/list/mutable_appearance/wall_overlays
    private static ref DmList add_large_wall_overlay__wall_overlays => ref TgState.Current.turf_closed__add_large_wall_overlay__wall_overlays;
}

public sealed partial class TgState
{
    // tg: code/game/turfs/closed/_closed.dm:32  /turf/closed/add_large_wall_overlay's var/static/list/wall_overlays (gaps G2, D291)
    internal DmList turf_closed__add_large_wall_overlay__wall_overlays = Dm.list();
}
