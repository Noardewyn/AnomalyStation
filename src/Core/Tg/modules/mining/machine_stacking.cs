// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/machine_stacking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2b (D304): the whole file. The var block is generated (D111).

/**********************Mineral stacking unit console**************************/

// tg: code/modules/mining/machine_stacking.dm:3
public partial class obj_machinery_mineral_stacking_unit_console : obj_machinery_mineral
{
    // tg: code/modules/mining/machine_stacking.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        area our_area = get_area(this) as area;
        if (our_area == null)
            return dot;
        DmList turf_list = our_area.get_turfs_by_zlevel(z) as DmList;
        if (turf_list == null)
            return dot;
        foreach (turf area_turf in Dm.each<turf>(turf_list))
        {
            obj_machinery_mineral_stacking_machine found_machine = Dm.locate<obj_machinery_mineral_stacking_machine>(area_turf);
            if (found_machine != null && found_machine.console == null)
            {
                found_machine.console = this;
                machine = found_machine;
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/mining/machine_stacking.dm:28
    public override object Destroy(float? force = null)
    {
        if (machine != null)
        {
            machine.console = null;
            machine = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mining/machine_stacking.dm:34
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        ((obj_item_multitool)tool).set_buffer(this);
        balloon_alert(user, "saved to multitool buffer");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/machine_stacking.dm:39
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "StackingConsole", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mining/machine_stacking.dm:45
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["machine"] = machine != null ? TRUE : FALSE;
        data["stacking_amount"] = null;
        DmList contents_rows = Dm.list();
        data["contents"] = contents_rows;
        if (machine != null)
        {
            data["stacking_amount"] = machine.stack_amt;
            data["input_direction"] = dir2text(machine.input_dir);
            data["output_direction"] = dir2text(machine.output_dir);
            foreach (object stack_type in Dm.each<object>(machine.stack_list))
            {
                obj_item_stack stored_sheet = (obj_item_stack)machine.stack_list[stack_type];
                if (Dm.num(stored_sheet.amount) <= 0)
                    continue;
                contents_rows.Add(Dm.list(Dm.list(
                    Dm.kv("type", stored_sheet.GetType()),
                    Dm.kv("name", capitalize(stored_sheet.name)),
                    Dm.kv("amount", stored_sheet.amount))));
            }
        }
        return data;
    }

    // tg: code/modules/mining/machine_stacking.dm:65
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "release":
            {
                Type released_type = text2path(@params["type"] as string);
                if (released_type == null || !Dm.@in(Dm.initial(released_type, (obj_item_stack s) => s.merge_type), machine.stack_list))
                    return dot; //someone tried to spawn materials by spoofing hrefs
                obj_item_stack inp = (obj_item_stack)machine.stack_list[Dm.initial(released_type, (obj_item_stack s) => s.merge_type)];
                obj_item_stack @out = (obj_item_stack)Dm.@new(inp.GetType(), null, inp.amount);
                inp.amount = 0;
                await machine.unload_mineral(@out);
                return TRUE;
            }
            case "rotate":
            {
                object input = text2num(@params["input"]);
                machine.rotate(input);
                return TRUE;
            }
        }
        return dot;
    }
}

/**********************Mineral stacking unit**************************/

// tg: code/modules/mining/machine_stacking.dm:86
public partial class obj_machinery_mineral_stacking_machine : obj_machinery_mineral
{
    /// Typecache of objects that we can suck up
    // tg: code/modules/mining/machine_stacking.dm:107
    public static DmList accepted_types => TgState.Current.obj_machinery_mineral_stacking_machine__accepted_types ??= (DmList)typecacheof(Dm.list(
        typeof(obj_item_stack_sheet),
        typeof(obj_item_stack_ore_bluespace_crystal)));

    // tg: code/modules/mining/machine_stacking.dm:112
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, 1);
        materials = Dm.@new<datum_remote_materials>(
            this,
            mapload,
            FALSE,
            Dm.num(Dm.truthy(mapload) && Dm.truthy(force_connect)));
        return dot;
    }

    // tg: code/modules/mining/machine_stacking.dm:122
    public override object Destroy(float? force = null)
    {
        if (console != null)
        {
            console.machine = null;
            console = null;
        }
        QDEL_NULL(ref proximity_monitor);
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/machine_stacking.dm:130
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        atom_movable entered = proximity_check_mob;
        if (QDELETED(entered))
            return null;
        if (entered.loc != get_step(this, input_dir))
            return null;
        if (Dm.truthy(is_type_in_typecache(entered, accepted_types)))
            await process_stack((obj_item_stack)entered);
        return null;
    }

    // tg: code/modules/mining/machine_stacking.dm:138
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item_multitool multi_tool = (obj_item_multitool)tool;
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(multi_tool.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(multi_tool.flags_1, HOLOGRAM_1)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!istype(multi_tool.buffer, typeof(obj_machinery_mineral_stacking_unit_console)))
            return ITEM_INTERACT_BLOCKING;

        console = (obj_machinery_mineral_stacking_unit_console)multi_tool.buffer;
        console.machine = this;
        to_chat(user, span_notice(I18n.T("You link [] to the console in []'s buffer.", this, multi_tool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/machine_stacking.dm:151
    public virtual void rotate(object input = null)
    {
        if (Dm.truthy(input))
            input_dir = turn(input_dir, 90);
        else
            output_dir = turn(output_dir, 90);
        if (input_dir == output_dir)
            rotate(input);
    }

    // tg: code/modules/mining/machine_stacking.dm:159
    public virtual async DmTask process_stack(obj_item_stack input = null)
    {
        if (QDELETED(input))
            return;

        // Dump the sheets to the silo if attached
        if (materials.silo != null && !materials.on_hold())
        {
            DmList matlist = Dm.bitand(input.custom_materials, materials.mat_container.materials) as DmList;
            if (Dm.truthy(length(matlist)))
            {
                await materials.insert_item(input);
                return;
            }
        }

        // No silo attached process to internal storage
        Type key = input.merge_type;
        obj_item_stack storage = stack_list[key] as obj_item_stack;
        if (storage == null) //It's the first of this sheet added
            stack_list[key] = storage = (obj_item_stack)Dm.@new(input.GetType(), this, 0);
        storage.amount = Dm.num(storage.amount) + Dm.num(input.amount); //Stack the sheets
        qdel(input);

        while (Dm.num(storage.amount) >= stack_amt) //Get rid of excessive stackage
        {
            obj_item_stack @out = (obj_item_stack)Dm.@new(input.GetType(), null, stack_amt);
            await unload_mineral(@out);
            storage.amount = Dm.num(storage.amount) - stack_amt;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/machine_stacking.dm:107  /obj/machinery/mineral/stacking_machine's var/static/list/accepted_types
    internal DmList obj_machinery_mineral_stacking_machine__accepted_types;
}
