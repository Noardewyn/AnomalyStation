// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/windowdoor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the windoor's M3 subset (M3 P3, scope 2f; D117): Initialize, the appearance and its unrestricted-side light,
// the timings, the bump with access and the autoclose of open_and_close, the face it blocks (CanAllowThrough by dir,
// on_exit through connect_loc, can_atmos_pass), open and close with the forced-door checks. The procs of later
// milestones are generated live-partial stubs (docs/ai/specs/live-partial.txt, ## P3) or stay Dm.NotTranslated below:
// construction, debris and deconstruction (M5), emag (M4a), M4a P4a: the screwdriver's panel, the crowbar and the
// sillycones' interact, the brig doors of the file (M10+). M5 slice 1: the heat exposure (atmos_sensitive and its damage)
// and play_attack_sound. M5 slice 6: the deconstruction (on_deconstruction with its debris, the crowbar into a windoor assembly)
// and the build direction checks of CanAllowThrough. E13: rcd_vals and rcd_act (the RCD).
// The sounds of the translated procs play (M4b audio slice 1). The var block is generated (D111).

// tg: code/game/machinery/doors/windowdoor.dm:1
public partial class obj_machinery_door_window : obj_machinery_door
{
    // tg: code/game/machinery/doors/windowdoor.dm:42
    [DmParams("mapload", "set_dir", "unres_sides")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_dir = Dm.arg(__extra, 0);
        object unres_sides = Dm.arg(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, set_dir, unres_sides));
        Dm.mask(ref flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1));
        if (Dm.truthy(set_dir))
            Waiting.NoSleep(setDir(set_dir));
        if (Dm.truthy(LAZYLEN(req_access)))
        {
            icon_state = Dm.text("[]", icon_state);
            base_state = icon_state;
        }

        if (Dm.truthy(unres_sides))
        {
            //remove unres_sides from directions it can't be bumped from
            float sw__1 = dir;
            if (sw__1 == NORTH || sw__1 == SOUTH)
            {
                Dm.mask(ref unres_sides, Dm.bitnot(EAST));
                Dm.mask(ref unres_sides, Dm.bitnot(WEST));
            }
            else if (sw__1 == EAST || sw__1 == WEST)
            {
                Dm.mask(ref unres_sides, Dm.bitnot(NORTH));
                Dm.mask(ref unres_sides, Dm.bitnot(SOUTH));
            }
        }

        this.unres_sides = Dm.num(unres_sides);
        update_appearance(UPDATE_ICON);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:64  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_door_window__Initialize__loc_connections;

    // tg: code/game/machinery/doors/windowdoor.dm:71
    public override object Destroy(float? force = null)
    {
        set_density(FALSE);
        electronics = null;
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/doors/windowdoor.dm:77
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            icon_state = Dm.text("[]opening", base_state);
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            icon_state = Dm.text("[]closing", base_state);
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
            icon_state = Dm.text("[]deny", base_state);
        else
            icon_state = Dm.text("[][]", base_state, Dm.truthy(density) ? null : "open");

        if (Dm.truthy(hasPower()) && Dm.truthy(unres_sides))
        {
            await set_light(l_range: 2, l_power: 1);
            return dot;
        }

        await set_light(l_range: 0);
        return dot;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:95
    public override object animation_length(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            return 0.9f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            return 0.9f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:102
    public override object animation_segment_delay(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_PASSABLE))
            return 0.7f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_OPENING_FINISHED))
            return 0.9f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_FINISHED))
            return 0.9f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:113
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);

        if (!Dm.truthy(hasPower()) || !Dm.truthy(unres_sides))
            return dot;

        float sw__1 = dir;
        if (sw__1 == NORTH || sw__1 == SOUTH)
        {
            if (Dm.truthy(Dm.bitand(unres_sides, NORTH)))
            {
                mutable_appearance side_overlay = mutable_appearance(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), "unres_1", FLOAT_LAYER, this, O_LIGHTING_VISUAL_PLANE, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                side_overlay.color = LIGHT_COLOR_DEFAULT;
                side_overlay.pixel_z = dir == NORTH ? 31 : 6;
                Dm.append(ref dot, side_overlay);
            }
            if (Dm.truthy(Dm.bitand(unres_sides, SOUTH)))
            {
                mutable_appearance side_overlay = mutable_appearance(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), "unres_2", FLOAT_LAYER, this, O_LIGHTING_VISUAL_PLANE, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                side_overlay.color = LIGHT_COLOR_DEFAULT;
                side_overlay.pixel_z = dir == NORTH ? -6 : -31;
                Dm.append(ref dot, side_overlay);
            }
        }
        else if (sw__1 == EAST || sw__1 == WEST)
        {
            if (Dm.truthy(Dm.bitand(unres_sides, EAST)))
            {
                mutable_appearance side_overlay = mutable_appearance(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), "unres_4", FLOAT_LAYER, this, O_LIGHTING_VISUAL_PLANE, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                side_overlay.color = LIGHT_COLOR_DEFAULT;
                side_overlay.pixel_w = dir == EAST ? 31 : 6;
                Dm.append(ref dot, side_overlay);
            }
            if (Dm.truthy(Dm.bitand(unres_sides, WEST)))
            {
                mutable_appearance side_overlay = mutable_appearance(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), "unres_8", FLOAT_LAYER, this, O_LIGHTING_VISUAL_PLANE, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                side_overlay.color = LIGHT_COLOR_DEFAULT;
                side_overlay.pixel_w = dir == EAST ? -6 : -31;
                Dm.append(ref dot, side_overlay);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:143
    public virtual async DmTask open_and_close()
    {
        if (!Dm.truthy(await open()))
            return;
        autoclose = TRUE;
        if (Dm.truthy(check_access(null)))
            await sleep(8 * SECONDS);
        else //secure doors close faster
            await sleep(5 * SECONDS);
        if (!Dm.truthy(density) && Dm.truthy(autoclose)) //did someone change state while we slept?
            await close();
    }

    // tg: code/game/machinery/doors/windowdoor.dm:154
    // set waitfor = FALSE (inherited from /atom/Bumped): this body may sleep through bumpopen, so it is the wrapper of
    // waiting-model.md 5.4 (no box: it never sets . before a sleep).
    public override object Bumped(atom_movable AM = null)
    {
        return Waiting.WaitforFalse(Bumped__body(AM, null), null);
    }

    private async DmTask<object> Bumped__body(atom_movable AM, DotBox<object> dot)
    {
        if (Dm.truthy(operating) || !Dm.truthy(density))
            return null;
        if (!ismob(AM))
        {
            if (ismecha(AM))
                Dm.NotTranslated("code/game/machinery/doors/windowdoor.dm:158", "Bumped: a mech's occupants opening the windoor (mechs, M10+)");
            return null;
        }
        if (!Dm.truthy(SSticker))
            return null;
        mob M = AM as mob;
        if (HAS_TRAIT(M, TRAIT_HANDS_BLOCKED) || ((isdrone(M) || iscyborg(M)) && IS_UNCONSCIOUS_OR_CRIT(M)))
            return null;
        await bumpopen(M);
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:177
    public override async DmTask bumpopen(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(operating) || !Dm.truthy(density))
            return;

        // tg: code/game/machinery/doors/windowdoor.dm:181
        add_fingerprint(user);
        if (!Dm.truthy(requiresID()))
            user = null;

        if (Dm.truthy(elevator_mode) && Dm.eq(elevator_status, LIFT_PLATFORM_UNLOCKED))
            await open();

        else if (Dm.truthy(await allowed__proc(user)))
            await open_and_close();

        else
            await run_animation(DOOR_DENY_ANIMATION);

        return;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:196
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(border_dir, dir))
            return FALSE;

        if (mover is obj_structure_window)
        {
            obj_structure_window moved_window = mover as obj_structure_window;
            return valid_build_direction(loc as turf, moved_window.dir, is_fulltile: moved_window.fulltile);
        }

        if (mover is obj_structure_windoor_assembly || mover is obj_machinery_door_window)
            return valid_build_direction(loc as turf, mover.dir, is_fulltile: FALSE);

        return TRUE;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:213
    public override object can_atmos_pass__proc(turf T = null, float? vertical = null)
    {
        vertical ??= FALSE;
        if (get_dir(loc, T) == dir)
            return Dm.not(density);
        else
            return TRUE;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:223
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;

        if (leaving == this)
            return null; // Let's not block ourselves.

        if (Dm.truthy(Dm.bitand(pass_flags_self, leaving.pass_flags)) || (Dm.truthy(Dm.bitand(pass_flags_self, LETPASSTHROW)) && Dm.truthy(leaving.throwing)))
            return null;

        if (Dm.eq(direction, dir) && Dm.truthy(density))
        {
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:238
    public override async DmTask<object> open(float? forced = null, mob_living opener = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (!Dm.truthy(density))
            return TRUE;

        if (Dm.truthy(operating)) //doors can still open when emag-disabled
            return FALSE;

        if (!Dm.truthy(try_to_force_door_open(forced)))
            return FALSE;

        if (!Dm.truthy(operating)) //in case of emag
            operating = TRUE;

        await run_animation(DOOR_OPENING_ANIMATION);
        playsound(this, Dm.resource("sound/machines/windowdoor.ogg"), 100, TRUE);
        object passable_delay = animation_segment_delay(DOOR_OPENING_PASSABLE);
        await sleep(Dm.num(passable_delay));
        set_density(FALSE);
        object open_delay = Dm.sub(animation_segment_delay(DOOR_OPENING_FINISHED), passable_delay);
        await sleep(Dm.num(open_delay));
        air_update_turf(TRUE, FALSE);

        if (Dm.eq(operating, 1)) //emag again
            operating = FALSE;

        return TRUE;
    }

    /// Additional checks depending on what we want to happen to this windoor
    // tg: code/game/machinery/doors/windowdoor.dm:266
    public override object try_to_force_door_open(float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        float sw__1 = Dm.num(force_type);
        if (sw__1 == DEFAULT_DOOR_CHECKS)
        {
            if (!Dm.truthy(hasPower()) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == FORCING_DOOR_CHECKS)
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == BYPASS_DOOR_CHECKS) // Get it open!
            return TRUE;
        else
            stack_trace(Dm.text("Invalid forced argument '[]' passed to open() on this airlock.", force_type), "code/game/machinery/doors/windowdoor.dm", 282);

        // Shit's fucked, let's just check parent real fast.
        return base.try_to_force_door_open(force_type);
    }

    // tg: code/game/machinery/doors/windowdoor.dm:287
    public override async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (Dm.truthy(density))
            return TRUE;

        if (Dm.truthy(operating) || !Dm.truthy(try_to_force_door_shut(forced)))
            return FALSE;

        operating = TRUE;
        await run_animation(DOOR_CLOSING_ANIMATION);
        playsound(this, Dm.resource("sound/machines/windowdoor.ogg"), 100, TRUE);
        object unpassable_delay = animation_segment_delay(DOOR_CLOSING_UNPASSABLE);
        await sleep(Dm.num(unpassable_delay));
        set_density(TRUE);
        air_update_turf(TRUE, TRUE);
        object close_delay = Dm.sub(animation_segment_delay(DOOR_CLOSING_FINISHED), unpassable_delay);
        await sleep(Dm.num(close_delay));

        operating = FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:307
    public override object try_to_force_door_shut(float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        float sw__1 = Dm.num(force_type);
        if (sw__1 == DEFAULT_DOOR_CHECKS)
        {
            if (!Dm.truthy(hasPower()) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == FORCING_DOOR_CHECKS)
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == BYPASS_DOOR_CHECKS) // Get it shut!
            return TRUE;
        else
            stack_trace(Dm.text("Invalid forced argument '[]' passed to close() on this airlock.", force_type), "code/game/machinery/doors/windowdoor.dm", 323);

        // If we got here, shit's fucked, but let's presume parent can bail us out somehow.
        return base.try_to_force_door_shut(force_type);
    }

    // tg: code/game/machinery/doors/windowdoor.dm:328
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
            playsound(this, Dm.resource("sound/effects/glass/glasshit.ogg"), 90, TRUE);
        else if (Dm.eq(sw__1, BURN))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:335
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(disassembled))
            return null;

        playsound(this, SFX_SHATTER, 70, TRUE);

        for (float i = 1; i <= shards; i++)
            await drop_debris(Dm.@new<obj_item_shard>(this));
        if (Dm.truthy(rods))
            await drop_debris(Dm.@new<obj_item_stack_rods>(this, rods));
        if (Dm.truthy(cable))
            await drop_debris(Dm.@new<obj_item_stack_cable_coil>(this, cable));
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:348
    public virtual async DmTask drop_debris(obj_item debris = null)
    {
        await debris.forceMove(loc);
        // tg: code/game/machinery/doors/windowdoor.dm:350
        transfer_fingerprints_to(debris);
    }

    // tg: code/game/machinery/doors/windowdoor.dm:366
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, T0C + (Dm.truthy(reinf) ? 1600 : 800)));
    }

    // tg: code/game/machinery/doors/windowdoor.dm:369
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(round(Dm.num(air.return_volume()) / 100), BURN, 0, 0);
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:372
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (!Dm.truthy(operating) && Dm.truthy(density) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            obj_flags = Dm.bitor(obj_flags, EMAGGED);
            operating = TRUE;
            flick(Dm.text("[]spark", base_state), this);
            playsound(this, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_emag_act)), 0.6f * SECONDS, file: "code/game/machinery/doors/windowdoor.dm", line: 378);
            return TRUE;
        }
        return FALSE;
    }

    /// Timer proc, called ~0.6 seconds after [emag_act]. Finishes the emag sequence by breaking the windoor.
    // tg: code/game/machinery/doors/windowdoor.dm:383
    public virtual async DmTask finish_emag_act()
    {
        operating = FALSE;
        await open(BYPASS_DOOR_CHECKS);
    }

    // tg: code/game/machinery/doors/windowdoor.dm:387
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_warning(I18n.T("Its access panel is smoking slightly.")));
        if (!Dm.truthy(density))
        {
            if (Dm.truthy(panel_open))
                Dm.append(ref dot, span_notice(I18n.T("The [] could be [] out.", span_boldnotice(I18n.T("airlock electronics")), span_boldnotice(I18n.T("levered")))));
        }
        return dot;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:396
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.screwdriver_act(user, tool);
        if (Dm.truthy(density) || Dm.truthy(operating))
        {
            to_chat(user, span_warning(I18n.T("You need to open the door to access the maintenance panel!")));
            return ITEM_INTERACT_BLOCKING;
        }
        // tg: code/game/machinery/doors/windowdoor.dm:401
        add_fingerprint(user);
        tool.play_tool_sound(this);
        toggle_panel_open();
        to_chat(user, span_notice(I18n.T("You [] the maintenance panel.", Dm.truthy(panel_open) ? "open" : "close")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:407
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.crowbar_act(user, tool);
        if (!Dm.truthy(panel_open) || Dm.truthy(density) || Dm.truthy(operating))
            return ITEM_INTERACT_BLOCKING;
        // tg: code/game/machinery/doors/windowdoor.dm:411
        add_fingerprint(user);
        user.visible_message(span_notice(I18n.T("[] removes the electronics from \\the [].", user, this)),
        span_notice(I18n.T("You start to remove electronics from \\the []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(panel_open) || Dm.truthy(density) || Dm.truthy(operating) || loc == null)
            return ITEM_INTERACT_BLOCKING;
        obj_structure_windoor_assembly windoor_assembly = Dm.@new<obj_structure_windoor_assembly>(loc);
        switch (base_state)
        {
            case "left":
                windoor_assembly.facing = "l";
                break;
            case "right":
                windoor_assembly.facing = "r";
                break;
            case "leftsecure":
                windoor_assembly.facing = "l";
                windoor_assembly.secure = TRUE;
                break;
            case "rightsecure":
                windoor_assembly.facing = "r";
                windoor_assembly.secure = TRUE;
                break;
        }
        await windoor_assembly.set_anchored(TRUE);
        windoor_assembly.cables_added = TRUE;
        await windoor_assembly.setDir(dir);
        windoor_assembly.update_appearance();
        windoor_assembly.created_name = name;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("You discard the damaged electronics.")));
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        to_chat(user, span_notice(I18n.T("You remove the airlock electronics.")));
        obj_item_electronics_airlock dropped_electronics;
        if (electronics == null)
        {
            dropped_electronics = Dm.@new<obj_item_electronics_airlock>(drop_location());
            if (Dm.truthy(req_one_access))
            {
                dropped_electronics.one_access = 1;
                dropped_electronics.accesses = req_one_access as DmList;
            }
            else
                dropped_electronics.accesses = req_access as DmList;
        }
        else
        {
            dropped_electronics = electronics;
            electronics = null;
            await dropped_electronics.forceMove(drop_location() as atom);
        }
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:455
    public override async DmTask<object> interact(mob user = null, params object[] __extra) //for sillycones
    {
        await try_to_activate_door(user);
        return null;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:458
    public override async DmTask<object> try_to_activate_door(mob user = null, float? access_bypass = null, float? bumped = null)
    {
        access_bypass ??= FALSE;
        bumped ??= FALSE;
        object dot = null;
        dot = await base.try_to_activate_door(user, access_bypass, bumped);
        if (Dm.truthy(dot))
            autoclose = FALSE;
        return dot;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:463
    public override object unrestricted_side(object opener = null)
    {
        if (get_turf(opener) == loc)
            return Dm.bitand(REVERSE_DIR(dir), unres_sides);
        return base.unrestricted_side(opener);
    }

    // tg: code/game/machinery/doors/windowdoor.dm:468
    public override async DmTask try_to_crowbar(obj_item I = null, mob user = null, float? forced = null)
    {
        forced ??= FALSE;
        if (istype(I, typeof(obj_item_crowbar_power)))
        {
            // power_tool.limit_jaws_access: the jaws of life (after the core version)
            Dm.NotTranslated("code/game/machinery/doors/windowdoor.dm:470", "try_to_crowbar: the jaws of life's access limit (after the core version)");
            return;
        }

        if (!Dm.truthy(hasPower()) || Dm.truthy(forced))
        {
            if (Dm.truthy(density))
                await open(BYPASS_DOOR_CHECKS);
            else
                await close(BYPASS_DOOR_CHECKS);
        }
        else
            to_chat(user, span_warning(I18n.T("The door's motors resist your efforts to force it!")));
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/doors/windowdoor.dm:64  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_machinery_door_window__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_machinery_door_window.on_exit)));
}

// tg: code/game/machinery/doors/windowdoor.dm:484 (E13: the RCD)
public partial class obj_machinery_door_window : obj_machinery_door
{
    // tg: code/game/machinery/doors/windowdoor.dm:484
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        object sw__1 = the_rcd.mode;
        if (Dm.eq(sw__1, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 32));
        return FALSE;
    }

    // tg: code/game/machinery/doors/windowdoor.dm:490
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_machinery_door_window : obj_machinery_door
{
    // tg: code/game/machinery/doors/windowdoor.dm:220
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        return Dm.num(!Dm.truthy(density) || !Dm.eq(dir, to_dir) || (Dm.truthy(check_access_list(pass_info.access)) && Dm.truthy(hasPower()) && !Dm.truthy(pass_info.no_id)));
    }
}
