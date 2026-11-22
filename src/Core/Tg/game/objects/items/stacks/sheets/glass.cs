// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/glass.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks (and the /datum/armor sheet types) are generated
// (D111). The shard's butchering is the call left deferred (M7 power helpers slice: the light replacer takes glass and shards); recipes whose results are not ported build inert things (D132, D148).

/* Glass stack types
 * Contains:
 * Glass sheets
 * Reinforced glass sheets
 * Glass shards - TODO: Move this into code/game/object/item/weapons
 */

/*
 * Glass sheets
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:11  GLOBAL_LIST_INIT(glass_recipes, list(...))
    public DmList glass_recipes;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:11  (GLOBAL_MANAGED(glass_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalglass_recipes()
    {
        glass_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("directional window", typeof(obj_structure_window_unanchored), Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("fulltile window", typeof(obj_structure_window_fulltile_unanchored), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("glass shard", typeof(obj_item_shard), Dm.named("time", 0), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("glass tile", typeof(obj_item_stack_tile_glass), 1, 4, 20, Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "glass_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:18
public partial class obj_item_stack_sheet_glass : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:42
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to slice [] neck with \the []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:49
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.glass_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:53
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // tg: code/game/objects/items/stacks/sheets/glass.dm:54
        add_fingerprint(user);
        if (istype(tool, typeof(obj_item_lightreplacer)))
        {
            obj_item_lightreplacer lightreplacer = tool as obj_item_lightreplacer;
            await lightreplacer.item_interaction(user, this, modifiers);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_cable_coil)))
        {
            obj_item_stack_cable_coil coil = tool as obj_item_stack_cable_coil;
            if (Dm.num(get_amount()) < 1 || Dm.num(coil.get_amount()) < 5)
            {
                to_chat(user, span_warning(I18n.T("You need five lengths of coil and one sheet of glass to make wired glass!")));
                return ITEM_INTERACT_BLOCKING;
            }
            await coil.use(5);
            await use(1);
            to_chat(user, span_notice(I18n.T("You attach wire to \the [].", this)));
            obj_item_stack_light_w new_tile = Dm.@new<obj_item_stack_light_w>(user.loc);
            if (!QDELETED(new_tile))
                // tg: code/game/objects/items/stacks/sheets/glass.dm:70
                new_tile.add_fingerprint(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_rods)))
        {
            obj_item_stack_rods rods = tool as obj_item_stack_rods;
            if (Dm.num(rods.get_amount()) < 1 || Dm.num(get_amount()) < 1) // the hell kind of check is this, how would this happen
            {
                to_chat(user, span_warning(I18n.T("You need one rod and one sheet of glass to make reinforced glass!")));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_stack_sheet_rglass new_glass = Dm.@new<obj_item_stack_sheet_rglass>(get_turf(user));
            if (!QDELETED(new_glass))
                // tg: code/game/objects/items/stacks/sheets/glass.dm:81
                new_glass.add_fingerprint(user);
            bool replace = user.get_inactive_held_item() == this;
            await rods.use(1);
            await use(1);
            if (QDELETED(this) && replace && !QDELETED(new_glass))
                await user.put_in_hands(new_glass);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:91  GLOBAL_LIST_INIT(pglass_recipes, list(...))
    public DmList pglass_recipes;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:91  (GLOBAL_MANAGED(pglass_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpglass_recipes()
    {
        pglass_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("directional window", typeof(obj_structure_window_plasma_unanchored), Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("fulltile window", typeof(obj_structure_window_plasma_fulltile_unanchored), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("plasma glass shard", typeof(obj_item_shard_plasma), Dm.named("time", 20), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("plasma glass tile", typeof(obj_item_stack_tile_glass_plasma), 1, 4, 20, Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "pglass_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:98
public partial class obj_item_stack_sheet_plasmaglass : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:122
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.pglass_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:126
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // tg: code/game/objects/items/stacks/sheets/glass.dm:127
        add_fingerprint(user);
        if (!istype(tool, typeof(obj_item_stack_rods)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        obj_item_stack_rods rods = tool as obj_item_stack_rods;
        if (Dm.num(rods.get_amount()) < 1 || Dm.num(get_amount()) < 1)
        {
            to_chat(user, span_warning(I18n.T("You need one rod and one sheet of plasma glass to make reinforced plasma glass!")));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_stack_sheet_plasmarglass RG = Dm.@new<obj_item_stack_sheet_plasmarglass>(get_turf(user));
        if (!QDELETED(RG))
            // tg: code/game/objects/items/stacks/sheets/glass.dm:138
            RG.add_fingerprint(user);
        bool replace = user.get_inactive_held_item() == this;
        await rods.use(1);
        await use(1);
        if (QDELETED(this) && replace)
            await user.put_in_hands(RG);
        return ITEM_INTERACT_SUCCESS;
    }
}



/*
 * Reinforced glass sheets
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:151  GLOBAL_LIST_INIT(reinforced_glass_recipes, list(...))
    public DmList reinforced_glass_recipes;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:151  (GLOBAL_MANAGED(reinforced_glass_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreinforced_glass_recipes()
    {
        reinforced_glass_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("windoor frame", typeof(obj_structure_windoor_assembly), 5, Dm.named("time", 0), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            null,
            Dm.@new<datum_stack_recipe>("directional reinforced window", typeof(obj_structure_window_reinforced_unanchored), Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("fulltile reinforced window", typeof(obj_structure_window_reinforced_fulltile_unanchored), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("glass shard", typeof(obj_item_shard), Dm.named("time", 10), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("reinforced glass tile", typeof(obj_item_stack_tile_rglass), 1, 4, 20, Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "reinforced_glass_recipes");
    }
}


// tg: code/game/objects/items/stacks/sheets/glass.dm:161
public partial class obj_item_stack_sheet_rglass : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:177
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_silicon), 10), Dm.kv(typeof(datum_reagent_iron), 10));
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:187
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // tg: code/game/objects/items/stacks/sheets/glass.dm:188
        add_fingerprint(user);
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:191
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.reinforced_glass_recipes);
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:195  GLOBAL_LIST_INIT(prglass_recipes, list(...))
    public DmList prglass_recipes;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:195  (GLOBAL_MANAGED(prglass_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprglass_recipes()
    {
        prglass_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("directional reinforced window", typeof(obj_structure_window_reinforced_plasma_unanchored), Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("fulltile reinforced window", typeof(obj_structure_window_reinforced_plasma_fulltile_unanchored), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("plasma glass shard", typeof(obj_item_shard_plasma), Dm.named("time", 40), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("reinforced plasma glass tile", typeof(obj_item_stack_tile_rglass_plasma), 1, 4, 20, Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "prglass_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:202
public partial class obj_item_stack_sheet_plasmarglass : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:220
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_silicon), 10), Dm.kv(typeof(datum_reagent_toxin_plasma), 10), Dm.kv(typeof(datum_reagent_iron), 10));
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:231
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.prglass_recipes);
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:235  GLOBAL_LIST_INIT(titaniumglass_recipes, list(...))
    public DmList titaniumglass_recipes;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:235  (GLOBAL_MANAGED(titaniumglass_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltitaniumglass_recipes()
    {
        titaniumglass_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("directional shuttle window", typeof(obj_structure_window_reinforced_titanium_unanchored), Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("shuttle window", typeof(obj_structure_window_reinforced_shuttle_unanchored), 2, Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("titanium glass shard", typeof(obj_item_shard_titanium), Dm.named("time", 40), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("titanium glass tile", typeof(obj_item_stack_tile_glass_titanium), 1, 4, 20, Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "titaniumglass_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:242
public partial class obj_item_stack_sheet_titaniumglass : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:265
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.titaniumglass_recipes);
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:269  GLOBAL_LIST_INIT(plastitaniumglass_recipes, list(...))
    public DmList plastitaniumglass_recipes;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:269  (GLOBAL_MANAGED(plastitaniumglass_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplastitaniumglass_recipes()
    {
        plastitaniumglass_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("directional plastitanium window", typeof(obj_structure_window_reinforced_survival_pod_unanchored), Dm.named("time", 0.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("plastitanium window", typeof(obj_structure_window_reinforced_plasma_plastitanium_unanchored), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("pod window", typeof(obj_structure_window_reinforced_shuttle_survival_pod_unanchored), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("plastitanium window filler", typeof(obj_item_stack_wall_filling_plastitaniumglass_basic), 2, 1, 10, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("plastitanium glass shard", typeof(obj_item_shard_plastitanium), Dm.named("time", 60), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("plastitanium glass tile", typeof(obj_item_stack_tile_glass_plastitanium), 1, 4, 20, Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "plastitaniumglass_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:278
public partial class obj_item_stack_sheet_plastitaniumglass : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:302
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.plastitaniumglass_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:306
public partial class obj_item_shard : obj_item
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:342
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] [] with the shard of glass! It looks like [] trying to commit suicide.", user, user.p_their(), pick("wrists", "throat"), user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:346
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", force)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 15 * SECONDS), Dm.named("effectiveness", 65f)));
        icon_state = (string)pick("large", "medium", "small");
        switch (icon_state)
        {
            case "small":
                pixel_x = rand(-12, 12);
                pixel_y = rand(-12, 12);
                break;
            case "medium":
                pixel_x = rand(-8, 8);
                pixel_y = rand(-8, 8);
                break;
            case "large":
                pixel_x = rand(-5, 5);
                pixel_y = rand(-5, 5);
                break;
        }
        if (Dm.truthy(icon_prefix))
            icon_state = Dm.text("[][]", icon_prefix, icon_state);

        turf T = get_turf(this);
        if (Dm.truthy(T) && Dm.truthy(is_station_level(T.z)))
            SSblackbox.record_feedback("tally", "station_mess_created", 1, name);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:370  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_shard__Initialize__loc_connections;

    // tg: code/game/objects/items/stacks/sheets/glass.dm:375
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);

        turf T = get_turf(this);
        if (Dm.truthy(T) && Dm.truthy(is_station_level(T.z)))
            SSblackbox.record_feedback("tally", "station_mess_destroyed", 1, name);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:382
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!iscarbon(user) || !Dm.truthy(user.is_holding(this)))
            return;

        mob_living_carbon jab = user as mob_living_carbon;
        if (Dm.truthy(Dm.bitand(jab.get_all_covered_flags(), HANDS)))
            return;

        to_chat(user, span_warning(I18n.T("[] cuts into your hand!", this)));
        await jab.apply_damage(force * 0.5f, BRUTE, ((mob_living_carbon)user).get_active_hand(), attacking_item: this);
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:393
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_lightreplacer)))
        {
            obj_item_lightreplacer lightreplacer = tool as obj_item_lightreplacer;
            await lightreplacer.attempt_insert_shard(user, this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!istype(tool, typeof(obj_item_stack_sheet_cloth)))
            return NONE;

        obj_item_stack_sheet_cloth cloth = tool as obj_item_stack_sheet_cloth;
        to_chat(user, span_notice(I18n.T("You begin to wrap the [] around the []...", cloth, this)));
        if (!Dm.truthy(await do_after(user, craft_time, target: this)))
            return ITEM_INTERACT_FAILURE;

        obj_item_knife_shiv shiv = Dm.@new(shiv_type) as obj_item_knife_shiv;
        await shiv.set_custom_materials(custom_materials);
        await cloth.use(1);
        to_chat(user, span_notice(I18n.T("You wrap the [] around the [], forming a makeshift weapon.", cloth, this)));
        qdel(this);
        await user.put_in_hands(shiv);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:415
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, 0, volume: 50)))
        {
            obj_item_stack_sheet new_glass = Dm.@new(weld_material) as obj_item_stack_sheet;
            to_chat(user, span_notice(I18n.T("You melt [] down into [].", this, new_glass.name)));
            await new_glass.forceMove((Dm.truthy(await Adjacent(user)) ? user.drop_location() : loc) as atom); //stack merging is handled automatically.
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/sheets/glass.dm:423
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (isliving(AM))
        {
            mob_living L = AM as mob_living;
            if (!Dm.truthy(Dm.bitand(L.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || Dm.truthy(L.buckled))
                playsound(this, Dm.resource("sound/effects/footstep/glass_step.ogg"), HAS_TRAIT(L, TRAIT_LIGHT_STEP) ? 30 : 50, TRUE);
        }
    }
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:430
public partial class obj_item_shard_plasma : obj_item_shard
{
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:444
public partial class obj_item_shard_titanium : obj_item_shard
{
}

// tg: code/game/objects/items/stacks/sheets/glass.dm:457
public partial class obj_item_shard_plastitanium : obj_item_shard
{
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/stacks/sheets/glass.dm:370  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_item_shard__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_shard.on_entered)));
}
