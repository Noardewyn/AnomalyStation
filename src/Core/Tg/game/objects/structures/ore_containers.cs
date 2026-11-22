// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/ore_containers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The ore container's base (the gutlunchers' food troughs and the mook village's material stand
// derive from it; they stay inert with Lavaland, not-translated.md 213): ores are put in by hand, its overlays follow what it
// holds, and its OreContainer window lists the stock and drops a withdrawn stack at the user's feet (the client's
// Tgui/Interfaces/OreContainer.cs). The var block is generated (D111).

///structure to contain ores
// tg: code/game/objects/structures/ore_containers.dm:2
public partial class obj_structure_ore_container : obj_structure
{
    // tg: code/game/objects/structures/ore_containers.dm:4
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_ore)) || Dm.truthy(user.combat_mode))
            return NONE;
        await tool.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/ore_containers.dm:10
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/ore_containers.dm:14
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/ore_containers.dm:18
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "OreContainer");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/ore_containers.dm:25
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList ores = Dm.list();
        foreach (obj_item_stack_ore ore_item in Dm.each<obj_item_stack_ore>(contents))
        {
            Dm.append(ref ores, Dm.list(Dm.list(
                Dm.kv("id", REF(ore_item)),
                Dm.kv("name", ore_item.name),
                Dm.kv("amount", ore_item.amount),
                Dm.kv("icon", ore_item.icon), // ore_item::icon
                Dm.kv("icon_state", ore_item.icon_state)))); // ore_item::icon_state
        }
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv("ores", ores));
    }

    // tg: code/game/objects/structures/ore_containers.dm:37
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);

        if (Dm.truthy(dot) || !isliving(ui.user))
            return TRUE;

        mob_living customer = (mob_living)ui.user;
        obj_item stack_to_move;
        switch (action)
        {
            case "withdraw":
                if (@params["reference"] == null)
                    return TRUE;
                stack_to_move = Dm.locate_ref(@params["reference"]) is obj_item found && Dm.@in(found, contents) ? found : null; // locate(params["reference"]) in contents
                if (stack_to_move == null)
                    return TRUE;
                await stack_to_move.forceMove(get_turf(customer));
                return TRUE;
        }
        return dot;
    }
}
