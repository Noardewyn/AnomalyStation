// Translated from tgstation (snapshot 1d99bd7f): code/modules/station_goals/bsa.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: the bluespace artillery goal, the cannon's three parts (generator, bore and fusor) linked with a
// multitool, the full cannon that fires its beam across the level and blows up its target, the filler, and the control console with
// its BluespaceArtillery window (the deployment preview, the targeting by GPS signal and the fire button the heads unlock at the keycard
// reader). GLOB.bsa_unlock (:3) is declared with the keycard reader that toggles it (security_levels/keycard_authentication.cs). One
// change from tg: deploying the cannon deletes the bore and the generator it replaces (tg deletes only their weak references, so the two
// parts stayed under the cannon, a slip of the weakref conversion). The var blocks are generated (D111).

// Crew has to build a bluespace cannon
// Cargo orders part for high price
// Requires high amount of power
// Requires high level stock parts
// tg: code/modules/station_goals/bsa.dm:16
public partial class datum_station_goal_bluespace_cannon : datum_station_goal
{
    // tg: code/modules/station_goals/bsa.dm:19
    public override object get_report()
    {
        return Dm.list(
            I18n.T("Our military presence is inadequate in your sector."),
            I18n.T("We need you to construct BSA-[] Artillery position aboard your station.", rand(1, 99)),
            "",
            I18n.T("Base parts are available for shipping via cargo."),
            I18n.T("<i>- Nanotrasen Naval Command</i>")
        ).Join("\n");
    }

    // tg: code/modules/station_goals/bsa.dm:28
    public override void on_report()
    {
        //Unlock BSA parts
        var P = SSshuttle.supply_packs[typeof(datum_supply_pack_engineering_bsa)] as datum_supply_pack_engineering_bsa;
        P.order_flags = Dm.num(Dm.bitor(P.order_flags, ORDER_SPECIAL_ENABLED));
    }

    // tg: code/modules/station_goals/bsa.dm:33
    public override object check_completion()
    {
        if (Dm.truthy(base.check_completion()))
            return TRUE;
        var B = Dm.locate<obj_machinery_bsa_full>(world);
        if (B != null && !Dm.truthy(B.machine_stat))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/station_goals/bsa.dm:41
public partial class obj_machinery_bsa : obj_machinery
{
    // Spatial defines (all in tiles)
    // tg: code/modules/station_goals/bsa.dm:6-10  (file-local, #undef at :478-482)
    internal const int BSA_WIDTH = 11;
    internal const int BSA_HEIGHT = 3;
    internal const int BSA_Y_OFFSET = -1;
    internal const int BSA_X_OFFSET_WEST = -6;
    internal const int BSA_X_OFFSET_EAST = -4;

    // tg: code/modules/station_goals/bsa.dm:46
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 1 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/station_goals/bsa.dm:51
public partial class obj_machinery_bsa_back : obj_machinery_bsa
{
    // tg: code/modules/station_goals/bsa.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:60
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        var M = tool as obj_item_multitool; // tg's obj/item/multitool/M (rules 3.3)
        M.set_buffer(this);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/station_goals/bsa.dm:65
public partial class obj_machinery_bsa_front : obj_machinery_bsa
{
    // tg: code/modules/station_goals/bsa.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:74
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        var M = tool as obj_item_multitool; // tg's obj/item/multitool/M (rules 3.3)
        M.set_buffer(this);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/station_goals/bsa.dm:79
public partial class obj_machinery_bsa_middle : obj_machinery_bsa
{
    // tg: code/modules/station_goals/bsa.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:90
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        var multitool = tool as obj_item_multitool; // tg's obj/item/multitool/tool (rules 3.3: the slot keeps obj/item)
        object dot = NONE;

        if (multitool.buffer is obj_machinery_bsa_back)
        {
            back_ref = WEAKREF(multitool.buffer) as datum_weakref;
            to_chat(user, span_notice(I18n.T("You link [] with [].", this, multitool.buffer)));
            multitool.set_buffer(null);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (multitool.buffer is obj_machinery_bsa_front)
        {
            front_ref = WEAKREF(multitool.buffer) as datum_weakref;
            to_chat(user, span_notice(I18n.T("You link [] with [].", this, multitool.buffer)));
            multitool.set_buffer(null);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:104
    public virtual object check_completion()
    {
        var front = front_ref?.resolve() as obj_machinery_bsa_front;
        var back = back_ref?.resolve() as obj_machinery_bsa_back;
        if (front == null || back == null)
            return I18n.T("No linked parts detected!");
        if (!Dm.truthy(front.anchored) || !Dm.truthy(back.anchored) || !Dm.truthy(anchored))
            return I18n.T("Linked parts unwrenched!");
        if (front.y != y || back.y != y || !(front.x > x && back.x < x || front.x < x && back.x > x) || front.z != z || back.z != z)
            return I18n.T("Parts misaligned!");
        if (!Dm.truthy(has_space()))
            return I18n.T("Not enough free space!");
        return null;
    }

    /**
     * Proc to check if the BSA has the required 11 x 1 block space to deploy.
     */
    // tg: code/modules/station_goals/bsa.dm:119
    public virtual object has_space()
    {
        object x_offset = get_directional_offset();
        if (!Dm.truthy(x_offset))
            return FALSE;

        turf @base = get_turf(this);
        bool blocked = false;
        foreach (turf T in Dm.each_cast<turf>(CORNER_BLOCK_OFFSET(@base, BSA_WIDTH, BSA_HEIGHT, Dm.num(x_offset), BSA_Y_OFFSET)))
        {
            if (Dm.truthy(T.density) || isspaceturf(T))
            {
                blocked = true;
                Dm.@new<obj_effect_temp_visual_point>(T);
            }
        }
        if (blocked)
            return FALSE;

        return TRUE;
    }

    /**
     * Returns the x offset (in tiles) associated with the BSA's direction
     */
    // tg: code/modules/station_goals/bsa.dm:138
    public virtual object get_directional_offset()
    {
        object cannon_dir = get_cannon_direction();
        if (Dm.eq(cannon_dir, EAST))
            return BSA_X_OFFSET_EAST;
        if (Dm.eq(cannon_dir, WEST))
            return BSA_X_OFFSET_WEST;
        return null;
    }

    // tg: code/modules/station_goals/bsa.dm:146
    public virtual object get_cannon_direction()
    {
        var front = front_ref?.resolve() as obj_machinery_bsa_front;
        var back = back_ref?.resolve() as obj_machinery_bsa_back;
        if (front == null || back == null)
            return null;
        if (front.x > x && back.x < x)
            return EAST;
        else if (front.x < x && back.x > x)
            return WEST;
        return null;
    }
}

// tg: code/modules/station_goals/bsa.dm:157
public partial class obj_machinery_bsa_full : obj_machinery_bsa
{
    // tg: code/modules/station_goals/bsa.dm:162  var/static/mutable_appearance/top_layer (a type static, rules 10.6)
    private static ref mutable_appearance top_layer => ref TgState.Current.obj_machinery_bsa_full__top_layer;

    // tg: code/modules/station_goals/bsa.dm:171
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        return FALSE;
    }

    // tg: code/modules/station_goals/bsa.dm:174
    public virtual object get_front_turf()
    {
        if (Dm.eq(dir, WEST))
            return locate(x - 7, y, z);
        if (Dm.eq(dir, EAST))
            return locate(x + 7, y, z);
        return get_turf(this);
    }

    // tg: code/modules/station_goals/bsa.dm:182
    public virtual object get_back_turf()
    {
        if (Dm.eq(dir, WEST))
            return locate(x + 5, y, z);
        if (Dm.eq(dir, EAST))
            return locate(x - 5, y, z);
        return get_turf(this);
    }

    // tg: code/modules/station_goals/bsa.dm:190
    public virtual object get_target_turf()
    {
        if (Dm.eq(dir, WEST))
            return locate(1, y, z);
        if (Dm.eq(dir, EAST))
            return locate(world.maxx, y, z);
        return get_turf(this);
    }

    // tg: code/modules/station_goals/bsa.dm:198
    [DmParams("mapload", "cannon_direction")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object cannon_direction = Dm.arg(__extra, 0) ?? WEST;
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(cannon_direction, WEST))
        {
            Waiting.NoSleep(setDir(WEST));
            icon_state = "cannon_west";
        }
        else if (Dm.eq(cannon_direction, EAST))
        {
            Waiting.NoSleep(setDir(EAST));
            pixel_x = -128;
            bound_x = -128;
            icon_state = "cannon_east";
        }
        get_layer();
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:211
    public virtual void get_layer()
    {
        top_layer = mutable_appearance(icon, layer: ABOVE_MOB_LAYER) as mutable_appearance;
        if (Dm.eq(dir, WEST))
            top_layer.icon_state = "top_west";
        else if (Dm.eq(dir, EAST))
            top_layer.icon_state = "top_east";
        add_overlay(Dm.list(top_layer));
    }

    // tg: code/modules/station_goals/bsa.dm:220
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(same_z_layer))
        {
            await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
            return;
        }
        cut_overlay(Dm.list(top_layer));
        get_layer();
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/modules/station_goals/bsa.dm:227
    public virtual async DmTask fire(mob user = null, turf bullseye = null)
    {
        turf point = get_front_turf() as turf;
        turf target = get_target_turf() as turf;
        atom blocker = null;
        foreach (object T in Dm.each(get_line(get_step(point, dir), target)))
        {
            turf tile = T as turf;
            if (Dm.truthy(Dm.bitand(tile.SEND_SIGNAL(COMSIG_ATOM_BSA_BEAM, Dm.list(tile)), COMSIG_ATOM_BLOCKS_BSA_BEAM)))
            {
                blocker = tile;
            }
            else
            {
                foreach (object AM in Dm.each(tile))
                {
                    atom_movable stuff = AM as atom_movable;
                    if (Dm.truthy(Dm.bitand(stuff.SEND_SIGNAL(COMSIG_ATOM_BSA_BEAM, Dm.list(stuff)), COMSIG_ATOM_BLOCKS_BSA_BEAM)))
                    {
                        blocker = stuff;
                        break;
                    }
                }
            }
            if (blocker != null)
            {
                target = tile;
                break;
            }
            else
            {
                Dm.append(ref SSexplosions.highturf, tile); //also fucks everything else on the turf
            }
        }
        point.Beam(target, icon_state: "bsa_beam", time: 5 * SECONDS, maxdistance: world.maxx); //ZZZAP
        Dm.@new<obj_effect_temp_visual_bsa_splash>(point, dir);

        notify_ghosts(
            I18n.T("The Bluespace Artillery has been fired!"),
            source: bullseye,
            header: I18n.T("KABOOM!"));

        if (blocker == null)
        {
            message_admins(Dm.text("[] has launched a bluespace artillery strike targeting [].", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(bullseye)));
            user.log_message(Dm.text("has launched a bluespace artillery strike targeting [].", AREACOORD(bullseye)), LOG_GAME);
            await explosion(bullseye, devastation_range: Dm.num(ex_power), heavy_impact_range: Dm.num(ex_power) * 2, light_impact_range: Dm.num(ex_power) * 4, explosion_cause: this);
            Dm.@new<obj_effect_temp_visual_bsa_impact>(bullseye);
        }
        else
        {
            message_admins(Dm.text("[] has launched a bluespace artillery strike targeting [] but it was blocked by [] at [].", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(bullseye), blocker, ADMIN_VERBOSEJMP(target)));
            user.log_message(Dm.text("has launched a bluespace artillery strike targeting [] but it was blocked by [] at [].", AREACOORD(bullseye), blocker, AREACOORD(target)), LOG_GAME);
        }
    }
}

// tg: code/modules/station_goals/bsa.dm:264
public partial class obj_structure_filler : obj_structure
{
    // tg: code/modules/station_goals/bsa.dm:271
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}

// tg: code/modules/station_goals/bsa.dm:274
public partial class obj_machinery_computer_bsa_control : obj_machinery_computer
{
    // tg: code/modules/station_goals/bsa.dm:298
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visualization_effects = Dm.list();
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:302
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (Dm.truthy(visualizing_position))
            stop_visualizing();
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:307
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        await base.on_set_machine_stat(old_value);
        if (Dm.truthy(machine_stat) && Dm.truthy(visualizing_position))
            stop_visualizing();
    }

    // tg: code/modules/station_goals/bsa.dm:312
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/modules/station_goals/bsa.dm:315
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "BluespaceArtillery", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:322
    public override async DmTask<object> ui_data(mob user = null)
    {
        var cannon = cannon_ref?.resolve() as obj_machinery_bsa_full;
        DmList data = Dm.list();
        data["connected"] = cannon;
        data["notice"] = notice;
        data["unlocked"] = GLOB.bsa_unlock;
        data["visualizing"] = visualizing_position;
        if (Dm.truthy(target))
            data["target"] = get_target_name();
        return data;
    }

    // tg: code/modules/station_goals/bsa.dm:333
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "build":
                cannon_ref = WEAKREF(await deploy() as datum) as datum_weakref;
                stop_visualizing();
                dot = TRUE;
                break;
            case "visualize":
                if (!Dm.truthy(visualizing_position))
                    await start_visualizing();
                break;
            case "unvisualize":
                if (Dm.truthy(visualizing_position))
                    stop_visualizing();
                break;
            case "fire":
            {
                var cannon = cannon_ref.resolve() as obj_machinery_bsa_full;
                if (Dm.truthy(await cannon.use_energy(cannon.power_used_per_shot, force: FALSE)))
                    await fire(ui.user);
                else
                    to_chat(ui.user, span_warning(I18n.T("Insufficient power!")));
                dot = TRUE;
                break;
            }
            case "recalibrate":
                await calibrate(usr);
                dot = TRUE;
                break;
        }
        update_appearance();
        return dot;
    }

    // tg: code/modules/station_goals/bsa.dm:361
    public virtual async DmTask calibrate(mob user = null)
    {
        if (!Dm.truthy(GLOB.bsa_unlock))
            return;
        DmList gps_locators = Dm.list();
        foreach (datum_component_gps G in Dm.each<datum_component_gps>(GLOB.GPS_list)) //nulls on the list somehow
        {
            if (Dm.truthy(G.tracking))
                gps_locators[G.gpstag] = G;
        }

        DmList options = gps_locators;
        if (Dm.truthy(area_aim))
            Dm.append(ref options, GLOB.teleportlocs);
        object victim = await tgui_input_list(user, I18n.T("Select target"), I18n.T("Artillery Targeting"), options);
        if (victim == null)
            return;
        if (options[victim] == null)
            return;
        target = options[victim];
        log_game(Dm.text("[] has aimed the bluespace artillery strike at [].", key_name(user), target));
    }

    // tg: code/modules/station_goals/bsa.dm:380
    public virtual object get_target_name()
    {
        if (target is area)
            return get_area_name(target as area, TRUE);
        else if (target is datum_component_gps)
        {
            var G = (datum_component_gps)target;
            return G.gpstag;
        }
        return null;
    }

    // tg: code/modules/station_goals/bsa.dm:387
    public virtual object get_impact_turf()
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return get_turf(this);
        else if (target is area)
            return pick(get_area_turfs(target) as DmList);
        else if (target is datum_component_gps)
        {
            var G = (datum_component_gps)target;
            return get_turf(G.parent as atom);
        }
        return null;
    }

    // tg: code/modules/station_goals/bsa.dm:396
    public virtual async DmTask fire(mob user = null)
    {
        var cannon = cannon_ref?.resolve() as obj_machinery_bsa_full;
        if (cannon == null)
        {
            notice = I18n.T("No Cannon Exists!");
            return;
        }
        if (Dm.truthy(cannon.machine_stat))
        {
            notice = I18n.T("Cannon unpowered!");
            return;
        }
        notice = null;
        turf target_turf = get_impact_turf() as turf;
        await cannon.fire(user, target_turf);
    }

    /// Sets `notice` and returns null if the BSA isn't complete.
    /// Returns the BSA's centerpiece if everything's alright.
    // tg: code/modules/station_goals/bsa.dm:410
    public virtual object check_completion()
    {
        var centerpiece = Dm.locate<obj_machinery_bsa_middle>(range(7));
        if (centerpiece == null)
        {
            notice = I18n.T("No BSA parts detected nearby.");
            return null;
        }
        notice = centerpiece.check_completion();
        if (Dm.truthy(notice))
            return null;
        return centerpiece;
    }

    /// Prior to BSA deployment, indicates the space which the BSA will occupy using effects.
    // tg: code/modules/station_goals/bsa.dm:421
    public virtual async DmTask start_visualizing()
    {
        var prebuilt = Dm.locate<obj_machinery_bsa_full>(range(7)); //In case of adminspawn
        if (prebuilt != null)
        {
            cannon_ref = WEAKREF(prebuilt) as datum_weakref;
            return;
        }

        var centerpiece = check_completion() as obj_machinery_bsa_middle;
        if (Dm.truthy(notice))
            return;

        visualization_center = get_turf(centerpiece);
        visualization_front = get_turf(centerpiece.front_ref?.resolve() as atom);
        visualizing_position = TRUE;

        object x_offset = centerpiece.get_directional_offset();
        turf @base = get_turf(centerpiece.loc);
        foreach (turf deployment_turf in Dm.each_cast<turf>(CORNER_BLOCK_OFFSET(@base, obj_machinery_bsa.BSA_WIDTH, obj_machinery_bsa.BSA_HEIGHT, Dm.num(x_offset), obj_machinery_bsa.BSA_Y_OFFSET)))
        {
            object bsa_effect = Dm.@new(visualization_type, deployment_turf);
            Dm.append(ref visualization_effects, bsa_effect);
        }
    }

    /// Clear all effects indicating where the BSA will deploy.
    // tg: code/modules/station_goals/bsa.dm:442
    public virtual void stop_visualizing()
    {
        visualizing_position = FALSE;
        visualization_front = null;
        visualization_center = null;
        foreach (obj_effect_clear_color_green deployment_visualizer in Dm.each<obj_effect_clear_color_green>(visualization_effects))
            qdel(deployment_visualizer);
    }

    // tg: code/modules/station_goals/bsa.dm:449
    public virtual async DmTask<object> deploy(float? force = null)
    {
        var centerpiece = check_completion() as obj_machinery_bsa_middle;
        if (centerpiece == null)
            return null;
        // `check_completion()` occurs both here and at `start_visualizing()`
        // These checks must only occur after visualization.
        if (visualization_center != get_turf(centerpiece))
        {
            notice = I18n.T("The BSA has been moved mid-deployment.");
            return null;
        }
        if (visualization_front != get_turf(centerpiece.front_ref?.resolve() as atom))
        {
            notice = I18n.T("The BSA has been rotated mid-deployment.");
            return null;
        }

        //Totally nanite construction system not an immersion breaking spawning
        await do_smoke(4, get_turf(centerpiece), get_turf(centerpiece));
        var cannon = Dm.@new<obj_machinery_bsa_full>(get_turf(centerpiece), centerpiece.get_cannon_direction());
        // tg QDEL_NULLs the parts' weak references, which deletes the references and leaves the bore and the generator standing under the
        // cannon (a slip of the weakref conversion; before it, :465-466 deleted the parts): the port deletes the parts with them
        qdel(centerpiece.front_ref?.resolve());
        QDEL_NULL(ref centerpiece.front_ref);
        qdel(centerpiece.back_ref?.resolve());
        QDEL_NULL(ref centerpiece.back_ref);
        qdel(centerpiece);
        return cannon;
    }

    // tg: code/modules/station_goals/bsa.dm:470
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        balloon_alert(user, I18n.T("rigged to explode"));
        to_chat(user, span_warning(I18n.T("You emag [] and hear the focusing crystal short out. You get the feeling it wouldn't be wise to stand near [] when the BSA fires...", this, this)));
        return TRUE;
    }
}

// tg: code/modules/station_goals/bsa.dm:162  var/static/mutable_appearance/top_layer
public sealed partial class TgState
{
    internal mutable_appearance obj_machinery_bsa_full__top_layer;
}
