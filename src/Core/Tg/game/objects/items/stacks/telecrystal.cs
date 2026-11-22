// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/telecrystal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the telecrystal: charging an implanted uplink, its tile recipe). The var blocks are
// generated. The telecrystal tile itself waits for the mineral tiles (inert, as the uranium tile of mineral.cs's recipes).

// tg: code/game/objects/items/stacks/telecrystal.dm:1
public partial class obj_item_stack_telecrystal : obj_item_stack
{
    // tg: code/game/objects/items/stacks/telecrystal.dm:16
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with != user) // You can't go around smacking people with crystals to find out if they have an uplink or not.
            return NONE;

        foreach (obj_item_implant_uplink uplink in Dm.each<obj_item_implant_uplink>(interacting_with))
        {
            if (uplink.imp_in == null)
                continue;

            datum_component_uplink hidden_uplink = uplink.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
            if (hidden_uplink == null)
                continue;
            hidden_uplink.uplink_handler.add_telecrystals(amount);
            await use(amount);
            to_chat(user, span_notice(I18n.T("You press [] onto yourself and charge your hidden uplink.", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/stacks/telecrystal.dm:43
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.telecrystal_recipes);
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/telecrystal.dm:39  GLOBAL_LIST_INIT(telecrystal_recipes, list(...))
    public DmList telecrystal_recipes;

    // tg: code/game/objects/items/stacks/telecrystal.dm:39  (GLOBAL_MANAGED(telecrystal_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltelecrystal_recipes()
    {
        telecrystal_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("telecrystal tile", typeof(obj_item_stack_tile_mineral_telecrystal), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "telecrystal_recipes");
    }
}
