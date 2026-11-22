// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/stairs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z): the whole file. The station map's blips are not ported (the minimap, as the mechs' blips): the blip procs
// keep tg's control flow around the missing calls.

// dir determines the direction of travel to go upwards
// stairs require /turf/open/openspace as the tile above them to work, unless your stairs have 'force_open_above' set to TRUE
// multiple stair objects can be chained together; the Z level transition will happen on the final stair object in the chain

// tg: code/game/objects/structures/stairs.dm:14
public partial class obj_structure_stairs : obj_structure
{
    // tg: code/game/objects/structures/stairs.dm:1  #define STAIR_TERMINATOR_AUTOMATIC 0 (file-local, rules 11)
    private const float STAIR_TERMINATOR_AUTOMATIC = 0;
    // tg: code/game/objects/structures/stairs.dm:2  #define STAIR_TERMINATOR_NO 1
    private const float STAIR_TERMINATOR_NO = 1;
    // tg: code/game/objects/structures/stairs.dm:3  #define STAIR_TERMINATOR_YES 2
    private const float STAIR_TERMINATOR_YES = 2;
    /// Range within which stair indicators will appear for approaching mobs
    // tg: code/game/objects/structures/stairs.dm:6  #define STAIR_INDICATOR_RANGE 3
    private const float STAIR_INDICATOR_RANGE = 3;
    /// Minimum tile spacing between stair minimap blips
    // tg: code/game/objects/structures/stairs.dm:8  #define STAIR_BLIP_MIN_DISTANCE 2
    private const float STAIR_BLIP_MIN_DISTANCE = 2;

    // tg: code/game/objects/structures/stairs.dm:83  var/static/list/exit_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__exit_connections => ref TgState.Current.obj_structure_stairs__Initialize__exit_connections;
    // tg: code/game/objects/structures/stairs.dm:89  var/static/list/range_connections
    private static ref DmList Initialize__range_connections => ref TgState.Current.obj_structure_stairs__Initialize__range_connections;

    // tg: code/game/objects/structures/stairs.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Dm.append(ref GLOB.stairs, this);
        if (Dm.truthy(force_open_above))
        {
            Waiting.NoSleep(force_open_above__proc());
            build_signal_listener();
        }
        update_surrounding();
        update_minimap_blip();

        Initialize__exit_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_EXIT, nameof(on_exit_stairs)));

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__exit_connections));

        Initialize__range_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_enter_range)),
            Dm.kv(COMSIG_ATOM_EXITED, nameof(on_exit_range)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_connect_range), this, Initialize__range_connections, STAIR_INDICATOR_RANGE));
        return dot;
    }

    // tg: code/game/objects/structures/stairs.dm:96
    public override object Destroy(float? force = null)
    {
        clear_minimap_blips();
        if (directly_above != null)
        {
            UnregisterSignal(directly_above, COMSIG_TURF_MULTIZ_NEW);
            directly_above = null;
        }
        foreach (object climber_ref in Dm.each<object>(mob_to_image))
            clear_climber_image(climber_ref as datum_weakref, instant: TRUE);
        Dm.remove(GLOB.stairs, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/stairs.dm:106
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null) //Look this should never happen but...
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (Dm.truthy(force_open_above))
            build_signal_listener();
        update_surrounding();
        return dot;
    }

    /// Updates the sprite and the sprites of neighboring stairs to reflect merged sprites
    // tg: code/game/objects/structures/stairs.dm:113
    public virtual void update_surrounding()
    {
        if (!Dm.truthy(has_merged_sprites))
            return;

        update_appearance();

        foreach (obj_structure_stairs stair in Dm.each<obj_structure_stairs>(get_step(this, turn(dir, 90))))
            stair.update_appearance();

        foreach (obj_structure_stairs stair in Dm.each<obj_structure_stairs>(get_step(this, turn(dir, -90))))
            stair.update_appearance();
        update_minimap_blip();
    }

    // tg: code/game/objects/structures/stairs.dm:126
    public virtual void update_minimap_blip()
    {
        string bottom_state = Dm.truthy(isTerminator()) ? "stairs_up" : "stairs_down";
        string top_state = (bottom_state == "stairs_up") ? "stairs_down" : "stairs_up";
        turf current_turf = get_turf(this);

        clear_minimap_blips();
        if (current_turf == null)
            return;

        add_minimap_blip_if_valid(current_turf, bottom_state);
        add_minimap_blip_if_valid(get_step_multiz(current_turf, UP) as atom, top_state);
    }

    // tg: code/game/objects/structures/stairs.dm:138
    public virtual void clear_minimap_blips()
    {
        if (!islist(minimap_blip_targets))
            return;
        // remove_minimap_blip(MINIMAP_STAIR_BLIP, target) for each target: the minimap is not ported
        LAZYCLEARLIST(minimap_blip_targets);
    }

    // tg: code/game/objects/structures/stairs.dm:145
    public virtual void add_minimap_blip_if_valid(atom target = null, object state = null)
    {
        if (target == null)
            return;
        if (!Dm.truthy(should_place_minimap_blip(target)))
            return;

        // get_minimap_blip(MINIMAP_STAIR_BLIP, target), add_minimap_blip(target, MINIMAP_STAIR_BLIP, state): the minimap is not
        // ported, so no blip is made and the target list stays empty
    }

    // tg: code/game/objects/structures/stairs.dm:158
    public virtual object should_place_minimap_blip(atom target = null)
    {
        turf target_turf = get_turf(target);
        if (target_turf == null)
            return FALSE;
        // length(get_minimap_blips_in_area(MINIMAP_STAIR_BLIP, target_turf, STAIR_BLIP_MIN_DISTANCE)): the minimap is not ported,
        // and the minimap would never hold a blip
        return TRUE;
    }

    // tg: code/game/objects/structures/stairs.dm:166
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(has_merged_sprites))
            return dot;

        bool has_left_stairs = false;
        bool has_right_stairs = false;
        foreach (obj_structure_stairs stair in Dm.each<obj_structure_stairs>(get_step(this, turn(dir, 90))))
        {
            if (Dm.eq(stair.dir, dir))
            {
                has_left_stairs = true;
                break;
            }
        }

        foreach (obj_structure_stairs stair in Dm.each<obj_structure_stairs>(get_step(this, turn(dir, -90))))
        {
            if (Dm.eq(stair.dir, dir))
            {
                has_right_stairs = true;
                break;
            }
        }

        if (has_left_stairs && has_right_stairs)
            icon_state = Dm.text("[]-m", base_icon_state);
        else if (has_left_stairs)
            icon_state = Dm.text("[]-r", base_icon_state);
        else if (has_right_stairs)
            icon_state = Dm.text("[]-l", base_icon_state);
        else
            icon_state = (string)base_icon_state;
        return dot;
    }

    // tg: code/game/objects/structures/stairs.dm:193
    [SignalHandler]
    public virtual object on_exit_stairs(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (leaving == this)
            return null; //Let's not block ourselves.
        // DEVIATION(DV415): a shot aimed by a camera flies its own 3D line (it reaches the level above through open space, DV394); tg's
        // stairs carry any movable leaving them their way up a level, a bullet too
        if (leaving is obj_projectile { dm_aimed: true })
            return null;

        if (!isobserver(leaving) && Dm.truthy(isTerminator()) && Dm.eq(direction, dir))
        {
            leaving.set_currently_z_moving(CURRENTLY_Z_ASCENDING);
            Waiting.Detach(stair_ascend(leaving));
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    // tg: code/game/objects/structures/stairs.dm:204
    [SignalHandler]
    public virtual void on_enter_range(datum source = null, atom_movable entered = null)
    {
        if (!isliving(entered))
            return;

        var climber = (mob_living)entered;
        object climber_ref = WEAKREF(climber);
        if (climber.client == null || !Dm.truthy(climber.client.prefs.read_preference(typeof(datum_preference_toggle_stair_indicator))))
            return;
        if (Dm.eq(climber.dir, REVERSE_DIR(dir)))
            return; // walking away
        if (Dm.truthy(LAZYACCESS(mob_to_image, climber_ref)))
            return; // already see it
        if (!Dm.@in(climber, viewers(STAIR_INDICATOR_RANGE + 1, this)))
            return; // can't see the staircase (+1 tile for some leeway)
        if (!isopenturf(get_step_multiz(this, UP)))
            return; // no place to go up to

        // DEVIATION(DV369): the green arrow image tg shows the climber (client.images, animate) is not made: in first person the
        // staircase is seen as it is; the image's bookkeeping is kept so the range procs keep tg's flow
        image pointing_image = (image)get_pointing_image();
        LAZYSET(ref mob_to_image, climber_ref, pointing_image);
    }

    // tg: code/game/objects/structures/stairs.dm:231
    [SignalHandler]
    public virtual void on_exit_range(datum source = null, atom_movable exited = null)
    {
        if (!isliving(exited))
            return;

        object climber_ref = WEAKREF(exited);
        if (!Dm.truthy(LAZYACCESS(mob_to_image, climber_ref)))
            return; // not seeing anything
        if (Dm.@in(exited, viewers(STAIR_INDICATOR_RANGE, this)))
            return; // still in range and can see the staircase

        clear_climber_image(climber_ref as datum_weakref);
    }

    // tg: code/game/objects/structures/stairs.dm:244
    public virtual void clear_climber_image(datum_weakref climber_ref = null, float? instant = null)
    {
        instant ??= FALSE;
        var pointing_image = LAZYACCESS(mob_to_image, climber_ref) as image;
        if (pointing_image == null)
        {
            LAZYREMOVE(ref mob_to_image, climber_ref); // just in case
            return;
        }
        if (Dm.truthy(instant))
        {
            clear_climber_image_callback(climber_ref, pointing_image);
            return;
        }

        // animate(pointing_image, alpha = 0, time = 0.75 SECONDS, tag = "point_fadeout"): the image is not shown (DV369)
        // note: the player won't see a new indicator until the image is fully a removed, so this timer also serves as a cooldown
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_climber_image_callback), climber_ref, pointing_image), 1.5f * SECONDS, TIMER_UNIQUE, file: "code/game/objects/structures/stairs.dm", line: 258);
    }

    // tg: code/game/objects/structures/stairs.dm:260
    public virtual void clear_climber_image_callback(datum_weakref climber_ref = null, image pointing_image = null)
    {
        var climber = climber_ref?.resolve() as mob_living;
        if (climber?.client != null)
            Dm.remove(climber.client.images, pointing_image);
        LAZYREMOVE(ref mob_to_image, climber_ref);
    }

    // tg: code/game/objects/structures/stairs.dm:266
    public virtual object get_pointing_image()
    {
        image point_image = image(Dm.resource("icons/hud/screen_gen.dmi"), this, "arrow_large_white_still");
        point_image.color = COLOR_DARK_MODERATE_LIME_GREEN;
        point_image.appearance_flags = Dm.bitor(point_image.appearance_flags, KEEP_APART);
        point_image.transform = matrix().Turn(Dm.num(dir2angle(REVERSE_DIR(dir))));
        point_image.layer = BELOW_MOB_LAYER;
        SET_PLANE(point_image, GAME_PLANE, this);
        return point_image;
    }

    // tg: code/game/objects/structures/stairs.dm:279
    public override async DmTask<object> Cross(atom_movable AM = null)
    {
        if (Dm.truthy(isTerminator()) && Dm.eq(get_dir(this, AM), dir))
            return FALSE;
        return await base.Cross(AM);
    }

    // tg: code/game/objects/structures/stairs.dm:284
    public virtual async DmTask stair_ascend(atom_movable climber = null)
    {
        turf checking = get_step_multiz(this, UP) as turf;
        if (checking == null)
            return;
        // I'm only interested in if the pass is unobstructed, not if the mob will actually make it
        if (!Dm.truthy(climber.can_z_move(UP, get_turf(this), checking, z_move_flags: ZMOVE_ALLOW_BUCKLED)))
            return;
        turf target = get_step_multiz(this, Dm.bitor(dir, UP)) as turf;
        if (target != null && !Dm.truthy(climber.can_z_move(DOWN, target, z_move_flags: ZMOVE_FALL_FLAGS))) //Don't throw them into a tile that will just dump them back down.
        {
            await climber.zMove(target: target, z_move_flags: ZMOVE_STAIRS_FLAGS);
            /// Moves anything that's being dragged by src or anything buckled to it to the stairs turf.
            if (climber.pulling != null)
                await climber.pulling.move_from_pull(climber, loc as turf, climber.glide_size);
            foreach (mob_living buckled in Dm.each<mob_living>(climber.buckled_mobs))
            {
                if (buckled.pulling != null)
                    await buckled.pulling.move_from_pull(buckled, loc as turf, buckled.glide_size);
            }
        }
    }

    // tg: code/game/objects/structures/stairs.dm:300
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.eq(var_name, nameof(force_open_above)))
            return dot;
        if (!Dm.truthy(var_value))
        {
            if (directly_above != null)
            {
                UnregisterSignal(directly_above, COMSIG_TURF_MULTIZ_NEW);
                directly_above = null;
            }
        }
        else
        {
            build_signal_listener();
            await force_open_above__proc();
        }
        return dot;
    }

    // tg: code/game/objects/structures/stairs.dm:315
    public virtual void build_signal_listener()
    {
        if (directly_above != null)
            UnregisterSignal(directly_above, COMSIG_TURF_MULTIZ_NEW);
        var T = get_step_multiz(this, UP) as turf;
        RegisterSignal(T, COMSIG_TURF_MULTIZ_NEW, nameof(on_multiz_new));
        directly_above = T;
    }

    // tg: code/game/objects/structures/stairs.dm:322
    public virtual async DmTask force_open_above__proc()
    {
        var T = get_step_multiz(this, UP) as turf;
        if (T != null && T is not turf_open_openspace)
            await T.ChangeTurf(typeof(turf_open_openspace), flags: CHANGETURF_INHERIT_AIR);
    }

    // tg: code/game/objects/structures/stairs.dm:327
    [SignalHandler]
    public virtual void on_multiz_new(turf source = null, object dir = null)
    {
        if (Dm.eq(dir, UP))
        {
            var T = get_step_multiz(this, UP) as turf;
            if (T != null && T is not turf_open_openspace)
                Waiting.NoSleep(T.ChangeTurf(typeof(turf_open_openspace), flags: CHANGETURF_INHERIT_AIR));
        }
    }

    // tg: code/game/objects/structures/stairs.dm:335
    public override async DmTask<object> intercept_zImpact(DmList falling_movables = null, float? levels = null)
    {
        levels ??= 1;
        object dot = await base.intercept_zImpact(falling_movables, levels);
        // falling from a higher z level onto stairs
        if (levels != 1 || !Dm.truthy(isTerminator()))
            return dot;
        foreach (mob_living guy in Dm.each<mob_living>(falling_movables))
        {
            if (!Dm.truthy(can_fall_down_stairs(guy)))
                continue;
            to_chat(guy, span_warning(I18n.T("You fall down []!", this)));
            await on_fall(guy);
        }
        Dm.combine(ref dot, FALL_INTERCEPTED | FALL_NO_MESSAGE | FALL_RETAIN_PULL);
        return dot;
    }

    /// Will the passed mob tumble down the stairs instead of walking?
    // tg: code/game/objects/structures/stairs.dm:348
    public virtual object can_fall_down_stairs(mob_living falling = null)
    {
        if (falling.buckled != null || falling.pulledby != null)
            return FALSE;
        if (IS_UNCONSCIOUS(falling)) // if you shove someone unconscious down the stairs, they'd probably roll
            return TRUE;
        if (Dm.truthy(falling.has_status_effect(typeof(datum_status_effect_staggered)))) // off balance
            return TRUE;
        return FALSE;
    }

    /// What happens when a mob tumbles down the stairs
    // tg: code/game/objects/structures/stairs.dm:358
    public virtual async DmTask on_fall(mob_living falling = null)
    {
        await falling.AdjustParalyzed(2 * SECONDS);
        await falling.adjust_staggered(2 * SECONDS);
        await falling.AdjustKnockdown(5 * SECONDS);
        falling.spin(1 * SECONDS, 0.25f * SECONDS);
        await falling.apply_damage(rand(4, 8), BRUTE, spread_damage: TRUE);
        GLOB.move_manager.move_towards(falling, get_ranged_target_turf(this, REVERSE_DIR(dir), 2), delay: 0.4f * SECONDS, timeout: 1 * SECONDS);
    }

    // tg: code/game/objects/structures/stairs.dm:366
    public virtual object isTerminator() //If this is the last stair in a chain and should move mobs up
    {
        if (terminator_mode != STAIR_TERMINATOR_AUTOMATIC)
            return Dm.num(terminator_mode == STAIR_TERMINATOR_YES);
        turf T = get_turf(this);
        if (T == null)
            return FALSE;
        var them = get_step(T, dir) as turf;
        if (them == null)
            return FALSE;
        foreach (obj_structure_stairs S in Dm.each<obj_structure_stairs>(them))
        {
            if (Dm.eq(S.dir, dir))
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/objects/structures/stairs.dm:380
public partial class obj_structure_stairs_frame : obj_structure
{
    // tg: code/game/objects/structures/stairs.dm:399
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/game/objects/structures/stairs.dm:403
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("The frame is anchored and can be made into proper stairs with 10 sheets of material.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The frame will need to be secured with a wrench before it can be completed.")));
        return dot;
    }

    // tg: code/game/objects/structures/stairs.dm:410
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_item used_tool = tool;
        user.balloon_alert_to_viewers(I18n.T("securing stairs frame"), I18n.T("securing frame"));
        used_tool.play_tool_sound(this);
        if (!Dm.truthy(await used_tool.use_tool(this, user, 3 * SECONDS)))
            return TRUE;
        if (Dm.truthy(anchored))
        {
            anchored = FALSE;
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            return TRUE;
        }
        anchored = TRUE;
        playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        return TRUE;
    }

    // tg: code/game/objects/structures/stairs.dm:423
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        obj_item used_tool = tool;
        to_chat(user, span_notice(I18n.T("You start disassembling []...", this)));
        used_tool.play_tool_sound(this);
        if (!Dm.truthy(await used_tool.use_tool(this, user, 3 * SECONDS)))
            return TRUE;
        playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        await deconstruct(TRUE);
        return TRUE;
    }

    // tg: code/game/objects/structures/stairs.dm:432
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new(frame_stack, get_turf(this), frame_stack_amount);
    }

    // tg: code/game/objects/structures/stairs.dm:435
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isstack(tool))
            return NONE;
        if (!Dm.truthy(anchored))
        {
            user.balloon_alert(user, I18n.T("secure the frame first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        var material = (obj_item_stack)tool;
        if (material.stairs_type != null)
        {
            if (Dm.num(material.get_amount()) < 10)
            {
                to_chat(user, span_warning(I18n.T("You need ten [] sheets to do this!", material.name)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.locate<obj_structure_stairs>(loc) != null)
            {
                to_chat(user, span_warning(I18n.T("There's already stairs built here!")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You start adding [] to []...", material, this)));
            if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)) || !Dm.truthy(await material.use(10)) || Dm.locate<obj_structure_table>(loc) != null)
                return ITEM_INTERACT_BLOCKING;

            await make_new_stairs(material.stairs_type);
            return ITEM_INTERACT_SUCCESS;
        }

        if (material is obj_item_stack_sheet)
        {
            if (Dm.num(material.get_amount()) < 10)
            {
                to_chat(user, span_warning(I18n.T("You need ten sheets to do this!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.locate<obj_structure_stairs>(loc) != null)
            {
                to_chat(user, span_warning(I18n.T("There's already stairs built here!")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You start adding [] to []...", material, this)));
            if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)) || !Dm.truthy(await material.use(10)) || Dm.locate<obj_structure_table>(loc) != null)
                return ITEM_INTERACT_BLOCKING;

            DmList material_list = Dm.list();
            if (material.material_type != null)
                material_list[material.material_type] = SHEET_MATERIAL_AMOUNT * 10;
            await make_new_stairs(typeof(obj_structure_stairs_material), material_list);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/stairs.dm:475
    public virtual async DmTask make_new_stairs(object stairs_type = null, object custom_materials = null)
    {
        var new_stairs = (obj_structure_stairs)Dm.@new((Type)stairs_type, loc);
        await new_stairs.setDir(dir);
        if (Dm.truthy(custom_materials))
            await new_stairs.set_custom_materials((DmList)custom_materials);
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/stairs.dm:83  var/static/list/exit_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit_stairs))
    internal DmList obj_structure_stairs__Initialize__exit_connections;
    // tg: code/game/objects/structures/stairs.dm:89  var/static/list/range_connections
    internal DmList obj_structure_stairs__Initialize__range_connections;
}
