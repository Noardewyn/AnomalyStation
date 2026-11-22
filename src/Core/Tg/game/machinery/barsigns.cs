// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/barsigns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the bar sign (picking a sign from the list, the area it renames, its neon light, the EMP and
// the emag signs, repairing it with cable, the panel, taking it down) and its wall frame. The sign datums and the var blocks are
// generated (D111). The light mask's emissive overlay is DV12 (the client draws the lit sign).

// tg: code/game/machinery/barsigns.dm:1
public partial class obj_machinery_barsign : obj_machinery
{
    // tg: code/game/machinery/barsigns.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Roundstart/map specific barsigns "belong" in their area and should be renaming it, signs created from wallmounts will not.
        change_area_name = Dm.truthy(mapload) ? TRUE : FALSE;
        set_sign(Dm.@new<datum_barsign_hiddensigns_signoff>());
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:36
    public virtual void set_sign(datum_barsign sign = null)
    {
        if (sign == null)
            return;

        var bar_area = get_area(this) as area;
        if (Dm.truthy(change_area_name) && Dm.truthy(sign.rename_area))
            rename_area(bar_area, sign.name);

        chosen_sign = sign;
        update_appearance();
    }

    // tg: code/game/machinery/barsigns.dm:47
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        var sign = chosen_sign as datum_barsign;
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN))
            && (!Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) || Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            && sign != null && Dm.truthy(sign.icon_state))
            icon_state = Dm.str(sign.icon_state);
        else
            icon_state = "empty";

        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/barsigns.dm:55
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);

        var sign = chosen_sign as datum_barsign;
        if (sign != null && Dm.truthy(sign.desc))
            desc = Dm.str(sign.desc);
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:61
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        var sign = chosen_sign as datum_barsign;
        string initial_name = Dm.initial(GetType(), (obj_machinery_barsign b) => b.name);
        if (sign != null && Dm.truthy(sign.rename_area))
            name = Dm.text("[] ([])", initial_name, sign.name);
        else
            name = Dm.text("[]", initial_name);
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:68
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if ((Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) && !Dm.truthy(Dm.bitand(machine_stat, EMPED))) || Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return dot;

        // DEVIATION(DV12): if(chosen_sign && chosen_sign.light_mask) . += emissive_appearance(icon, "[chosen_sign.icon_state]-light-mask", src),
        // the glow on the emissive plane, is not created; the client lights the sign by its own state.
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:77
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }
        var sign = chosen_sign as datum_barsign;
        if (sign != null && Dm.truthy(sign.neon_color))
            Waiting.NoSleep(set_light(MINIMUM_USEFUL_LIGHT_RANGE, 0.7f, sign.neon_color));
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:85
    public virtual void set_sign_by_name(object sign_name = null)
    {
        foreach (Type sign in Dm.each<Type>(subtypesof(typeof(datum_barsign))))
        {
            if (Dm.eq(Dm.initial(sign, (datum_barsign b) => b.name), sign_name))
            {
                var new_sign = (datum_barsign)Dm.@new(sign);
                set_sign(new_sign);
            }
        }
    }

    // tg: code/game/machinery/barsigns.dm:91
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            set_sign(Dm.@new<datum_barsign_hiddensigns_signoff>());
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:96
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(disassembled))
            Dm.@new(disassemble_result, drop_location());
        else
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
            Dm.@new<obj_item_stack_cable_coil>(drop_location(), 2);
        }
        return null;
    }

    // tg: code/game/machinery/barsigns.dm:103
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
                break;
            case BURN:
                playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }

    // tg: code/game/machinery/barsigns.dm:110
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/barsigns.dm:113
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, "access denied!");
            return dot;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), EMPED))))
        {
            balloon_alert(user, "controls are unresponsive!");
            return dot;
        }
        await pick_sign(user);
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:125
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        tool.play_tool_sound(this);
        panel_open = Dm.truthy(panel_open) ? FALSE : TRUE;
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, "panel opened");
            set_sign(Dm.@new<datum_barsign_hiddensigns_signoff>());
            return ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, "panel closed");

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || chosen_sign == null)
            set_sign(Dm.@new<datum_barsign_hiddensigns_signoff>());
        else
            set_sign(chosen_sign as datum_barsign);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/barsigns.dm:142
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, "open the panel first!");
            return ITEM_INTERACT_BLOCKING;
        }

        tool.play_tool_sound(this);
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        tool.play_tool_sound(this);
        await deconstruct(disassembled: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/barsigns.dm:155
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_blueprints && !Dm.truthy(change_area_name))
        {
            if (!Dm.truthy(panel_open))
            {
                balloon_alert(user, "open the panel first!");
                return ITEM_INTERACT_BLOCKING;
            }

            change_area_name = TRUE;
            balloon_alert(user, "sign registered");
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_cable_coil wire && Dm.truthy(panel_open))
        {
            if (Dm.num(atom_integrity) >= max_integrity)
            {
                balloon_alert(user, "doesn't need repairs!");
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await wire.use(2)))
            {
                balloon_alert(user, "need two cables!");
                return ITEM_INTERACT_BLOCKING;
            }

            balloon_alert(user, "repaired");
            atom_integrity = max_integrity;
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/barsigns.dm:185
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        await set_machine_stat(Dm.bitor(machine_stat, EMPED));
        _addtimer(Dm.@new<datum_callback>(this, nameof(fix_emp), chosen_sign), 60 * SECONDS, file: "code/game/machinery/barsigns.dm", line: 191);
        set_sign(Dm.@new<datum_barsign_hiddensigns_empbarsign>());
        return dot;
    }

    /// Callback to un-emp the sign some time.
    // tg: code/game/machinery/barsigns.dm:195
    public virtual async DmTask fix_emp(datum_barsign sign = null)
    {
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(EMPED)));
        if (sign == null)
            return;

        set_sign(sign);
    }

    // tg: code/game/machinery/barsigns.dm:202
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), EMPED))))
        {
            balloon_alert(user, "controls are unresponsive!");
            return FALSE;
        }

        balloon_alert(user, "illegal barsign loaded");
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_emag_act)), 10 * SECONDS, file: "code/game/machinery/barsigns.dm", line: 208);
        return TRUE;
    }

    /// Timer proc, called after ~10 seconds after [emag_act], since [emag_act] returns a value and cannot sleep
    // tg: code/game/machinery/barsigns.dm:212
    public virtual void finish_emag_act()
    {
        set_sign(Dm.@new<datum_barsign_hiddensigns_syndibarsign>());
    }

    // tg: code/game/machinery/barsigns.dm:215
    public virtual async DmTask pick_sign(mob user = null)
    {
        object picked_name = await tgui_input_list(user, "Available Signage", "Bar Sign", sort_list(get_bar_names() as DmList) as DmList);
        if (picked_name == null)
            return;
        set_sign_by_name(picked_name);
        // tg: barsigns.dm:220  SSblackbox.record_feedback("tally", "barsign_picked", 1, chosen_sign.type): the statistics (the statistics database, not ported)
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/machinery/barsigns.dm:222
    public static object get_bar_names()
    {
        DmList names = Dm.list();
        foreach (Type d in Dm.each<Type>(subtypesof(typeof(datum_barsign))))
        {
            if (!Dm.truthy(Dm.initial(d, (datum_barsign D) => D.hidden)))
                Dm.append(ref names, Dm.initial(d, (datum_barsign D) => D.name));
        }
        return names;
    }
}

// tg: code/game/machinery/barsigns.dm:231
public partial class datum_barsign : datum
{
    // tg: code/game/machinery/barsigns.dm:246
    public override async DmTask New(params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(desc))
            desc = Dm.text("It displays \"[]\".", name);
    }
}

// tg: code/game/machinery/barsigns.dm:560
public partial class obj_item_wallframe_barsign : obj_item_wallframe
{
    // tg: code/game/machinery/barsigns.dm:570
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc += Dm.text(" Can be registered with a set of [] to associate the sign with the area it occupies.", span_bold("station blueprints"));
        return dot;
    }

    // tg: code/game/machinery/barsigns.dm:574
    public override async DmTask<object> try_build(atom support = null, mob user = null)
    {
        object dot = await base.try_build(support, user);
        if (!Dm.truthy(dot))
            return dot;

        if (isopenturf(get_step(support, EAST))) //This takes up 2 tiles so we want to make sure we have two tiles to hang it from.
        {
            balloon_alert(user, "needs more support!");
            return FALSE;
        }
        return dot;
    }
}
