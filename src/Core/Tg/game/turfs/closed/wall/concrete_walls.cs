// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/wall/concrete_walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the MetaStation leftovers: concrete): the concrete and hexacrete walls a concrete reagent pours over a girder,
// curing over a minute (a grey colour filter while wet), broken with a mining tool. The hexacrete wall's rust (r_wall's rust_turf)
// is a live-partial row, as the reinforced wall's.

// tg: code/game/turfs/closed/wall/concrete_walls.dm:1
public partial class turf_closed_wall_concrete : turf_closed_wall
{
    // tg: code/game/turfs/closed/wall/concrete_walls.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        check_harden();
        return dot;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:31
    public override object deconstruction_hints(mob user = null)
    {
        DmList dot = Dm.list();
        Dm.append(ref dot, span_notice(I18n.T("[] look[] like you could smash [] with <b>[]</b>.", p_They(), p_s(), p_them(), tool_behaviour_name(TOOL_MINING))));
        // DM's switch ranges, first match: 0.8 to 0.99, 0.4 to 0.8, 0 to 0.4
        if (harden_lvl >= 0.8f && harden_lvl <= 0.99f)
            Dm.append(ref dot, I18n.T("[] look[] nearly dry.", p_They(), p_s()));
        else if (harden_lvl >= 0.4f && harden_lvl <= 0.8f)
            Dm.append(ref dot, I18n.T("[] look[] a little wet.", p_They(), p_s()));
        else if (harden_lvl >= 0f && harden_lvl <= 0.4f)
            Dm.append(ref dot, I18n.T("[] look[] freshly poured.", p_They(), p_s()));
        return dot;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:43
    public override async DmTask<object> try_clean(obj_item W = null, mob_living user = null)
    {
        // You can't weld dents on concrete wall
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:47
    public override async DmTask<object> try_decon(obj_item I = null, mob user = null, params object[] __extra)
    {
        if (Dm.eq(I.tool_behaviour, TOOL_MINING))
        {
            to_chat(user, span_notice(I18n.T("You begin breaking the wall...")));
            float cell_x = x, cell_y = y, cell_z = z;
            if (Dm.truthy(await I.use_tool(this, user as mob_living, slicing_duration, volume: 100)))
            {
                // TURF-IDENTITY: tg's src after the sleep is the turf here; a wall replaced meanwhile is not this one
                if (QDELETED(this) || !iswallturf(this))
                    return TRUE;
                // break_prob decreases linearly from 100 to hardness as it cures
                float break_prob = hardness + (1 - harden_lvl) * (100 - hardness);
                if (prob(break_prob))
                {
                    to_chat(user, span_notice(I18n.T("You break the wall.")));
                    await dismantle_wall();
                    // TURF-IDENTITY: tg's src is now the turf dismantle_wall left
                    Dm.@new<obj_effect_decal_cleanable_concrete_dust>(locate(cell_x, cell_y, cell_z));
                }
                else
                {
                    add_dent(WALL_DENT_HIT);
                    playsound(this, break_sound, 50, TRUE);
                }

                return TRUE;
            }
        }

        return FALSE;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:66
    public override async DmTask<object> break_wall()
    {
        atom girder = await base.break_wall() as atom;
        await girder.atom_break();
        return girder;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:71
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        if (!Dm.truthy(dot))
            return dot;
        remove_filter("harden");
        if (harden_lvl == 1)
            return dot;

        float offset = (1 - harden_lvl) * 0.4f;
        float @base = 1 - offset;
        DmList col_filter = Dm.list(@base, 0, 0, 0, @base, 0, 0, 0, @base, offset, offset, offset);
        add_filter("harden", 1, color_matrix_filter(col_filter, FILTER_COLOR_RGB) as DmList);
        return dot;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:83
    public virtual void check_harden()
    {
        harden_lvl = clamp(harden_lvl, 0, 1);
        if (harden_lvl < 1)
        {
            START_PROCESSING(SSobj, this);
            Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        }
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:89
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float time_per_tick = Dm.num(seconds_per_tick) * SECONDS;
        harden_lvl = Math.Min(harden_lvl + (time_per_tick / time_to_harden), 1);
        if (harden_lvl == 1)
            STOP_PROCESSING(SSobj, this);
        Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        return null;
    }
}

// tg: code/game/turfs/closed/wall/concrete_walls.dm:98
public partial class turf_closed_wall_concrete_reinforced : turf_closed_wall_concrete
{
    // tg: code/game/turfs/closed/wall/concrete_walls.dm:112  call(src, /turf/closed/wall/r_wall::rcd_vals())(arglist(args)): the
    // reinforced wall's proc run on this wall, its ..() the plain wall's (reinf_walls.dm:215)
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.construction_mode, RCD_WALLFRAME))
            return base.rcd_vals(user, the_rcd, __extra);
        if (!Dm.truthy(the_rcd.canRturf))
            return null;
        object dot = base.rcd_vals(user, the_rcd, __extra);
        if (Dm.truthy(dot))
            ((DmList)dot)["delay"] = Dm.num(((DmList)dot)["delay"]) * RCD_RWALL_DELAY_MULT;
        return dot;
    }

    // tg: code/game/turfs/closed/wall/concrete_walls.dm:115  call(src, /turf/closed/wall/r_wall::rcd_act())(arglist(args))
    // (reinf_walls.dm:228)
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.truthy(the_rcd.canRturf) || Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_WALLFRAME))
            return await base.rcd_act(user, the_rcd, rcd_data, __extra);
        return null;
    }
}
