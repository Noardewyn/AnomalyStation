// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/satchel_ore_box.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a (D304): the whole file. The boulders (obj/item/boulder) are L2c's: until then nothing makes one. The var block
// is generated (D111).

/**********************Ore box**************************/

// tg: code/modules/mining/satchel_ore_box.dm:4
public partial class obj_structure_ore_box : obj_structure
{
    // tg: code/modules/mining/satchel_ore_box.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    ///Dumps all contents of this ore box on the turf
    // tg: code/modules/mining/satchel_ore_box.dm:18
    public virtual async DmTask dump_box_contents()
    {
        atom drop = drop_location() as atom;
        foreach (obj_item weapon in Dm.each<obj_item>(contents.Copy()))
            await weapon.forceMove(drop);
    }

    // tg: code/modules/mining/satchel_ore_box.dm:23
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        Dm.@new<obj_item_stack_sheet_mineral_wood>(loc, 4);

        await dump_box_contents();
    }

    // tg: code/modules/mining/satchel_ore_box.dm:28
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (istype(held_item, typeof(obj_item_stack_ore)) || istype(held_item, typeof(obj_item_boulder)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Insert Item";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (held_item.atom_storage != null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Transfer Contents";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/mining/satchel_ore_box.dm:44
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(in_range(this, user)) || isobserver(user))
        {
            dot.Add(span_notice(I18n.T("Can be [] apart.", EXAMINE_HINT("pried"))));
            await ui_interact(user);
        }
        return dot;
    }

    // tg: code/modules/mining/satchel_ore_box.dm:50
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(await tool.use_tool(this, user, 50, volume: 50)))
        {
            user.visible_message(span_notice(I18n.T("[] pries \\the [] apart.", user, this)),
                span_notice(I18n.T("You pry apart \\the [].", this)),
                span_hear("You hear splitting wood."));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/mining/satchel_ore_box.dm:59
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_ore)) || istype(tool, typeof(obj_item_boulder)))
        {
            await user.transferItemToLoc(tool, this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool.atom_storage != null)
        {
            await tool.atom_storage.remove_type(typeof(obj_item_stack_ore), this, INFINITY, TRUE, FALSE, user, null);
            to_chat(user, span_notice(I18n.T("You empty the ore in [] into \\the [].", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mining/satchel_ore_box.dm:71
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (istype(arrived, typeof(obj_item_boulder)) && ismecha(loc)) //Boulders being put into a mech's orebox get processed
        {
            obj_item_boulder to_process = (obj_item_boulder)arrived;
            await Dm.Call(to_process, "convert_to_ore", this);
            qdel(to_process);
        }
        return dot;
    }

    // tg: code/modules/mining/satchel_ore_box.dm:78
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "OreBox", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mining/satchel_ore_box.dm:84
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList materials = Dm.list();
        object name;
        object amount;
        foreach (atom_movable potental_ore in Dm.each<atom_movable>(contents))
        {
            if (potental_ore is obj_item_stack_ore ore)
            {
                name = ore.name;
                amount = ore.amount;
            }
            else
            {
                name = "Boulders";
                amount = 1;
            }

            bool item_found = false;
            foreach (DmList item in Dm.each<DmList>(materials))
            {
                if (Dm.eq(item["name"], name))
                {
                    item_found = true;
                    item["amount"] = Dm.num(item["amount"]) + Dm.num(amount);
                    break;
                }
            }
            if (!item_found)
                materials.Add(Dm.list(Dm.list(Dm.kv("name", name), Dm.kv("amount", amount))));
        }

        await DmTask.CompletedTask;
        return Dm.list(Dm.kv("materials", materials));
    }

    // tg: code/modules/mining/satchel_ore_box.dm:107
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "removeall"))
        {
            await dump_box_contents();
            return TRUE;
        }
        return dot;
    }

    /// Special override for notify_contents = FALSE.
    // tg: code/modules/mining/satchel_ore_box.dm:117
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        notify_contents ??= FALSE;
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }
}
