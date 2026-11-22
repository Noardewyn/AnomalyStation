// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/rods.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file (the cyborg rods' Initialize since silicons SI2). The var blocks are generated (D111). The welder screentips (M9) and the slapcrafting recipes
// (spear, stunprod, teleprod, wireprod) come with the crafting menu (M5 slice 8); recipes whose results are not ported
// build inert things (D132, D148).

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/rods.dm:1  GLOBAL_LIST_INIT(rod_recipes, list(...))
    public DmList rod_recipes;

    // tg: code/game/objects/items/stacks/rods.dm:1  (GLOBAL_MANAGED(rod_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrod_recipes()
    {
        rod_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("grille", typeof(obj_structure_grille), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("table frame", typeof(obj_structure_table_frame), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("scooter frame", typeof(obj_item_scooter_frame), 10, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("linen bin", typeof(obj_structure_bedsheetbin_empty), 2, Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("railing", typeof(obj_structure_railing), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("railing corner", typeof(obj_structure_railing_corner), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("railing end", typeof(obj_structure_railing_corner_end), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("railing end (flipped)", typeof(obj_structure_railing_corner_end_flip), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("tank holder", typeof(obj_structure_tank_holder), 2, Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("ladder", typeof(obj_structure_ladder_crafted), 15, Dm.named("time", 15 * SECONDS), Dm.named("crafting_flags", CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("catwalk floor tile", typeof(obj_item_stack_tile_catwalk_tile), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("stairs frame", typeof(obj_structure_stairs_frame), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("probing cane", typeof(obj_item_cane_white), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("sharpened iron rod", typeof(obj_item_ammo_casing_rebar), 1, Dm.named("time", 0.2f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY), Dm.named("category", CAT_WEAPON_AMMO))
        );
        Dm.append(ref gvars_datum_init_order, "rod_recipes");
    }
}

// tg: code/game/objects/items/stacks/rods.dm:18
[DmPath("/obj/item/stack/rods")]
public partial class obj_item_stack_rods : obj_item_stack
{
    // tg: code/game/objects/items/stacks/rods.dm:50
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to stuff \the [] down [] throat! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));//it looks like theyre ur mum
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/stacks/rods.dm:54
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        float? merge = Dm.arg<float?>(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        float? mat_amt = Dm.arg<float?>(__extra, 3);
        merge ??= TRUE;
        mat_amt ??= 1;
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt));
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_openspace_item_click_handler)));
        // var/static/list/tool_behaviors and AddElement(/datum/element/contextual_screentip_tools, tool_behaviors) (:58-64)
        Dm.Deferred("code/game/objects/items/stacks/rods.dm:64", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");

        // tg: code/game/objects/items/stacks/rods.dm:66  var/static/list/slapcraft_recipe_list (a type-level static, rules 10.6)
        // snatcher prod isn't here as a spoopy secret
        // tg: code/game/objects/items/stacks/rods.dm:68
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_stack_rods__slapcraft_recipe_list));
        return dot;
    }

    // tg: code/game/objects/items/stacks/rods.dm:73
    public override async DmTask handle_openspace_click(turf target = null, mob user = null, DmList modifiers = null)
    {
        // target.base_item_interaction(user, src, list2params(modifiers)): tg passes the modifiers as params text where the
        // proc declares a list; the typed parameter reads text as null (rules 3.5).
        await target.base_item_interaction(user as mob_living, this, null);
    }

    // tg: code/game/objects/items/stacks/rods.dm:76
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.rod_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/rods.dm:80
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        float amount = Dm.num(get_amount());
        if (amount <= 5)
            icon_state = Dm.text("[]-[]", Dm.initial(this, (atom a) => a.icon_state), amount);
        else
            icon_state = Dm.initial(this, (atom a) => a.icon_state);
        return dot;
    }

    // tg: code/game/objects/items/stacks/rods.dm:88
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.num(get_amount()) < 2)
        {
            balloon_alert(user, I18n.T("not enough rods!"));
            return null;
        }
        if (Dm.truthy(await tool.use_tool(this, user, delay: 0, volume: 40)))
        {
            obj_item_stack_sheet_iron new_item = Dm.@new<obj_item_stack_sheet_iron>(user.loc);
            user.visible_message(
                span_notice(I18n.T("[] shaped [] into iron sheets with [].", user.name, this, tool)),
                blind_message: span_hear(I18n.T("You hear welding.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user
            );
            await use(2);
            await user.put_in_inactive_hand(new_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/rods.dm:104
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, delay: 0, volume: 40)))
        {
            obj_item_stack_tile_iron_two new_item = Dm.@new<obj_item_stack_tile_iron_two>(user.loc);
            user.visible_message(
                span_notice(I18n.T("[] shaped [] into floor tiles with [].", user.name, this, tool)),
                blind_message: span_hear(I18n.T("You hear welding.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user
            );
            await use(1);
            await user.put_in_inactive_hand(new_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// tg: code/game/objects/items/stacks/rods.dm:117
public partial class obj_item_stack_rods_cyborg : obj_item_stack_rods
{
    // tg: code/game/objects/items/stacks/rods.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}
