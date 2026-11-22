// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RPD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice D172 (the rapid pipe dispenser, for the supermatter's pipes): the whole file. The RPD lays the pipe fittings of
// construction.dm (game/machinery/pipe/construction.cs) and the meter fitting, wrenches them down when its Connect mode is on,
// destroys loose fittings, reprograms smart pipes and copies a pipe's colour and layer with a right click; its tgui window is
// RapidPipeDispenser (the client's godot/scripts/Shared/Tgui/Interfaces/RapidPipeDispenser.cs). The transit tube category lays
// the unattached tubes and pods (transit_tubes/transit_tube_construction.cs, 2026-10-26); the disposal category lays the disposal
// segments (recycling/disposal/construction.cs, D325). The var blocks are generated. The
// mouse wheel that changes the layer (mouse_wheeled) is translated (protocol version 70 sends the wheel); D320 (DV352) adds Shift with the
// wheel, which turns the next build through its recipe's directions, and the build preview of the 3D client's ghost (build_preview).

//RAPID PIPE DISPENSER

// tg: code/game/objects/items/rcd/RPD.dm:21
public partial class obj_item_pipe_dispenser : obj_item
{
    // tg: code/game/objects/items/rcd/RPD.dm:3-5  #define ATMOS_CATEGORY 0 ... (file-local defines, rules 11.4)
    private const float ATMOS_CATEGORY = 0;
    private const float DISPOSALS_CATEGORY = 1;
    private const float TRANSIT_CATEGORY = 2;

    // tg: code/game/objects/items/rcd/RPD.dm:7-10
    private const float BUILD_MODE = 1 << 0;
    private const float WRENCH_MODE = 1 << 1;
    private const float DESTROY_MODE = 1 << 2;
    private const float REPROGRAM_MODE = 1 << 3;

    ///Maximum number of pipe layers the RPD can support
    // tg: code/game/objects/items/rcd/RPD.dm:13
    private const float MAX_PIPE_LAYERS = 5;

    ///Converts the pipe layer into a bitflag so we can append multiple layers into 1 bitfield
    // tg: code/game/objects/items/rcd/RPD.dm:16  #define PIPE_LAYER(num) (1 << (num - 1))
    private static float PIPE_LAYER(float num) => Dm.shl(1, num - 1);

    // tg: code/game/objects/items/rcd/RPD.dm:19  #define RPD_USE_SOUND 'sound/items/deconstruct.ogg' (a resource define, expanded at each use: rules 3.7)

    // tg: code/game/objects/items/rcd/RPD.dm:68-72  var/static/datum/pipe_info/first_atmos, first_disposal, first_transit (rules 10.6)
    public static ref datum_pipe_info first_atmos => ref TgState.Current.obj_item_pipe_dispenser__first_atmos;
    public static ref datum_pipe_info first_disposal => ref TgState.Current.obj_item_pipe_dispenser__first_disposal;
    public static ref datum_pipe_info first_transit => ref TgState.Current.obj_item_pipe_dispenser__first_transit;

    // tg: code/game/objects/items/rcd/RPD.dm:259, :329, :509  the proc-level var/statics (rules 10.6)
    private static ref DmList ui_act__recipes => ref TgState.Current.obj_item_pipe_dispenser__ui_act__recipes;
    private static ref DmList interact_with_atom__destroyables => ref TgState.Current.obj_item_pipe_dispenser__interact_with_atom__destroyables;
    private static ref DmList check_can_make_pipe__make_pipe_whitelist => ref TgState.Current.obj_item_pipe_dispenser__check_can_make_pipe__make_pipe_whitelist;

    // tg: code/game/objects/items/rcd/RPD.dm:82
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 5, FALSE);
        spark_system.attach(this);
        if (!Dm.truthy(first_atmos))
            first_atmos = ((DmList)GLOB.atmos_pipe_recipes[GLOB.atmos_pipe_recipes[1]])[1] as datum_pipe_info;
        if (!Dm.truthy(first_disposal))
            first_disposal = ((DmList)GLOB.disposal_pipe_recipes[GLOB.disposal_pipe_recipes[1]])[1] as datum_pipe_info;
        if (!Dm.truthy(first_transit))
            first_transit = ((DmList)GLOB.transit_tube_recipes[GLOB.transit_tube_recipes[1]])[1] as datum_pipe_info;

        recipe = first_atmos;
        register_item_context();
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:96
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spark_system);
        qdel(build_probe); // DV352
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/rcd/RPD.dm:100
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can scroll your <b>mouse wheel</b> to change the piping layer.")));
        Dm.append(ref dot, span_notice(I18n.T("You can <b>right click</b> a pipe to set the RPD to its color and layer.")));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:105
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        object dot = NONE;

        if (istype(target, typeof(obj_machinery_atmospherics)))
        {
            obj_machinery_atmospherics atmos_target = target as obj_machinery_atmospherics;
            if (Dm.truthy(atmos_target.pipe_color) && Dm.truthy(atmos_target.piping_layer))
            {
                context[SCREENTIP_CONTEXT_RMB] = "Copy piping color and layer";
                return CONTEXTUAL_SCREENTIP_SET;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:114
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(Dm.num(slot), ITEM_SLOT_HANDS)))
            RegisterSignal(user, COMSIG_MOUSE_SCROLL_ON, nameof(mouse_wheeled));
        else
            UnregisterSignal(user, COMSIG_MOUSE_SCROLL_ON);
    }

    // tg: code/game/objects/items/rcd/RPD.dm:121
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, COMSIG_MOUSE_SCROLL_ON);
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/rcd/RPD.dm:125
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await ui_interact(user);
        return null;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:128
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] points the end of the RPD down [] throat and presses a button! It looks like [] trying to commit suicide...", user, user.p_their(), user.p_theyre())));
        playsound(get_turf(user), SFX_TOOL_SWITCH, 20, TRUE);
        playsound(get_turf(user), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        return BRUTELOSS;
    }

    ///Converts pipe_layers bitflag into its corresponding list of actual pipe layers
    // tg: code/game/objects/items/rcd/RPD.dm:135
    public virtual object get_active_pipe_layers()
    {
        DmList layer_nums = Dm.list();
        for (float pipe_layer_number = 1, pipe_layer_number__end = MAX_PIPE_LAYERS; pipe_layer_number <= pipe_layer_number__end; pipe_layer_number++)
        {
            if (Dm.truthy(Dm.bitand(PIPE_LAYER(pipe_layer_number), pipe_layers)))
                Dm.append(ref layer_nums, pipe_layer_number);
        }
        return layer_nums;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:145
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/pipes)): the asset transport is the client's (DV44); the
        // client draws the "pipes" sheet's pictures from its own copy of tg's .dmi files (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/game/objects/items/rcd/RPD.dm:150
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RapidPipeDispenser", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:156
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("paint_colors", GLOB.pipe_paint_colors),
            Dm.kv("max_pipe_layers", MAX_PIPE_LAYERS));
    }

    // tg: code/game/objects/items/rcd/RPD.dm:162
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("category", category),
            Dm.kv("multi_layer", multi_layer),
            Dm.kv("pipe_layers", pipe_layers),
            Dm.kv("categories", Dm.list()),
            Dm.kv("selected_recipe", recipe.name),
            Dm.kv("selected_color", paint_color),
            Dm.kv("mode", mode));

        //currently selected category (atmos, disposal or transit)
        DmList selected_major_category = null;
        object sw__1 = category;
        if (Dm.eq(sw__1, ATMOS_CATEGORY))
            selected_major_category = GLOB.atmos_pipe_recipes;
        else if (Dm.eq(sw__1, DISPOSALS_CATEGORY))
            selected_major_category = GLOB.disposal_pipe_recipes;
        else if (Dm.eq(sw__1, TRANSIT_CATEGORY))
            selected_major_category = GLOB.transit_tube_recipes;
        //selected subcategory (e.g. pipes/binary/devices/heat exchange for atmos)
        foreach (object subcategory in Dm.each(selected_major_category))
        {
            DmList subcategory_recipes = selected_major_category[subcategory] as DmList;
            DmList available_recipe = Dm.list();
            for (float i = 1, i__end = subcategory_recipes.len; i <= i__end; i++)
            {
                datum_pipe_info info = subcategory_recipes[i] as datum_pipe_info;

                Dm.append(ref available_recipe, Dm.list(Dm.list(
                    Dm.kv("pipe_name", info.name),
                    Dm.kv("pipe_desc", info.desc),
                    Dm.kv("pipe_index", i),
                    Dm.kv("previews", info.get_preview(p_dir, Dm.num(info == recipe))))));
                if (info == recipe)
                    data["selected_category"] = subcategory;
            }

            Dm.append_at(data, "categories", Dm.list(Dm.list(Dm.kv("cat_name", subcategory), Dm.kv("recipes", available_recipe))));
        }

        DmList init_directions = Dm.list(Dm.kv("north", FALSE), Dm.kv("south", FALSE), Dm.kv("east", FALSE), Dm.kv("west", FALSE));
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(p_init_dir, Dm.num(direction))))
                init_directions[dir2text(direction)] = TRUE;
        }
        data["init_directions"] = init_directions;
        return data;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:207
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        playsound(this, SFX_TOOL_SWITCH, 20, TRUE);

        switch (action)
        {
            case "color":
                paint_color = (string)@params["paint_color"];
                return TRUE;

            case "category":
            {
                category = text2num(@params["category"]);
                object sw__1 = category;
                if (Dm.eq(sw__1, DISPOSALS_CATEGORY))
                    recipe = first_disposal;
                else if (Dm.eq(sw__1, ATMOS_CATEGORY))
                    recipe = first_atmos;
                else if (Dm.eq(sw__1, TRANSIT_CATEGORY))
                    recipe = first_transit;
                p_dir = NORTH;
                return TRUE;
            }

            case "pipe_layers":
            {
                float selected_layers = Dm.num(text2num(@params["pipe_layers"]));

                //is valid
                float valid_layer = FALSE;
                for (float pipe_layer_number = 1, pipe_layer_number__end = MAX_PIPE_LAYERS; pipe_layer_number <= pipe_layer_number__end; pipe_layer_number++)
                {
                    if (!Dm.truthy(Dm.bitand(PIPE_LAYER(pipe_layer_number), selected_layers)))
                        continue;
                    valid_layer = TRUE;
                    break;
                }
                if (!Dm.truthy(valid_layer))
                    return FALSE;

                //append or set the layer
                if (Dm.truthy(multi_layer))
                {
                    if (pipe_layers != selected_layers)
                        pipe_layers = Dm.bitxor(pipe_layers, selected_layers);
                }
                else
                    pipe_layers = selected_layers;

                return TRUE;
            }

            case "toggle_multi_layer":
                if (Dm.truthy(multi_layer))
                    pipe_layers = PIPE_LAYER(Dm.num(max(get_active_pipe_layers())));
                multi_layer = Dm.not(multi_layer);
                break;

            case "pipe_type":
                if (!Dm.truthy(ui_act__recipes))
                    ui_act__recipes = Dm.add(Dm.add(GLOB.disposal_pipe_recipes, GLOB.atmos_pipe_recipes), GLOB.transit_tube_recipes) as DmList;
                recipe = ((DmList)ui_act__recipes[@params["category"]])[text2num(@params["pipe_type"])] as datum_pipe_info;
                p_dir = NORTH;
                break;

            case "setdir":
                p_dir = Dm.num(text2dir(@params["dir"]));
                p_flipped = Dm.num(text2num(@params["flipped"]));
                return TRUE;

            case "mode":
            {
                float? selected_mode = text2num(@params["mode"]);
                mode = Dm.bitxor(Dm.num(mode), Dm.num(selected_mode));
                break;
            }

            case "init_dir_setting":
            {
                float target_dir = Dm.bitxor(p_init_dir, Dm.num(text2dir(@params["dir_flag"])));
                // Refuse to create a smart pipe that can only connect in one direction (it would act weirdly and lack an icon)
                if (Dm.truthy(ISNOTSTUB(target_dir)))
                    p_init_dir = target_dir;
                else
                {
                    to_chat(ui.user, span_warning(I18n.T("\\The []'s screen flashes a warning: Can't configure a pipe to only connect in one direction.", this)));
                    return FALSE;
                }
                break;
            }

            case "init_reset":
                p_init_dir = ALL_CARDINALS;
                break;
        }

        await spark_system.start();
        playsound(get_turf(this), Dm.resource("sound/effects/pop.ogg"), 50, FALSE);
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:290
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom attack_target = interacting_with;
        object dot = NONE;

        if (!ISADVANCEDTOOLUSER(user) || HAS_TRAIT(attack_target, TRAIT_COMBAT_MODE_SKIP_INTERACTION) || istype(attack_target, typeof(turf_open_space_transit)))
            return dot;

        if (istype(attack_target, typeof(obj_item_rpd_upgrade)))
        {
            obj_item_rpd_upgrade rpd_disk = attack_target as obj_item_rpd_upgrade;

            // Check if the upgrade's already present
            if (Dm.truthy(Dm.bitand(Dm.num(rpd_disk.upgrade_flags), Dm.num(upgrade_flags))))
            {
                balloon_alert(user, I18n.T("already installed!"));
                return ITEM_INTERACT_BLOCKING;
            }

            // Adds the upgrade from the disk and then deletes the disk
            upgrade_flags = Dm.bitor(Dm.num(upgrade_flags), Dm.num(rpd_disk.upgrade_flags));
            playsound(loc, Dm.resource("sound/machines/click.ogg"), 50, vary: TRUE);
            balloon_alert(user, I18n.T("upgrade installed"));
            qdel(rpd_disk);
            return ITEM_INTERACT_SUCCESS;
        }

        //So that changing the menu settings doesn't affect the pipes already being built.
        Type queued_pipe_type = recipe.id;
        float queued_pipe_dir = p_dir;
        float queued_pipe_flipped = p_flipped;

        //Unwrench pipe before we build one over/paint it, but only if we're not already running a do_after on it already to prevent a potential runtime.
        if (Dm.truthy(Dm.bitand(Dm.num(mode), DESTROY_MODE)) && Dm.truthy(Dm.bitand(Dm.num(upgrade_flags), RPD_UPGRADE_UNWRENCH)) && istype(attack_target, typeof(obj_machinery_atmospherics)) && !Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, attack_target)))
        {
            object unwrenched = await attack_target.wrench_act(user, this);
            attack_target = unwrenched as atom;
            if (!isatom(unwrenched)) //can return null, FALSE if do_after() fails see /obj/machinery/atmospherics/wrench_act()
                return ITEM_INTERACT_FAILURE;
        }

        if (istype(attack_target, typeof(obj_machinery_atmospherics)) && Dm.truthy(Dm.bitand(Dm.num(mode), BUILD_MODE)))
            attack_target = get_turf(attack_target);

        float can_destroy = FALSE;
        if (Dm.truthy(Dm.bitand(Dm.num(mode), DESTROY_MODE)) && istype(attack_target, typeof(obj_item_pipe)))
            can_destroy = TRUE;
        if (!Dm.truthy(can_destroy))
        {
            can_destroy = Dm.num(is_type_in_list(attack_target, destroyables())); // the list: destroyables(), shared with build_preview (DV352)
        }
        if (Dm.truthy(can_destroy))
        {
            turf ground = get_turf(this);
            playsound(ground, SFX_TOOL_SWITCH, 20, TRUE);
            playsound(ground, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            qdel(attack_target);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(Dm.bitand(Dm.num(mode), REPROGRAM_MODE)))
        {
            // If this is a placed smart pipe, try to reprogram it
            obj_machinery_atmospherics_pipe_smart target_smart_pipe = attack_target as obj_machinery_atmospherics_pipe_smart;
            if (target_smart_pipe != null)
            {
                if (Dm.eq(target_smart_pipe.dir, ALL_CARDINALS))
                {
                    balloon_alert(user, I18n.T("has no unconnected directions!"));
                    return ITEM_INTERACT_FAILURE;
                }
                object old_init_dir = target_smart_pipe.get_init_directions();
                if (Dm.eq(old_init_dir, p_init_dir))
                {
                    balloon_alert(user, I18n.T("already configured!"));
                    return ITEM_INTERACT_FAILURE;
                }
                // Check for differences in unconnected directions
                float target_differences = Dm.bitand(Dm.bitxor(p_init_dir, Dm.num(old_init_dir)), Dm.bitnot(Dm.num(target_smart_pipe.connections)));
                if (!Dm.truthy(target_differences))
                {
                    balloon_alert(user, I18n.T("already configured for its directions!"));
                    return ITEM_INTERACT_FAILURE;
                }

                playsound(get_turf(this), SFX_TOOL_SWITCH, 20, TRUE);

                // Something else could have changed the target's state while we were waiting in do_after
                // Most of the edge cases don't matter, but atmos components being able to have live connections not described by initializable directions sounds like a headache at best and an exploit at worst

                // Double check to make sure that nothing has changed. If anything we were about to change was connected during do_after, abort
                if (Dm.truthy(Dm.bitand(target_differences, Dm.num(target_smart_pipe.connections))))
                {
                    balloon_alert(user, I18n.T("can't configure for its direction!"));
                    return ITEM_INTERACT_FAILURE;
                }
                // Grab the current initializable directions, which may differ from old_init_dir if someone else was working on the same pipe at the same time
                float current_init_dir = Dm.num(target_smart_pipe.get_init_directions());
                // Access p_init_dir directly. The RPD can change target layer and initializable directions (though not pipe type or dir) while working to dispense and connect a component,
                // and have it reflected in the final result. Reprogramming should be similarly consistent.
                float new_init_dir = Dm.bitor(Dm.bitand(current_init_dir, Dm.bitnot(target_differences)), Dm.bitand(p_init_dir, target_differences));
                // Don't make a smart pipe with only one connection
                if (ISSTUB(new_init_dir))
                {
                    balloon_alert(user, I18n.T("no one directional pipes allowed!"));
                    return ITEM_INTERACT_FAILURE;
                }
                target_smart_pipe.set_init_directions(new_init_dir);
                // We're now reconfigured.
                // We can never disconnect from existing connections, but we can connect to previously unconnected directions, and should immediately do so
                float newly_permitted_connections = Dm.bitand(new_init_dir, Dm.bitnot(current_init_dir));
                if (Dm.truthy(newly_permitted_connections))
                {
                    // We're allowed to connect in new directions. Recompute our nodes
                    // Disconnect from everything that is currently connected
                    for (float i = 1, i__end = Dm.num(target_smart_pipe.device_type); i <= i__end; i++)
                    {
                        // This is basically pipe.nullifyNode, but using it here would create a pitfall for others attempting to
                        // copy and paste disconnection code for other components. Welcome to the atmospherics subsystem
                        obj_machinery_atmospherics node = target_smart_pipe.nodes[i] as obj_machinery_atmospherics;
                        if (!Dm.truthy(node))
                            continue;
                        node.disconnect(target_smart_pipe);
                        target_smart_pipe.nodes[i] = null;
                    }
                    // Get our new connections
                    target_smart_pipe.atmos_init();
                    // Connect to our new connections
                    foreach (obj_machinery_atmospherics connected_device in Dm.each<obj_machinery_atmospherics>(target_smart_pipe.nodes))
                    {
                        connected_device.atmos_init();
                        connected_device.add_member(target_smart_pipe);
                    }
                    SSair.add_to_rebuild_queue(target_smart_pipe);
                }
                // Finally, update our internal state - update_pipe_icon also updates dir and connections
                target_smart_pipe.update_pipe_icon();
                user.visible_message(span_notice(I18n.T("[] reprograms \\the [].", user, target_smart_pipe)), span_notice(I18n.T("You reprogram \\the [].", target_smart_pipe)));
                return ITEM_INTERACT_SUCCESS;
            }

            // If this is an unplaced smart pipe, try to reprogram it
            obj_item_pipe_quaternary target_unsecured_pipe = attack_target as obj_item_pipe_quaternary;
            if (target_unsecured_pipe != null && ispath(target_unsecured_pipe.pipe_type, typeof(obj_machinery_atmospherics_pipe_smart)))
            {
                // An unplaced pipe never has any existing connections, so just directly assign the new configuration
                target_unsecured_pipe.p_init_dir = p_init_dir;
                target_unsecured_pipe.update();
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (Dm.truthy(Dm.bitand(Dm.num(mode), BUILD_MODE)))
        {
            object sw__1 = category; //if we've gotten this var, the target is valid
            if (Dm.eq(sw__1, ATMOS_CATEGORY)) //Making pipes
                return Dm.truthy(await do_pipe_build(attack_target, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_FAILURE;

            else if (Dm.eq(sw__1, DISPOSALS_CATEGORY)) //Making disposals pipes
            {
                if (!Dm.truthy(check_can_make_pipe(attack_target)))
                    return ITEM_INTERACT_FAILURE;
                attack_target = get_turf(attack_target);
                if (isclosedturf(attack_target))
                {
                    balloon_alert(user, I18n.T("target is blocked!"));
                    return ITEM_INTERACT_FAILURE;
                }
                playsound(get_turf(this), SFX_TOOL_SWITCH, 20, TRUE);

                if (!Dm.truthy(await do_after(user, disposal_build_speed, target: attack_target)))
                    return ITEM_INTERACT_FAILURE;

                obj_structure_disposalconstruct new_disposals_segment = Dm.@new<obj_structure_disposalconstruct>(attack_target, queued_pipe_type, queued_pipe_dir, queued_pipe_flipped);

                if (!Dm.truthy(new_disposals_segment.can_place()))
                {
                    balloon_alert(user, I18n.T("not enough room!"));
                    qdel(new_disposals_segment);
                    return ITEM_INTERACT_FAILURE;
                }

                playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);

                new_disposals_segment.add_fingerprint(user);
                new_disposals_segment.update_appearance();
                if (Dm.truthy(Dm.bitand(Dm.num(mode), WRENCH_MODE)))
                    await new_disposals_segment.wrench_act(user, this);
                return ITEM_INTERACT_SUCCESS;
            }

            else if (Dm.eq(sw__1, TRANSIT_CATEGORY)) //Making transit tubes
            {
                if (!Dm.truthy(check_can_make_pipe(attack_target)))
                    return ITEM_INTERACT_FAILURE;
                attack_target = get_turf(attack_target);
                if (isclosedturf(attack_target))
                {
                    balloon_alert(user, I18n.T("something in the way!"));
                    return ITEM_INTERACT_FAILURE;
                }

                turf target_turf = get_turf(attack_target);
                if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
                {
                    balloon_alert(user, I18n.T("something in the way!"));
                    return ITEM_INTERACT_FAILURE;
                }

                playsound(get_turf(this), SFX_TOOL_SWITCH, 20, TRUE);
                if (!Dm.truthy(await do_after(user, transit_build_speed, target: attack_target)))
                    return ITEM_INTERACT_FAILURE;

                playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
                if (queued_pipe_type == typeof(obj_structure_c_transit_tube_pod))
                {
                    obj_structure_c_transit_tube_pod pod = Dm.@new<obj_structure_c_transit_tube_pod>(attack_target);
                    pod.add_fingerprint(user);
                    if (Dm.truthy(Dm.bitand(Dm.num(mode), WRENCH_MODE)))
                        await pod.wrench_act(user, this);
                }

                else
                {
                    obj_structure_c_transit_tube tube = Dm.@new(queued_pipe_type, attack_target) as obj_structure_c_transit_tube;
                    await tube.setDir(queued_pipe_dir);

                    if (Dm.truthy(queued_pipe_flipped))
                    {
                        await tube.setDir(turn(queued_pipe_dir, 45 + ROTATION_FLIP));
                        tube.post_rotation(user, ROTATION_FLIP);
                    }

                    tube.add_fingerprint(user);
                    if (Dm.truthy(Dm.bitand(Dm.num(mode), WRENCH_MODE)))
                        await tube.wrench_act(user, this);
                }
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RPD.dm:483
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = NONE;

        obj_machinery_atmospherics target = interacting_with as obj_machinery_atmospherics;
        if (target == null)
            return dot;

        if (Dm.truthy(target.pipe_color) && Dm.truthy(target.piping_layer))
        {
            paint_color = (string)GLOB.pipe_color_name[target.pipe_color];
            pipe_layers = PIPE_LAYER(target.piping_layer);
            balloon_alert(user, I18n.T("color/layer copied"));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    /**
     * Can we make a pipe on the target
     * Arguments
     *
     * * atom/target_of_attack - the target we are trying to build a pipe on
     */
    // tg: code/game/objects/items/rcd/RPD.dm:501
    public virtual object check_can_make_pipe(atom target_of_attack = null)
    {
        if (isturf(target_of_attack))
            return TRUE;

        //make sure what we're clicking is valid for the current category
        check_can_make_pipe__make_pipe_whitelist ??= typecacheof(
            Dm.list(
                    typeof(obj_structure_lattice),
                    typeof(obj_structure_girder),
                    typeof(obj_item_pipe),
                    typeof(obj_structure_window),
                    typeof(obj_structure_grille)
                )
        ) as DmList;
        return is_type_in_typecache(target_of_attack, check_can_make_pipe__make_pipe_whitelist);
    }

    /**
     * Build pipe on the target
     * Arguments
     *
     * * atom/atom_to_target - the target we are trying to build the pipe on
     * * mob/user - mob performing the action
     */
    // tg: code/game/objects/items/rcd/RPD.dm:527
    public virtual async DmTask<object> do_pipe_build(atom atom_to_target = null, mob user = null)
    {
        if (!Dm.truthy(check_can_make_pipe(atom_to_target)))
            return FALSE;

        //So that changing the menu settings doesn't affect the pipes already being built.
        Type queued_pipe_type = recipe.id;
        float queued_pipe_dir = p_dir;
        float queued_pipe_flipped = p_flipped;

        DmList pipe_layer_numbers = get_active_pipe_layers() as DmList;
        foreach (object layer_to_build in Dm.each(pipe_layer_numbers))
        {
            playsound(get_turf(this), SFX_TOOL_SWITCH, 20, vary: TRUE);
            if (!Dm.truthy(await do_after(user, atmos_build_speed, target: atom_to_target)))
                return FALSE;
            if (!Dm.truthy(recipe.all_layers) && (Dm.eq(layer_to_build, 1) || Dm.eq(layer_to_build, MAX_PIPE_LAYERS)))
            {
                balloon_alert(user, I18n.T("can't build on layer []!", layer_to_build));
                if (Dm.truthy(multi_layer))
                    continue;
                return FALSE;
            }
            playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            if (recipe.type == typeof(datum_pipe_info_meter))
            {
                obj_item_pipe_meter new_meter = Dm.@new<obj_item_pipe_meter>(get_turf(atom_to_target));
                new_meter.setAttachLayer(layer_to_build);
                if (Dm.truthy(Dm.bitand(Dm.num(mode), WRENCH_MODE)))
                    await new_meter.wrench_act(user as mob_living, this);
            }
            else
            {
                Type path = queued_pipe_type;
                Type pipe_item_type = Dm.initial(path, (obj_machinery_atmospherics p) => p.construction_type) ?? typeof(obj_item_pipe);
                obj_item_pipe pipe_type = await Dm.new_async(pipe_item_type,
                    get_turf(atom_to_target),
                    queued_pipe_type,
                    queued_pipe_dir,
                    null,
                    GLOB.pipe_paint_colors[paint_color],
                    ispath(queued_pipe_type, typeof(obj_machinery_atmospherics_pipe_smart)) ? p_init_dir : null
                ) as obj_item_pipe;
                if (Dm.truthy(queued_pipe_flipped) && istype(pipe_type, typeof(obj_item_pipe_trinary_flippable)))
                {
                    obj_item_pipe_trinary_flippable new_flippable_pipe = pipe_type as obj_item_pipe_trinary_flippable;
                    new_flippable_pipe.flipped = queued_pipe_flipped;
                }

                pipe_type.update();
                pipe_type.add_fingerprint(user);
                pipe_type.set_piping_layer(layer_to_build);
                if (ispath(queued_pipe_type, typeof(obj_machinery_atmospherics)) && !ispath(queued_pipe_type, typeof(obj_machinery_atmospherics_pipe_color_adapter)))
                    await pipe_type.add_atom_colour(GLOB.pipe_paint_colors[paint_color], FIXED_COLOUR_PRIORITY);
                if (Dm.truthy(Dm.bitand(Dm.num(mode), WRENCH_MODE)))
                    await pipe_type.wrench_act(user as mob_living, this);
            }
        }
        return TRUE;
    }

    ///Changes the piping layer when the mousewheel is scrolled up or down.
    // tg: code/game/objects/items/rcd/RPD.dm:579
    [SignalHandler]
    public virtual void mouse_wheeled(mob source_mob = null, atom A = null, object delta_x = null, object delta_y = null, object @params = null)
    {
        if (Dm.truthy(params2list(@params)?[SHIFT_CLICK]))
        {
            turn_build(source_mob, delta_y);
            return;
        }
        if (Dm.truthy(multi_layer))
        {
            balloon_alert(source_mob, I18n.T("turn off multi layer!"));
            return;
        }
        if (Dm.truthy(INCAPACITATED_IGNORING(source_mob, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_STASIS))))
            return;
        if (!Dm.eq(source_mob.get_active_held_item(), this))
            return;

        if (Dm.lt(delta_y, 0))
            pipe_layers = min(PIPE_LAYER(MAX_PIPE_LAYERS), Dm.shl(pipe_layers, 1));
        else if (Dm.gt(delta_y, 0))
            pipe_layers = max(PIPE_LAYER(1), Dm.shr(pipe_layers, 1));
        else //mice with side-scrolling wheels are apparently a thing and fuck this up
            return;
        SStgui.update_uis(this);
        balloon_alert(source_mob, I18n.T("set pipe layer to []", ((DmList)get_active_pipe_layers())[1]));
    }
}

// D320 (DV352): the 3D window has one button per recipe in place of tg's one per direction; Shift with the wheel turns the next build
// through the recipe's directions (its window previews, in their order) and the client's ghost shows it on the aimed tile.
public partial class obj_item_pipe_dispenser
{
    /// <summary>DEVIATION(DV352): Shift and the wheel: the recipe's next direction (down) or the one before (up), as the window's setdir act sets it.</summary>
    public void turn_build(mob source_mob, object delta_y)
    {
        if (recipe == null || Dm.eq(delta_y, 0))
            return;
        if (Dm.truthy(INCAPACITATED_IGNORING(source_mob, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_STASIS))))
            return;
        if (!Dm.eq(source_mob.get_active_held_item(), this))
            return;
        DmList rows = recipe.get_preview(p_dir, TRUE) as DmList;
        int count = (int)(rows?.len ?? 0);
        if (count < 2)
            return;
        int at = 0;
        for (int i = 1; i <= count; i++)
        {
            if (Dm.truthy(((DmList)rows[i])["selected"]))
                at = i;
        }
        int next = Dm.lt(delta_y, 0) ? at % count + 1 : (at <= 1 ? count : at - 1);
        DmList row = (DmList)rows[next];
        p_dir = Dm.num(text2dir(row["dir"]));
        p_flipped = Dm.num(row["flipped"]);
        playsound(this, SFX_TOOL_SWITCH, 20, TRUE);
        SStgui.update_uis(this);
        balloon_alert(source_mob, lowertext(row["dir_name"]));
    }

    // DV352: a fitting of the next build, never placed, that answers tg's conflict check dry (obj_item_pipe.placement_conflict)
    private obj_item_pipe build_probe;

    /// <summary>
    /// DEVIATION(DV352): what a click on the hovered thing would build, as interact_with_atom decides it: the recipe's window preview of the
    /// chosen direction on the turf it would go to, coloured and shifted to the first active layer with no conflict; null where the click
    /// takes, unwrenches or reprograms something, or builds nothing (out of reach, blocked, every layer taken).
    /// </summary>
    public override BuildPreview? build_preview(mob user)
    {
        atom target = user?.client?.dm_mouse_over;
        if (recipe == null || !Dm.truthy(Dm.bitand(Dm.num(mode), BUILD_MODE)) || target == null || target.gc_destroyed != null || user is not mob_living living)
            return null;
        if (!ISADVANCEDTOOLUSER(living) || HAS_TRAIT(target, TRAIT_COMBAT_MODE_SKIP_INTERACTION) || istype(target, typeof(turf_open_space_transit)) || target is obj_item_rpd_upgrade)
            return null;
        if (!Dm.truthy(Waiting.NoSleep(target.Adjacent(user))))
            return null;
        bool destroy = Dm.truthy(Dm.bitand(Dm.num(mode), DESTROY_MODE));
        if (destroy && Dm.truthy(Dm.bitand(Dm.num(upgrade_flags), RPD_UPGRADE_UNWRENCH)) && target is obj_machinery_atmospherics)
            return null; // the click unwrenches it
        if (target is obj_machinery_atmospherics)
            target = get_turf(target);
        if ((destroy && target is obj_item_pipe) || Dm.truthy(is_type_in_list(target, destroyables())))
            return null; // the click takes it away
        if (Dm.truthy(Dm.bitand(Dm.num(mode), REPROGRAM_MODE)) && target is obj_item_pipe_quaternary loose && ispath(loose.pipe_type, typeof(obj_machinery_atmospherics_pipe_smart)))
            return null; // the click reprograms it
        if (!Dm.truthy(check_can_make_pipe(target)) || get_turf(target) is not turf place)
            return null;

        // the window's preview picture of the chosen direction (a one-direction recipe's only one): the "pipes" sheet's files by category
        int dir = (int)NORTH;
        foreach (DmList row in Dm.each<DmList>(recipe.get_preview(p_dir, TRUE)))
        {
            if (Dm.truthy(row["selected"]))
            {
                dir = (int)Dm.num(text2dir(row["dir"]));
                break;
            }
        }
        if (!Dm.eq(category, ATMOS_CATEGORY))
        {
            bool transit = Dm.eq(category, TRANSIT_CATEGORY);
            if (isclosedturf(place) || (transit && Dm.truthy(Waiting.NoSleep(place.is_blocked_turf(exclude_mobs: TRUE)))))
                return null;
            return new BuildPreview(transit ? "icons/obj/pipes_n_cables/transit_tube.dmi" : "icons/obj/pipes_n_cables/disposal.dmi", Dm.str(recipe.icon_state), dir, null, 0, 0, place);
        }
        // tg's do_pipe_build: the first active layer the recipe may use and no machine there conflicts with
        foreach (object layer in Dm.each(get_active_pipe_layers()))
        {
            if (!Dm.truthy(recipe.all_layers) && (Dm.eq(layer, 1) || Dm.eq(layer, MAX_PIPE_LAYERS)))
                continue;
            if (recipe is not datum_pipe_info_meter && probe_conflict(place, Dm.num(layer), living))
                continue;
            // tg: PIPING_LAYER_SHIFT (atmos_helpers.dm:8)
            int shift = ((int)Dm.num(layer) - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            return new BuildPreview("icons/obj/pipes_n_cables/pipe_item.dmi", Dm.str(recipe.icon_state), dir, Dm.str(GLOB.pipe_paint_colors[paint_color]),
                (dir & (int)Dm.bitor(NORTH, SOUTH)) != 0 ? shift : 0, (dir & (int)Dm.bitor(EAST, WEST)) != 0 ? shift : 0, place);
        }
        return null;
    }

    // The probe set as do_pipe_build makes its fitting (RPD.dm:548-560), then tg's wrench_act conflict check, dry.
    private bool probe_conflict(turf place, float layer, mob_living user)
    {
        Type item_type = Dm.initial(recipe.id, (obj_machinery_atmospherics p) => p.construction_type) ?? typeof(obj_item_pipe);
        object init_dir = ispath(recipe.id, typeof(obj_machinery_atmospherics_pipe_smart)) ? p_init_dir : null;
        if (build_probe == null || build_probe.gc_destroyed != null || build_probe.GetType() != item_type)
        {
            qdel(build_probe);
            build_probe = Dm.@new(item_type, null, recipe.id, p_dir, null, null, init_dir) as obj_item_pipe;
        }
        build_probe.pipe_type = recipe.id;
        Waiting.NoSleep(build_probe.setDir(p_dir));
        build_probe.p_init_dir = Dm.num(init_dir ?? SOUTH);
        if (build_probe is obj_item_pipe_trinary_flippable flippable)
            flippable.flipped = p_flipped;
        build_probe.set_piping_layer(layer);
        return build_probe.placement_conflict(place, user, dry: true) != null;
    }

    private DmList destroyables() => interact_with_atom__destroyables ??= Dm.list(
        typeof(obj_structure_disposalconstruct),
        typeof(obj_structure_c_transit_tube),
        typeof(obj_structure_c_transit_tube_pod),
        typeof(obj_item_pipe_meter),
        typeof(obj_structure_disposalpipe_broken));
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/rcd/RPD.dm:68  /obj/item/pipe_dispenser var/static/datum/pipe_info/first_atmos
    internal datum_pipe_info obj_item_pipe_dispenser__first_atmos;

    // tg: code/game/objects/items/rcd/RPD.dm:70  var/static/datum/pipe_info/first_disposal
    internal datum_pipe_info obj_item_pipe_dispenser__first_disposal;

    // tg: code/game/objects/items/rcd/RPD.dm:72  var/static/datum/pipe_info/first_transit
    internal datum_pipe_info obj_item_pipe_dispenser__first_transit;

    // tg: code/game/objects/items/rcd/RPD.dm:259  ui_act's var/static/list/recipes (null until the first "pipe_type" act)
    internal DmList obj_item_pipe_dispenser__ui_act__recipes;

    // tg: code/game/objects/items/rcd/RPD.dm:329  interact_with_atom's var/static/list/destroyables (no datum created: made at the first use)
    internal DmList obj_item_pipe_dispenser__interact_with_atom__destroyables;

    // tg: code/game/objects/items/rcd/RPD.dm:509  check_can_make_pipe's var/static/list/make_pipe_whitelist (typecacheof creates no datum)
    internal DmList obj_item_pipe_dispenser__check_can_make_pipe__make_pipe_whitelist;
}
