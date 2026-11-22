// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/reinforced_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (engineering package E4): /turf/open/floor/engine's procs (the reinforced floor of the engine, 369 tiles on MetaStation)
// and the cult floor's. Not here: the engine floor's examine (a live-partial row, D264) and the cult floor's overlay and glow
// (Dm.Deferred leaves, the cult antagonist). The gas-filled, airless, hull and insulation variants hold only vars (the
// generated part) and turn live with their parent.

// tg: code/game/turfs/open/floor/reinforced_floor.dm:1
public partial class turf_open_floor_engine : turf_open_floor
{
    // tg: code/game/turfs/open/floor/reinforced_floor.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The reinforcement rods are <b>wrenched</b> firmly in place.")));
        return dot;
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:27
    public override object break_tile()
    {
        return null; //unbreakable
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:30
    public override object burn_tile()
    {
        return null; //unburnable
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:33
    public override async DmTask<object> make_plating(float? force = null)
    {
        force ??= FALSE;
        if (Dm.truthy(force))
            return await base.make_plating(force);
        return null; //unplateable
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:38
    public override async DmTask<object> try_replace_tile(obj_item_stack_tile tile = null, mob user = null, DmList modifiers = null)
    {
        return null;
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:41
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:44
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        to_chat(user, span_notice(I18n.T("You begin removing rods...")));
        // TURF-IDENTITY: tg's src is whatever turf is at this cell after the wait
        float cell_x = x, cell_y = y, cell_z = z;
        if (Dm.truthy(await tool.use_tool(this, user, 30, volume: 80)))
        {
            turf_open_floor_engine src = locate(cell_x, cell_y, cell_z) as turf_open_floor_engine;
            if (src == null)
                return TRUE;
            if (Dm.truthy(src.floor_tile))
                Dm.@new((Type)src.floor_tile, src, src.floor_tile_amount);
            await src.ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        }
        return TRUE;
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:55  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of
    // waiting-model.md 5.4; ScrapeAway may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        if (target == this)
        {
            await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        if (Dm.truthy(is_explosion_shielded(severity)))
            return FALSE;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                if (prob(80))
                {
                    if (!ispath(baseturf_at_depth(2), typeof(turf_open_floor)))
                        await attempt_lattice_replacement();
                    else
                        await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR);
                }
                else if (prob(50))
                    await ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR);
                else
                    await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
                break;
            case EXPLODE_HEAVY:
                if (prob(50))
                    await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
                break;
        }

        return TRUE;
    }

    // Contents *under* the reinforced flooring is protected from explosions (unless it's devastate level)
    // tg: code/game/turfs/open/floor/reinforced_floor.dm:80
    public override object can_propagate_explosion(atom_movable some_thing = null, object severity = null)
    {
        return Dm.num(Dm.num(severity) == EXPLODE_DEVASTATE || !HAS_TRAIT(some_thing, TRAIT_UNDERFLOOR));
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:83
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        // TURF-IDENTITY: the pull may shear the tile and replace this turf; tg goes on with the turf that is at the cell now
        float cell_x = x, cell_y = y, cell_z = z;
        await base.singularity_pull(singularity, current_size);
        turf_open_floor src = locate(cell_x, cell_y, cell_z) as turf_open_floor; // tg reads floor_tile of any turf; a non-floor turf is a runtime error there
        if (src == null)
            return null;
        if (Dm.num(current_size) >= STAGE_FIVE)
        {
            if (Dm.truthy(src.floor_tile))
            {
                if (prob(30))
                {
                    Dm.@new((Type)src.floor_tile, src);
                    await src.make_plating(TRUE);
                }
            }
            else if (prob(30))
                await src.attempt_lattice_replacement();
        }
        return null;
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:93
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }
}

// tg: code/game/turfs/open/floor/reinforced_floor.dm:190
public partial class turf_open_floor_engine_cult : turf_open_floor_engine
{
    // tg: code/game/turfs/open/floor/reinforced_floor.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = "plating"; //we're redefining the base icon_state here so that the Conceal/Reveal Presence spell works for cultists

        if (!Dm.truthy(mapload))
            Dm.Deferred("code/game/turfs/open/floor/reinforced_floor.dm:203", "Initialize: new /obj/effect/temp_visual/cult/turf/floor(src): the cult floor's glow (the cult antagonist, M10+)");

        Dm.Deferred("code/game/turfs/open/floor/reinforced_floor.dm:205", "Initialize: realappearance = new /obj/effect/cult_turf(src): the overlay that hides the floor from mesons (the cult antagonist, M10+)");
        return dot;
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:208
    public override object Destroy(float? force = null)
    {
        be_removed();
        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:212
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        if (!Dm.eq(path, type))
            be_removed();
        return await base.ChangeTurf(path, new_baseturfs, flags);
    }

    // tg: code/game/turfs/open/floor/reinforced_floor.dm:217
    public virtual void be_removed()
    {
        QDEL_NULL(ref realappearance);
    }
}
