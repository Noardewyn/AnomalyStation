// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/wall/reinf_walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /turf/closed/wall/r_wall's update_icon and update_icon_state (M3 P4: the reinforced wall's picture follows its
// smoothing) and its deconstruction (M5 slice 4: try_decon's seven stages and their reversals, devastate_wall); the
// hulks (M10+) and rust (M10+) are generated
// live-partial stubs (docs/ai/specs/live-partial.txt's P4 section); E13: rcd_vals and rcd_act (the RCD). The plastitanium subtypes
// wait for their own procs.

// tg: code/game/turfs/closed/wall/reinf_walls.dm:1
public partial class turf_closed_wall_r_wall : turf_closed_wall
{
    // tg: code/game/turfs/closed/wall/reinf_walls.dm:26
    public override object deconstruction_hints(mob user = null)
    {
        switch (d_state)
        {
            case INTACT:
                return span_notice(I18n.T("The outer <b>grille</b> is fully intact."));
            case SUPPORT_LINES:
                return span_notice(I18n.T("The outer <i>grille</i> has been cut, and the support lines are <b>screwed</b> securely to the outer cover."));
            case COVER:
                return span_notice(I18n.T("The support lines have been <i>unscrewed</i>, and the metal cover is <b>welded</b> firmly in place."));
            case CUT_COVER:
                return span_notice(I18n.T("The metal cover has been <i>sliced through</i>, and is <b>connected loosely</b> to the girder."));
        }
        return null;
    }

    // tg: code/game/turfs/closed/wall/reinf_walls.dm:43
    public override async DmTask devastate_wall()
    {
        Dm.@new(sheet_type, this, sheet_amount);
        Dm.@new<obj_item_stack_sheet_iron>(this, 2);
    }

    // TURF-IDENTITY: after a timed step tg asks istype(src, /turf/closed/wall/r_wall) and d_state, which read the turf at this
    // location; when the r-wall was replaced during the step the port's src is a deleted turf off the map, and a replacing
    // r-wall would start INTACT, which none of the checked states is: the step is abandoned either way.
    private bool dm_r_wall_at(float state) => !QDELETED(this) && istype(this, typeof(turf_closed_wall_r_wall)) && d_state == state;

    // tg: code/game/turfs/closed/wall/reinf_walls.dm:50
    public override async DmTask<object> try_decon(obj_item W = null, mob user = null, params object[] __extra)
    {
        mob_living living = user as mob_living;
        //DECONSTRUCTION
        switch (d_state)
        {
            case INTACT:
                if (Dm.eq(W.tool_behaviour, TOOL_WIRECUTTER))
                {
                    W.play_tool_sound(this, 100);
                    d_state = SUPPORT_LINES;
                    update_appearance();
                    to_chat(user, span_notice(I18n.T("You cut the outer grille.")));
                    return TRUE;
                }
                break;

            case SUPPORT_LINES:
                if (Dm.eq(W.tool_behaviour, TOOL_SCREWDRIVER))
                {
                    to_chat(user, span_notice(I18n.T("You begin unsecuring the support lines...")));
                    if (Dm.truthy(await W.use_tool(this, living, 40, volume: 100)))
                    {
                        if (!dm_r_wall_at(SUPPORT_LINES))
                            return TRUE;
                        d_state = COVER;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You unsecure the support lines.")));
                    }
                    return TRUE;
                }
                else if (Dm.eq(W.tool_behaviour, TOOL_WIRECUTTER))
                {
                    W.play_tool_sound(this, 100);
                    d_state = INTACT;
                    update_appearance();
                    to_chat(user, span_notice(I18n.T("You repair the outer grille.")));
                    return TRUE;
                }
                break;

            case COVER:
                if (Dm.eq(W.tool_behaviour, TOOL_WELDER))
                {
                    if (!Dm.truthy(W.tool_start_check(living, amount: 2, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                        return null;
                    to_chat(user, span_notice(I18n.T("You begin slicing through the metal cover...")));
                    if (Dm.truthy(await W.use_tool(this, living, 60, volume: 100)))
                    {
                        if (!dm_r_wall_at(COVER))
                            return TRUE;
                        d_state = CUT_COVER;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You press firmly on the cover, dislodging it.")));
                    }
                    return TRUE;
                }

                if (Dm.eq(W.tool_behaviour, TOOL_SCREWDRIVER))
                {
                    to_chat(user, span_notice(I18n.T("You begin securing the support lines...")));
                    if (Dm.truthy(await W.use_tool(this, living, 40, volume: 100)))
                    {
                        if (!dm_r_wall_at(COVER))
                            return TRUE;
                        d_state = SUPPORT_LINES;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("The support lines have been secured.")));
                    }
                    return TRUE;
                }
                break;

            case CUT_COVER:
                if (Dm.eq(W.tool_behaviour, TOOL_CROWBAR))
                {
                    to_chat(user, span_notice(I18n.T("You struggle to pry off the cover...")));
                    if (Dm.truthy(await W.use_tool(this, living, 100, volume: 100)))
                    {
                        if (!dm_r_wall_at(CUT_COVER))
                            return TRUE;
                        d_state = ANCHOR_BOLTS;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You pry off the cover.")));
                    }
                    return TRUE;
                }

                if (Dm.eq(W.tool_behaviour, TOOL_WELDER))
                {
                    if (!Dm.truthy(W.tool_start_check(living, amount: 2, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                        return null;
                    to_chat(user, span_notice(I18n.T("You begin welding the metal cover back to the frame...")));
                    if (Dm.truthy(await W.use_tool(this, living, 60, volume: 100)))
                    {
                        if (!dm_r_wall_at(CUT_COVER))
                            return TRUE;
                        d_state = COVER;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("The metal cover has been welded securely to the frame.")));
                    }
                    return TRUE;
                }
                break;

            case ANCHOR_BOLTS:
                if (Dm.eq(W.tool_behaviour, TOOL_WRENCH))
                {
                    to_chat(user, span_notice(I18n.T("You start loosening the anchoring bolts which secure the support rods to their frame...")));
                    if (Dm.truthy(await W.use_tool(this, living, 40, volume: 100)))
                    {
                        if (!dm_r_wall_at(ANCHOR_BOLTS))
                            return TRUE;
                        d_state = SUPPORT_RODS;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You remove the bolts anchoring the support rods.")));
                    }
                    return TRUE;
                }

                if (Dm.eq(W.tool_behaviour, TOOL_CROWBAR))
                {
                    to_chat(user, span_notice(I18n.T("You start to pry the cover back into place...")));
                    if (Dm.truthy(await W.use_tool(this, living, 20, volume: 100)))
                    {
                        if (!dm_r_wall_at(ANCHOR_BOLTS))
                            return TRUE;
                        d_state = CUT_COVER;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("The metal cover has been pried back into place.")));
                    }
                    return TRUE;
                }
                break;

            case SUPPORT_RODS:
                if (Dm.eq(W.tool_behaviour, TOOL_WELDER))
                {
                    if (!Dm.truthy(W.tool_start_check(living, amount: 2, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                        return null;
                    to_chat(user, span_notice(I18n.T("You begin slicing through the support rods...")));
                    if (Dm.truthy(await W.use_tool(this, living, 100, volume: 100)))
                    {
                        if (!dm_r_wall_at(SUPPORT_RODS))
                            return TRUE;
                        d_state = SHEATH;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You slice through the support rods.")));
                    }
                    return TRUE;
                }

                if (Dm.eq(W.tool_behaviour, TOOL_WRENCH))
                {
                    to_chat(user, span_notice(I18n.T("You start tightening the bolts which secure the support rods to their frame...")));
                    W.play_tool_sound(this, 100);
                    if (Dm.truthy(await W.use_tool(this, living, 40)))
                    {
                        if (!dm_r_wall_at(SUPPORT_RODS))
                            return TRUE;
                        d_state = ANCHOR_BOLTS;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You tighten the bolts anchoring the support rods.")));
                    }
                    return TRUE;
                }
                break;

            case SHEATH:
                if (Dm.eq(W.tool_behaviour, TOOL_CROWBAR))
                {
                    to_chat(user, span_notice(I18n.T("You struggle to pry off the outer sheath...")));
                    if (Dm.truthy(await W.use_tool(this, living, 100, volume: 100)))
                    {
                        if (!dm_r_wall_at(SHEATH))
                            return TRUE;
                        to_chat(user, span_notice(I18n.T("You pry off the outer sheath.")));
                        await dismantle_wall();
                    }
                    return TRUE;
                }

                if (Dm.eq(W.tool_behaviour, TOOL_WELDER))
                {
                    if (!Dm.truthy(W.tool_start_check(living, amount: 0, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                        return null;
                    to_chat(user, span_notice(I18n.T("You begin welding the support rods back together...")));
                    if (Dm.truthy(await W.use_tool(this, living, 100, volume: 100)))
                    {
                        if (!dm_r_wall_at(SHEATH))
                            return TRUE;
                        d_state = SUPPORT_RODS;
                        update_appearance();
                        to_chat(user, span_notice(I18n.T("You weld the support rods back together.")));
                    }
                    return TRUE;
                }
                break;
        }
        return FALSE;
    }

    // tg: code/game/turfs/closed/wall/reinf_walls.dm:193
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        if (d_state != INTACT)
        {
            smoothing_flags = NONE;
            return dot;
        }
        if (!Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)))
            return dot;
        smoothing_flags = SMOOTH_BITMASK;
        QUEUE_SMOOTH_NEIGHBORS(this);
        QUEUE_SMOOTH(this);
        return dot;
    }

    // We don't react to smoothing changing here because this else exists only to "revert" intact changes
    // tg: code/game/turfs/closed/wall/reinf_walls.dm:205
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (d_state != INTACT)
        {
            icon = Dm.resource("icons/turf/walls/reinforced_states.dmi");
            icon_state = Dm.text("[]-[]", base_decon_state, d_state);
        }
        else
        {
            icon = Dm.initial(this, (turf_closed_wall_r_wall p) => p.icon);
            icon_state = Dm.text("[]-[]", base_icon_state, smoothing_junction);
        }
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/turfs/closed/wall/reinf_walls.dm:214 (interleaved types, rules 1.9; M5x slice 5)
public partial class turf_closed_wall_r_wall : turf_closed_wall
{
    // tg: code/game/turfs/closed/wall/reinf_walls.dm:214
    public override async DmTask wall_singularity_pull(object current_size = null)
    {
        if (Dm.num(current_size) >= STAGE_FIVE)
        {
            if (prob(30))
                await dismantle_wall();
        }
    }
}

// tg: code/game/turfs/closed/wall/reinf_walls.dm:219 (E13: the RCD)
public partial class turf_closed_wall_r_wall : turf_closed_wall
{
    // tg: code/game/turfs/closed/wall/reinf_walls.dm:219
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

    // tg: code/game/turfs/closed/wall/reinf_walls.dm:228
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.truthy(the_rcd.canRturf) || Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_WALLFRAME))
            return await base.rcd_act(user, the_rcd, rcd_data, __extra);
        return null;
    }
}
