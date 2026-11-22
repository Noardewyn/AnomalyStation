// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/_toolbox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the toolbox base (docs/ai/specs/m4a-ownership.md, P3's list): Initialize, the latches' overlay, the toolbox's own
// chain overrides (interact_with_atom's quick tool use and use_tool_on) and tool_act. Later: suicide_act (M10, a
// live-partial row). The quick tool use shows tg's radial of the toolbox's tools (show_radial_menu, M4c slice 4: drawn by the
// client, DV40); the falling-hazard and cuffable elements are
// deferred leaves (falls M5, restraints M9); the repairbot assembly is the bots' (after the core version).

// tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:1
public partial class obj_item_storage_toolbox : obj_item_storage
{
    /// Items we should not interact with when left clicking
    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:31 (var/static: rules 10.6, a TgState field; typecacheof
    // creates no datum, so tg's world-start value and a creation at the first use are the same list)
    public static ref DmList lmb_exception_typecache => ref TgState.Current.obj_item_storage_toolbox__lmb_exception_typecache;

    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(has_latches))
        {
            if (prob(10))
            {
                latches = "double_latch";
                if (prob(1))
                {
                    latches = "triple_latch";
                    if (prob(0.1f))
                        latches = "quad_latch"; // like winning the lottery, but worse
                }
            }
        }
        update_appearance();
        Dm.Deferred("code/game/objects/items/storage/toolboxes/_toolbox.dm:48", "Initialize: AddElement(/datum/element/falling_hazard, ...) (a toolbox falling on a head: falls, M5)");
        Dm.Deferred("code/game/objects/items/storage/toolboxes/_toolbox.dm:49", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)");
        return dot;
    }

    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:51
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        lmb_exception_typecache ??= typecacheof(Dm.list(
            typeof(obj_structure_table),
            typeof(obj_structure_rack),
            typeof(obj_structure_closet),
            typeof(obj_machinery_disposal)
        )) as DmList;
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(user.has_hand_for_held_index(user.get_inactive_hand_index())))
            return NONE;
        if (Dm.truthy(is_type_in_typecache(interacting_with, lmb_exception_typecache)) && !Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return NONE;
        if (Dm.truthy(current_interactions))
        {
            obj_item other_tool = user.get_inactive_held_item() as obj_item;
            if (!(other_tool is obj_item)) // what even
                return NONE;
            Waiting.Detach(use_tool_on(interacting_with, user, modifiers, other_tool));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(user.get_inactive_held_item()))
        {
            user.balloon_alert(user, I18n.T("hands busy!"));
            return ITEM_INTERACT_BLOCKING;
        }
        DmList item_radial = Dm.list();
        foreach (obj_item tool in Dm.each<obj_item>(atom_storage.real_location))
        {
            if (Dm.truthy(is_type_in_list(tool, tool_items__gap())))
                item_radial[tool] = tool; // SHIM-GAP(lead): BYOND's read-only appearance var is not in the atom engine part; the
                                          // radial's extract_image copies the tool's appearance (new /mutable_appearance(tool)), the same picture.
        }
        if (!Dm.truthy(Dm.length(item_radial)))
            return NONE;
        playsound(user, Dm.resource("sound/items/handling/toolbox/toolbox_open.ogg"), 50); // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:77
        obj_item picked_item = await show_radial_menu(user, interacting_with, item_radial, require_near: TRUE) as obj_item; // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:78
        if (!Dm.truthy(picked_item))
            return ITEM_INTERACT_BLOCKING;
        playsound(user, Dm.resource("sound/items/handling/toolbox/toolbox_rustle.ogg"), 50); // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:82
        if (!Dm.truthy(await user.put_in_inactive_hand(picked_item)))
            return ITEM_INTERACT_BLOCKING;
        atom_storage.animate_parent();
        if (picked_item is obj_item_weldingtool)
        {
            obj_item_weldingtool welder = picked_item as obj_item_weldingtool;
            if (!Dm.truthy(welder.welding))
                await welder.attack_self(user);
        }
        if (picked_item is obj_item_spess_knife)
            await picked_item.attack_self(user);
        Waiting.Detach(use_tool_on(interacting_with, user, modifiers, picked_item));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:98
    public virtual async DmTask use_tool_on(atom interacting_with = null, mob_living user = null, DmList modifiers = null, obj_item picked_tool = null)
    {
        current_interactions += 1;
        await picked_tool.melee_attack_chain(user, interacting_with, modifiers);
        current_interactions -= 1;
        if (QDELETED(picked_tool) || picked_tool.loc != user || !Dm.truthy(await picked_tool.IsReachableBy(user)))
        {
            current_interactions = 0;
            return;
        }
        if (Dm.truthy(current_interactions))
            return;
        if (picked_tool is obj_item_weldingtool)
        {
            obj_item_weldingtool welder = picked_tool as obj_item_weldingtool;
            if (Dm.truthy(welder.welding))
                await welder.attack_self(user);
        }
        atom_storage.attempt_insert(picked_tool, user);
    }

    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:114
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_latches))
            Dm.append(ref dot, latches);
        return dot;
    }

    //repairbot assembly
    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:124
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!(tool is obj_item_assembly_prox_sensor))
            return await base.tool_act(user, tool, modifiers);
        DmList allowed_toolbox = Dm.list(
            typeof(obj_item_storage_toolbox_artistic),
            typeof(obj_item_storage_toolbox_crafter),
            typeof(obj_item_storage_toolbox_electrical),
            typeof(obj_item_storage_toolbox_emergency),
            typeof(obj_item_storage_toolbox_mechanical),
            typeof(obj_item_storage_toolbox_syndicate)
        );
        if (!Dm.truthy(is_type_in_list(this, allowed_toolbox)) && (type != typeof(obj_item_storage_toolbox)))
            return ITEM_INTERACT_BLOCKING;
        if (contents.len >= 1)
        {
            balloon_alert(user, I18n.T("not empty!"));
            return ITEM_INTERACT_BLOCKING;
        }
        DmList toolbox_colors = TgState.Current.obj_item_storage_toolbox__toolbox_colors ??= Dm.list(
            Dm.kv(typeof(obj_item_storage_toolbox), "#445eb3"),
            Dm.kv(typeof(obj_item_storage_toolbox_emergency), "#445eb3"),
            Dm.kv(typeof(obj_item_storage_toolbox_electrical), "#b77931"),
            Dm.kv(typeof(obj_item_storage_toolbox_artistic), "#378752"),
            Dm.kv(typeof(obj_item_storage_toolbox_crafter), "#9D3282"),
            Dm.kv(typeof(obj_item_storage_toolbox_syndicate), "#3d3d3d"));
        obj_item_bot_assembly_repairbot repair = Dm.@new<obj_item_bot_assembly_repairbot>(drop_location());
        repair.toolbox = type;
        object new_color = toolbox_colors[type] ?? "#445eb3";
        repair.set_color(new_color);
        repair.update_appearance();
        repair.balloon_alert(user, "sensor added!");
        qdel(tool);
        object held_index = user.is_holding(this);
        qdel(this);
        if (Dm.truthy(held_index))
            await user.put_in_hand(repair, held_index);
        return ITEM_INTERACT_SUCCESS;
    }

    // GAP(lead): GLOB.tool_items (__DEFINES/inventory.dm:350, GLOBAL_LIST_INIT) is not declared in the GLOB container (the
    // lead's __DEFINES mirrors); a local copy of its list until the lead adds it.
    // tg: code/__DEFINES/inventory.dm:350
    internal static DmList tool_items__gap() => Dm.list(
        typeof(obj_item_airlock_painter),
        typeof(obj_item_analyzer),
        typeof(obj_item_assembly_signaler),
        typeof(obj_item_construction_rcd),
        typeof(obj_item_construction_rld),
        typeof(obj_item_construction_rtd),
        typeof(obj_item_crowbar),
        typeof(obj_item_extinguisher_mini),
        typeof(obj_item_flashlight),
        typeof(obj_item_forcefield_projector),
        typeof(obj_item_geiger_counter),
        typeof(obj_item_holosign_creator_atmos),
        typeof(obj_item_holosign_creator_engineering),
        typeof(obj_item_inducer),
        typeof(obj_item_lightreplacer),
        typeof(obj_item_multitool),
        typeof(obj_item_pipe_dispenser),
        typeof(obj_item_pipe_painter),
        typeof(obj_item_plunger),
        typeof(obj_item_radio),
        typeof(obj_item_screwdriver),
        typeof(obj_item_stack_cable_coil),
        typeof(obj_item_t_scanner),
        typeof(obj_item_weldingtool),
        typeof(obj_item_wirecutters),
        typeof(obj_item_wrench),
        typeof(obj_item_spess_knife)
    );
}

public sealed partial class TgState
{
    internal DmList obj_item_storage_toolbox__toolbox_colors;
    // tg: code/game/objects/items/storage/toolboxes/_toolbox.dm:31  /obj/item/storage/toolbox/var/static/list/lmb_exception_typecache
    internal DmList obj_item_storage_toolbox__lmb_exception_typecache;
}
