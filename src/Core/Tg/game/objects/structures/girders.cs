// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/girders.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 4 (walls, girders, floors, lattice): the girder's construction and deconstruction (the wall recipes through
// /datum/element/uses_girder_wall_recipes, plasteel reinforcing, the screwdriver, wirecutter, wrench and welder steps,
// replace_girder, the projectile passage, atom_deconstruct). Generated live-partial stubs (docs/ai/specs/live-partial.txt,
// "M5 slice 4"): examine (M9), CanAStarPass (bot pathfinding, M10+); narsie_act since gaps ST5. E13: rcd_vals and rcd_act (the
// RCD). The var blocks are generated.

// tg: code/game/objects/structures/girders.dm:1
public partial class obj_structure_girder : obj_structure
{
    // tg: code/game/objects/structures/girders.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_uses_girder_wall_recipes)));
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:37
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        switch (state)
        {
            case GIRDER_REINF:
                Dm.append(ref dot, span_notice(I18n.T("The support struts are <b>screwed</b> in place.")));
                break;
            case GIRDER_REINF_STRUTS:
                Dm.append(ref dot, span_notice(I18n.T("The support struts are <i>unscrewed</i> and the inner <b>grille</b> is intact.")));
                break;
            case GIRDER_NORMAL:
                if (Dm.truthy(can_displace))
                    Dm.append(ref dot, span_notice(I18n.T("The bolts are <b>wrenched</b> in place.")));
                break;
            case GIRDER_DISPLACED:
                Dm.append(ref dot, span_notice(I18n.T("The bolts are <i>loosened</i>, but the <b>screws</b> are holding [] together.", this)));
                break;
            case GIRDER_TRAM:
                Dm.append(ref dot, span_notice(I18n.T("[] is designed for tram usage. Deconstructed with a screwdriver!", this)));
                break;
        }
        if (Dm.truthy(can_weld_apart))
            Dm.append(ref dot, span_notice(I18n.T("The frame looks weak enough to be <b>welded</b> apart.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The frame could be sliced apart with a <b>plasmacutter</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:56
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (istype(tool, typeof(obj_item_stack_sheet_plasteel)))
        {
            if (Dm.truthy(await try_construction_step(user, tool, 5 * SECONDS, req_state: GIRDER_NORMAL, start_alert: I18n.T("reinforcing frame..."), amount: 1)))
            {
                await replace_girder(typeof(obj_structure_girder_reinforced));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }
        return null;
    }

    // tg: code/game/objects/structures/girders.dm:65
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        switch (state)
        {
            case GIRDER_TRAM:
                if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_TRAM, start_alert: I18n.T("disassembling frame..."))))
                {
                    await deconstruct(disassembled: TRUE);
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
            case GIRDER_DISPLACED:
                if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_DISPLACED, start_alert: I18n.T("disassembling frame..."))))
                {
                    await deconstruct(disassembled: TRUE);
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
            case GIRDER_REINF:
                if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_REINF, start_alert: I18n.T("unsecuring support struts..."))))
                {
                    state = GIRDER_REINF_STRUTS;
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
            case GIRDER_REINF_STRUTS:
                if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_REINF_STRUTS, start_alert: I18n.T("securing support struts..."))))
                {
                    state = GIRDER_REINF;
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
        }
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:85
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_REINF_STRUTS, start_alert: I18n.T("removing inner grille..."))))
        {
            Dm.@new<obj_item_stack_sheet_plasteel>(get_turf(this));
            await replace_girder(typeof(obj_structure_girder));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:92
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(can_displace))
        {
            balloon_alert(user, I18n.T("no bolts!"));
            return dot;
        }
        switch (state)
        {
            case GIRDER_NORMAL:
                if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_NORMAL, start_alert: I18n.T("unsecuring frame..."))))
                {
                    await replace_girder(typeof(obj_structure_girder_displaced));
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
            case GIRDER_DISPLACED:
                if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, req_state: GIRDER_DISPLACED, start_alert: I18n.T("securing frame..."))))
                {
                    await replace_girder(typeof(obj_structure_girder));
                    return ITEM_INTERACT_SUCCESS;
                }
                break;
        }
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:107
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        // Plasmacutters can always slice apart girders.
        if (!Dm.truthy(can_weld_apart) && !istype(tool, typeof(obj_item_gun_energy_plasmacutter)))
        {
            balloon_alert(user, I18n.T("can't weld apart!"));
            return dot;
        }
        if (Dm.truthy(await try_construction_step(user, tool, 4 * SECONDS, start_alert: I18n.T("slicing apart..."))))
        {
            await deconstruct(disassembled: TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:117
    public virtual async DmTask<object> try_construction_step(mob_living user = null, obj_item tool = null, object delay = null, object req_state = null, object req_floor = null, object start_alert = null, float? volume = null, float? amount = null)
    {
        volume ??= 100;
        amount ??= 0;
        if (!Dm.truthy(check_state(user, req_state, req_floor)))
            return FALSE;

        balloon_alert(user, start_alert);

        // tg: code/game/objects/structures/girders.dm:123
        add_fingerprint(user);
        tool.add_fingerprint(user);

        // tg passes req_floor as check_state's third argument, req_anchored (girders.dm:126): kept
        return await tool.use_tool(this, user, delay, amount, volume, Dm.@new<datum_callback>(this, nameof(check_state), user, req_state, req_floor));
    }

    // tg: code/game/objects/structures/girders.dm:128
    public virtual object check_state(mob_living user = null, object req_state = null, object req_anchored = null, object req_floor = null)
    {
        if (req_state != null && !Dm.eq(req_state, state))
            return FALSE;
        if (Dm.truthy(req_floor) && !isfloorturf(loc))
        {
            balloon_alert(user, I18n.T("needs a floor!"));
            return FALSE;
        }
        return TRUE;
    }

    // We do this instead of state handling because of the large number of variables we would otherwise need to keep track of.
    // That said, ultimately, it would be a better solution for all of it to just be [/obj/structure/girder] at base.
    // For example right now if you paint a girder and use a wrench on it, it will magically lose that paint.
    // tg: code/game/objects/structures/girders.dm:139
    public virtual async DmTask replace_girder(object girder_type = null)
    {
        obj_structure_girder new_girder = Dm.@new((Type)girder_type, loc) as obj_structure_girder;
        // tg: code/game/objects/structures/girders.dm:141
        transfer_fingerprints_to(new_girder);
        new_girder.update_integrity(new_girder.max_integrity * (Dm.num(atom_integrity) / max_integrity));
        qdel(this);
    }

    // tg: code/game/objects/structures/girders.dm:145
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(Dm.bitand(mover.pass_flags, PASSGRILLE)) || isprojectile(mover))
            return Dm.num(prob(projectile_pass_chance));
        return dot;
    }

    // tg: code/game/objects/structures/girders.dm:157
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled) || Dm.truthy(always_drop_stack))
            Dm.@new(stack_type, drop_location(), stack_amount);
        else
        {
            Type remains = (Type)pick(typeof(obj_item_stack_rods), stack_type);
            Dm.@new(remains, drop_location());
        }
    }
}

// tg: code/game/objects/structures/girders.dm:223 (E13: the RCD)
public partial class obj_structure_girder : obj_structure
{
    // tg: code/game/objects/structures/girders.dm:223
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        object sw__1 = the_rcd.mode;
        if (Dm.eq(sw__1, RCD_TURF))
        {
            if (the_rcd.rcd_design_path != typeof(turf_open_floor_plating_rcd))
                return FALSE;

            return rcd_result_with_memory(
                Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 8)),
                get_turf(this), RCD_MEMORY_WALL);
        }
        else if (Dm.eq(sw__1, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 13));
        return FALSE;
    }

    // tg: code/game/objects/structures/girders.dm:237
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_TURF))
        {
            if (the_rcd.rcd_design_path != typeof(turf_open_floor_plating_rcd))
                return FALSE;

            turf T = get_turf(this);
            await T.place_on_top(typeof(turf_closed_wall));
            qdel(this);
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_structure_girder : obj_structure
{
    // tg: code/game/objects/structures/girders.dm:150
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(Dm.bitand(pass_info.pass_flags, PASSGRILLE)))
            return TRUE;
        return FALSE;
    }
}

// Gaps ST5 (D333): Nar'Sie's touch (a cult girder).
public partial class obj_structure_girder : obj_structure
{
    // tg: code/game/objects/structures/girders.dm:164
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await replace_girder(typeof(obj_structure_girder_cult));
        return null;
    }
}

// tg: code/game/objects/structures/girders.dm:200
public partial class obj_structure_girder_cult : obj_structure_girder
{
    // tg: code/game/objects/structures/girders.dm:220  (gaps ST5)
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}
