// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file but blob_act (a live-partial row, the blob), so the asteroid and basalt floors (the mined volcanic
// rock's floor) take rods and floor tiles and break in explosions as tg's.

/// Used as a parent type for types that want to allow construction, but do not want to be floors
/// I wish I could use components for turfs at scale
/// Please do not bloat this. Love you <3
// tg: code/game/turfs/open/misc.dm:4
public partial class turf_open_misc : turf_open
{
    // tg: code/game/turfs/open/misc.dm:24
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (istype(tool, typeof(obj_item_stack_rods)))
        {
            await build_with_rods(tool as obj_item_stack_rods, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (ismetaltile(tool))
        {
            await build_with_floor_tiles(tool as obj_item_stack_tile_iron, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/turfs/open/misc.dm:37
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/turfs/open/misc.dm:40  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of waiting-model.md 5.4;
    // ScrapeAway may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.ex_act(severity, target, __extra);

        if (target == this)
        {
            await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        if (Dm.truthy(is_explosion_shielded(severity)))
            return FALSE;

        if (Dm.truthy(target))
            severity = EXPLODE_LIGHT;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR);
                break;
            case EXPLODE_HEAVY:
            {
                float sw__1 = rand(1, 3);
                if (sw__1 >= 1 && sw__1 <= 2)
                    await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR);
                else if (sw__1 == 3)
                {
                    if (prob(80))
                        await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
                    else
                        break_tile();
                    hotspot_expose(1000, CELL_VOLUME);
                }
                break;
            }
            case EXPLODE_LIGHT:
                if (prob(50))
                {
                    break_tile();
                    hotspot_expose(1000, CELL_VOLUME);
                }
                break;
        }

        return TRUE;
    }

    // tg: code/game/turfs/open/misc.dm:72
    public override object is_explosion_shielded(object severity = null)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE)
            return FALSE;
        foreach (obj blocker in Dm.each<obj>(this))
        {
            if (Dm.truthy(blocker.density))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/misc.dm:83
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF))
        {
            if (!Dm.eq(the_rcd.rcd_design_path, typeof(turf_open_floor_plating_rcd)))
                return FALSE;

            return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 3));
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/misc.dm:91
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_TURF))
        {
            if (!Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(turf_open_floor_plating_rcd)))
                return FALSE;

            await place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        return FALSE;
    }
}
