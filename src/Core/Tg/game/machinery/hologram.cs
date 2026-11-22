// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/hologram.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the holopad, the AI's and the crew's holograms, holocalls' ends, the recorded messages and the
// holoray. The emissive glow of the pad's lights and of the holoray is DV12's. The var blocks are generated (D111).

/* Holograms!
 * Contains:
 * Holopad
 * Hologram
 * Other stuff
 */

/*
Revised. Original based on space ninja hologram code. Which is also mine. /N
How it works:
AI clicks on holopad in camera view. View centers on holopad.
AI clicks again on the holopad to display a hologram. Hologram stays as long as AI is looking at the pad and it (the hologram) is in range of the pad.
AI can use the directional keys to move the hologram around, provided the above conditions are met and the AI in question is the holopad's master.
Any number of AIs can use a holopad. /Lo6
AI may cancel the hologram at any time by clicking on the holopad once more.

Possible to do for anyone motivated enough:
	Give an AI variable for different hologram icons.
	Itegrate EMP effect to disable the unit.
*/

/*
 * Holopad
 */

// tg: code/game/machinery/hologram.dm:36
public partial class obj_machinery_holopad : obj_machinery
{
    private const int CAN_HEAR_MASTERS = 1 << 0; // tg: hologram.dm:1
    private const int CAN_HEAR_ACTIVE_HOLOCALLS = 1 << 1; // tg: hologram.dm:2
    private const int CAN_HEAR_RECORD_MODE = 1 << 2; // tg: hologram.dm:3
    private const int CAN_HEAR_HOLOCALL_USER = 1 << 3; // tg: hologram.dm:4
    private const int CAN_HEAR_ALL_FLAGS = CAN_HEAR_MASTERS | CAN_HEAR_ACTIVE_HOLOCALLS | CAN_HEAR_RECORD_MODE | CAN_HEAR_HOLOCALL_USER; // tg: hologram.dm:5
    private const float HOLOPAD_PASSIVE_POWER_USAGE = 1; // tg: hologram.dm:33
    private const float HOLOGRAM_POWER_USAGE = 2; // tg: hologram.dm:34

    /// Calls will be automatically answered after a couple rings, here for debugging (var/static/force_answer_call)
    public static bool force_answer_call
    {
        get => TgState.Current.obj_machinery_holopad__force_answer_call;
        set => TgState.Current.obj_machinery_holopad__force_answer_call = value;
    }

    /// var/static/list/holopads
    public static DmList holopads => TgState.Current.obj_machinery_holopad__holopads ??= Dm.list();

    // tg: code/game/machinery/hologram.dm:106
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        DmList hovering_mob_typechecks = TgState.Current.obj_machinery_holopad__hovering_mob_typechecks ??= Dm.list(
            Dm.kv(typeof(mob_living_silicon), Dm.list(
                Dm.kv(SCREENTIP_CONTEXT_ALT_LMB, "Disconnect all active calls")
            ))
        ); // var/static/list/hovering_mob_typechecks
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_mob_typechecks), hovering_mob_typechecks));
        set_wires(Dm.@new<datum_wires_holopad>(this));

        if (Dm.truthy(on_network))
        {
            DmList pads = holopads; // a list: changed in place
            Dm.append(ref pads, this);
        }
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:150
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (loc == null)
            return dot;
        // move any relevant holograms, basically non-AI, and rays with the pad
        foreach (mob_living user in Dm.each<mob_living>(holorays))
        {
            obj_effect_overlay_holoray user_ray = holorays[user] as obj_effect_overlay_holoray;
            await user_ray.abstract_move(loc);
        }
        DmList non_call_masters = masters?.Copy();
        foreach (datum_holocall holocall in Dm.each<datum_holocall>(holo_calls))
        {
            if (holocall.user == null || !Dm.truthy(LAZYACCESS(masters, holocall.user)))
                continue;
            Dm.remove(ref non_call_masters, holocall.user);
            // moving the eye moves the holo which updates the ray too
            holocall.eye.setLoc(locate(clamp(x + (holocall.hologram.x - old_loc.x), 1, Dm.world.maxx), clamp(y + (holocall.hologram.y - old_loc.y), 1, Dm.world.maxy), z));
        }
        foreach (datum holo_master in Dm.each<datum>(non_call_masters))
        {
            obj_effect holo = masters[holo_master] as obj_effect;
            update_holoray(holo_master, holo.loc as turf);
        }
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:184
    public override object Destroy(float? force = null)
    {
        if (outgoing_call != null)
            outgoing_call.ConnectionFailure(this);

        foreach (datum_holocall holocall_to_disconnect in Dm.each<datum_holocall>(holo_calls?.Copy()))
            holocall_to_disconnect.ConnectionFailure(this);

        if (Dm.truthy(replay_mode))
            replay_stop();
        if (Dm.truthy(record_mode))
            record_stop();

        foreach (object I in Dm.each(masters?.Copy()))
            clear_holo(I as datum);

        QDEL_NULL(ref disk);

        DmList pads = holopads; // a list: changed in place
        Dm.remove(ref pads, this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/hologram.dm:203
    public override object power_change()
    {
        object dot = base.power_change();
        if (!Dm.truthy(powered__proc()))
        {
            if (Dm.truthy(replay_mode))
                replay_stop();
            if (Dm.truthy(record_mode))
                record_stop();
            if (outgoing_call != null)
                outgoing_call.ConnectionFailure(this);
        }
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:213
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (outgoing_call != null)
            outgoing_call.ConnectionFailure(this);
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:218
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (disk != null)
            await disk.forceMove(drop_location() as atom);
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/game/machinery/hologram.dm:222
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float holograph_range = 4;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            holograph_range += 1 * Dm.num(capacitor.tier);
        holo_range = holograph_range;
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:229
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (isAI(user) || Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Current projection range: <b>[]</b> units.", holo_range)));

        if (!isAI(user))
            return dot;

        Dm.append(ref dot, span_info(I18n.T("Use :[] to speak through the projection.", MODE_KEY_HOLOPAD)));
        Dm.append(ref dot, span_info(I18n.T("Right-click to project or cancel a projection.")));
        Dm.append(ref dot, span_info(I18n.T("Alt-click to hangup all active and incomming calls.")));
        Dm.append(ref dot, span_info(I18n.T("Ctrl-click to end projection without jumping to your last location.")));
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:241
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/hologram.dm:246
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (dot == null || Dm.truthy(anchorvalue))
            return dot;

        if (outgoing_call != null)
            outgoing_call.ConnectionFailure(this); //disconnect the call if we got unwrenched.

        foreach (datum_holocall holocall_to_disconnect in Dm.each<datum_holocall>(holo_calls?.Copy()))
            holocall_to_disconnect.ConnectionFailure(this);

        if (Dm.truthy(replay_mode))
            replay_stop();
        if (Dm.truthy(record_mode))
            record_stop();
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:262
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/hologram.dm:265
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: TRUE, closed_density: FALSE, deconstruct_on_fail: TRUE);
    }

    // tg: code/game/machinery/hologram.dm:268
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_disk_holodisk new_disk)
        {
            if (disk != null)
            {
                to_chat(user, span_warning(I18n.T("There's already a disk inside []!", this)));
                return null;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return null;
            to_chat(user, span_notice(I18n.T("You insert [] into [].", tool, this)));
            disk = new_disk;
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/machinery/hologram.dm:280
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == disk)
            disk = null;
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:285
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!Dm.truthy(is_operational))
            return UI_CLOSE;
        if (outgoing_call != null && !Dm.truthy(calling))
            return UI_CLOSE;
        return await base.ui_status(user, state);
    }

    // tg: code/game/machinery/hologram.dm:292
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Holopad", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/hologram.dm:298
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["calling"] = calling;
        data["on_network"] = on_network;
        data["on_cooldown"] = last_request + 200 < world.time ? FALSE : TRUE;
        data["allowed"] = await allowed__proc(user);
        data["disk"] = disk != null ? TRUE : FALSE;
        data["disk_record"] = disk?.record != null ? TRUE : FALSE;
        data["replay_mode"] = replay_mode;
        data["loop_mode"] = loop_mode;
        data["record_mode"] = record_mode;
        data["holo_calls"] = Dm.list();
        foreach (datum_holocall HC in Dm.each<datum_holocall>(holo_calls))
        {
            DmList call_data = Dm.list(
                Dm.kv("caller", HC.user),
                Dm.kv("connected", HC.connected_holopad == this ? TRUE : FALSE),
                Dm.kv("ref", REF(HC))
            );
            DmList calls = data["holo_calls"] as DmList;
            Dm.append(ref calls, Dm.list(call_data));
        }
        return data;
    }

    // tg: code/game/machinery/hologram.dm:321
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = usr as mob;
        switch (Dm.str(action))
        {
            case "AIrequest":
                if (isAI(user))
                {
                    mob_living_silicon_ai ai_user = user as mob_living_silicon_ai;
                    ai_user.eyeobj.setLoc(get_turf(this));
                    to_chat(user, span_info(I18n.T("AIs can not request AI presence. Jumping instead.")));
                    return null;
                }
                if (last_request + 200 < world.time)
                {
                    last_request = world.time;
                    to_chat(user, span_info(I18n.T("You requested an AI's presence.")));
                    area area = get_area(this) as area;
                    foreach (mob_living_silicon_ai AI in Dm.each<mob_living_silicon_ai>(GLOB.silicon_mobs))
                    {
                        if (AI.client == null)
                            continue;
                        to_chat(AI, span_info(I18n.T("Your presence is requested at <a href='byond://?src=[];jump_to_holopad=[]'>\\the []</a>. <a href='byond://?src=[];project_to_holopad=[]'>Project Hologram?</a>", REF(AI), REF(this), area, REF(AI), REF(this))));
                    }
                    return TRUE;
                }
                else
                {
                    to_chat(user, span_info(I18n.T("A request for AI presence was already sent recently.")));
                    return null;
                }
            case "holocall":
                if (outgoing_call != null)
                    return null;
                if (user.loc == loc)
                {
                    DmList callnames = Dm.list();
                    foreach (object I in Dm.each(holopads))
                    {
                        area A = get_area(I as atom) as area;
                        if (A != null)
                        {
                            DmList pads_here = callnames[A] as DmList;
                            LAZYADD(ref pads_here, I);
                            callnames[A] = pads_here;
                        }
                    }
                    Dm.remove(ref callnames, get_area(this));
                    object result = await tgui_input_list(user, "Choose an area to call", "Holocall", sort_names(callnames) as DmList);
                    if (result == null)
                        return null;
                    if (QDELETED(user) || outgoing_call != null)
                        return null;
                    if (user.loc == loc)
                    {
                        float input = Dm.num(text2num(@params["headcall"]));
                        float headcall = input == 1 ? TRUE : FALSE;
                        datum_holocall holo_call = Dm.@new<datum_holocall>(user, this, callnames[result], headcall);
                        if (QDELETED(holo_call)) //can delete itself if the target pad was destroyed
                            return FALSE;
                        calling = TRUE;
                        return TRUE;
                    }
                }
                else
                    to_chat(user, span_warning(I18n.T("You must stand on the holopad to make a call!")));
                break;
            case "connectcall":
            {
                datum_holocall call_to_connect = locate_ref_in(@params["holopad"], holo_calls) as datum_holocall;
                if (!QDELETED(call_to_connect))
                {
                    await call_to_connect.Answer(this);
                    return TRUE;
                }
                break;
            }
            case "disconnectcall":
            {
                datum_holocall call_to_disconnect = locate_ref_in(@params["holopad"], holo_calls) as datum_holocall;
                if (!QDELETED(call_to_disconnect))
                {
                    call_to_disconnect.Disconnect(this);
                    return TRUE;
                }
                break;
            }
            case "rejectall":
                foreach (datum_holocall call_to_reject in Dm.each<datum_holocall>(holo_calls?.Copy()))
                {
                    if (call_to_reject.connected_holopad == this) // do not kill the current connection
                        continue;
                    call_to_reject.Disconnect(this);
                }
                return TRUE;
            case "disk_eject":
                if (disk != null && !Dm.truthy(replay_mode))
                {
                    await disk.forceMove(drop_location() as atom);
                    return TRUE;
                }
                break;
            case "replay_mode":
                if (Dm.truthy(replay_mode))
                {
                    replay_stop();
                    return TRUE;
                }
                else
                {
                    await replay_start();
                    return TRUE;
                }
            case "loop_mode":
                loop_mode = Dm.truthy(loop_mode) ? FALSE : TRUE;
                return TRUE;
            case "record_mode":
                if (Dm.truthy(record_mode))
                {
                    record_stop();
                    return TRUE;
                }
                else
                {
                    record_start__proc(user as mob_living);
                    return TRUE;
                }
            case "record_clear":
                record_clear();
                return TRUE;
            case "offset":
            {
                offset++;
                if (offset > 4)
                    offset = FALSE;
                turf new_turf;
                if (!Dm.truthy(offset))
                    new_turf = get_turf(this);
                else
                    new_turf = get_step(this, Dm.num(GLOB.cardinals[offset])) as turf;
                await move_hologram(disk.record, new_turf);
                return TRUE;
            }
            case "hang_up":
                if (outgoing_call != null)
                {
                    outgoing_call.Disconnect(this);
                    return TRUE;
                }
                break;
        }
        return null;
    }

    //setters
    /**
     * setter for can_hear_flags. handles adding or removing the given flag on can_hear_flags and then adding hearing sensitivity or removing it depending on the final state
     * this is necessary because holopads are a significant fraction of the hearable atoms on station which increases the cost of procs that iterate through hearables
     * so we need holopads to not be hearable until it is needed
     *
     * * flag - one of the can_hear_flags flag defines
     * * set_flag - boolean, if TRUE sets can_hear_flags to that flag and might add hearing sensitivity if can_hear_flags was NONE before,
     * if FALSE unsets the flag and possibly removes hearing sensitivity
     */
    // tg: code/game/machinery/hologram.dm:429
    public virtual bool set_can_hear_flags(int flag, bool set_flag = true)
    {
        if ((flag & CAN_HEAR_ALL_FLAGS) == 0)
            return false; //the given flag doesnt exist

        if (set_flag)
        {
            if (can_hear_flags == NONE) //we couldnt hear before, so become hearing sensitive
                become_hearing_sensitive();

            can_hear_flags = (int)can_hear_flags | flag;
            return true;
        }
        else
        {
            can_hear_flags = (int)can_hear_flags & ~flag;
            if (can_hear_flags == NONE)
                lose_hearing_sensitivity();

            return true;
        }
    }

    ///setter for adding/removing holocalls to this holopad. used to update the holo_calls list and can_hear_flags
    ///adds the given holocall if add_holocall is TRUE, removes if FALSE
    // tg: code/game/machinery/hologram.dm:450
    public virtual bool set_holocall(datum_holocall holocall_to_update = null, bool add_holocall = true)
    {
        if (holocall_to_update == null)
            return false;

        if (add_holocall)
        {
            set_can_hear_flags(CAN_HEAR_ACTIVE_HOLOCALLS);
            LAZYADD(ref holo_calls, holocall_to_update);
        }
        else
        {
            LAZYREMOVE(ref holo_calls, holocall_to_update);
            if (!Dm.truthy(LAZYLEN(holo_calls)))
                set_can_hear_flags(CAN_HEAR_ACTIVE_HOLOCALLS, false);
        }

        update_appearance();
        return true;
    }

    /**
     * hangup_all_calls: Disconnects all current holocalls from the holopad
     */
    // tg: code/game/machinery/hologram.dm:470
    public virtual async DmTask hangup_all_calls()
    {
        foreach (datum_holocall holocall_to_disconnect in Dm.each<datum_holocall>(holo_calls?.Copy()))
            holocall_to_disconnect.Disconnect(this);
        await DmTask.CompletedTask;
    }

    // tg: code/game/machinery/hologram.dm:480
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        if (user is not mob_living_silicon_ai AI)
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        if (!Dm.truthy(on_network))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        /*There are pretty much only three ways to interact here.
        I don't need to check for client since they're clicking on an object.
        This may change in the future but for now will suffice.*/
        if (!Dm.truthy(LAZYLEN(masters)) || !Dm.truthy(masters[AI])) //If there is no hologram, possibly make one.
            await activate_holo(AI);
        else //If there is a hologram, remove it, and jump to your last location.
        {
            clear_holo(AI);
            if (AI.lastloc != null) //only jump to your last location if your lastloc is set, which only sets if you projected from a request message.
            {
                AI.eyeobj.setLoc(AI.lastloc);
                AI.lastloc = null;
            }
        }
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/hologram.dm:497
    public override async DmTask AICtrlClick(mob_living_silicon user = null)
    {
        await DmTask.CompletedTask;
        if (user is not mob_living_silicon_ai AI)
            return;
        if (!Dm.truthy(on_network))
            return;
        if (!Dm.truthy(LAZYLEN(masters)) || !Dm.truthy(masters[AI])) //If there is no hologram, then this button does nothing.
            return;
        else //If there is a hologram, remove it, but dont jump to your last location.
        {
            AI.lastloc = null;
            clear_holo(AI);
        }
    }

    //this really should not be processing by default with how common holopads are
    //everything in here can start processing if need be once first set and stop processing after being unset
    // tg: code/game/machinery/hologram.dm:511
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(LAZYLEN(masters)))
        {
            foreach (datum master in Dm.each<datum>(masters.Copy()))
            {
                if (!Dm.truthy(is_operational) || !validate_user(master))
                    clear_holo(master);
            }
        }

        if (outgoing_call != null)
            outgoing_call.Check();

        bool are_ringing = false;

        foreach (datum_holocall holocall in Dm.each<datum_holocall>(holo_calls?.Copy()))
        {
            if (holocall.connected_holopad == this)
                continue;

            if (force_answer_call && world.time > (Dm.num(holocall.call_start_time) + (HOLOPAD_MAX_DIAL_TIME / 2)))
            {
                Waiting.NoSleep(holocall.Answer(this));
                break;
            }
            if (Dm.truthy(holocall.head_call) && !Dm.truthy(secure))
            {
                Waiting.NoSleep(holocall.Answer(this));
                break;
            }
            if (outgoing_call != null)
                holocall.Disconnect(this); //can't answer calls while calling
            else
            {
                playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 100); //bring, bring!
                are_ringing = true;
            }
        }

        if (Dm.truthy(ringing) != are_ringing)
        {
            ringing = are_ringing ? TRUE : FALSE;
            update_appearance();
        }
        return null;
    }

    // tg: code/game/machinery/hologram.dm:542
    public virtual async DmTask<obj_effect_overlay_holo_pad_hologram> activate_holo(mob_living user = null)
    {
        mob_living_silicon_ai AI = user as mob_living_silicon_ai;

        if (Dm.truthy(is_operational)) //If the projector has power
        {
            if (AI != null && AI.current is obj_machinery_holopad)
            {
                to_chat(user, Dm.text("[] \\black Image feed in progress.", span_danger(I18n.T("ERROR:"))));
                return null;
            }

            // What to pull our appearance out of
            obj_effect_overlay_holo_pad_hologram hologram = Dm.@new<obj_effect_overlay_holo_pad_hologram>(loc); //Spawn a blank effect at the location.
            object work_off = (object)AI?.hologram_appearance ?? user;

            hologram.icon = (work_off as IDmAppearance)?.icon ?? (work_off as atom)?.icon;
            hologram.icon_state = Dm.str(work_off is atom work_atom ? work_atom.icon_state : (work_off as mutable_appearance)?.icon_state);
            if (work_off is atom work_off_atom)
                hologram.copy_overlays(work_off_atom, TRUE);
            else
                hologram.copy_overlays(work_off as IDmAppearance, TRUE);
            hologram.makeHologram(color_override: AI?.ai_holocolor);

            if (AI != null)
                AI.eyeobj.setLoc(get_turf(this)); //ensure the AI camera moves to the holopad
            else //make it like real life
                hologram.Impersonation = user;
            hologram.mouse_opacity = MOUSE_OPACITY_TRANSPARENT; //So you can't click on it.
            hologram.layer = FLY_LAYER; //Above all the other objects/mobs. Or the vast majority of them.
            SET_PLANE_EXPLICIT(hologram, ABOVE_GAME_PLANE, this);
            await hologram.set_anchored(TRUE); //So space wind cannot drag it.
            hologram.name = Dm.text("[] (Hologram)", user.name); //If someone decides to right click.
            set_holo(user, hologram);

            set_holo(user, hologram);
            visible_message(span_notice(I18n.T("A holographic image of [] flickers to life before your eyes!", user)));

            return hologram;
        }
        else
            to_chat(user, Dm.text("[] ", span_danger(I18n.T("ERROR:"))) + I18n.T("Unable to project hologram."));
        return null;
    }

    /*This is the proc for special two-way communication between AI and holopad/people talking near holopad.
    For the other part of the code, check silicon say.dm. Particularly robot talk.*/
    // tg: code/game/machinery/hologram.dm:578
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (speaker != null && Dm.truthy(LAZYLEN(masters)) && !Dm.truthy(radio_freq)) //Master is mostly a safety in case lag hits or something. Radio_freq so AIs dont hear holopad stuff through radios.
        {
            foreach (mob_living_silicon_ai master in Dm.each<mob_living_silicon_ai>(masters.Copy()))
            {
                if (Dm.truthy(masters[master]) && speaker != master)
                    await master.relay_speech(speaker, message_language as datum_language, raw_message, radio_freq, spans, message_mods);
            }
        }

        foreach (datum_holocall holocall_to_update in Dm.each<datum_holocall>(holo_calls?.Copy()))
        {
            if (holocall_to_update.connected_holopad == this) //if we answered this call originating from another holopad
            {
                if (speaker == holocall_to_update.hologram && Dm.truthy(holocall_to_update.user.client?.prefs.read_preference(typeof(datum_preference_toggle_enable_runechat))))
                    holocall_to_update.user.create_chat_message(speaker, message_language, raw_message, spans);
                else
                    await holocall_to_update.user.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range: INFINITY);
            }
        }

        if (outgoing_call?.hologram != null && speaker == outgoing_call.user)
            await outgoing_call.hologram.say(raw_message, spans: spans, sanitize: FALSE, language: message_language, message_mods: message_mods);

        if (Dm.truthy(record_mode) && speaker == record_user)
            record_message(speaker as mob_living, raw_message, message_language);
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:598
    public virtual void SetLightsAndPower()
    {
        float total_users = LAZYLEN(masters) + LAZYLEN(holo_calls);
        update_use_power(total_users > 0 ? ACTIVE_POWER_USE : IDLE_POWER_USE);
        update_mode_power_usage(ACTIVE_POWER_USE, Dm.num(Dm.initial(GetType(), (obj_machinery m) => m.active_power_usage)) + HOLOPAD_PASSIVE_POWER_USAGE + (HOLOGRAM_POWER_USAGE * total_users));
        if (total_users > 0 || Dm.truthy(replay_mode))
            Waiting.NoSleep(set_light(2));
        else
            Waiting.NoSleep(set_light(0));
        update_appearance();
    }

    // tg: code/game/machinery/hologram.dm:608
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        float total_users = LAZYLEN(masters) + LAZYLEN(holo_calls);
        if (Dm.truthy(ringing))
        {
            icon_state = Dm.text("[]_ringing", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, (total_users > 0 || Dm.truthy(replay_mode)) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/hologram.dm:622
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;

        object default_color = COLOR_AI_HOLOGRAM_BLUE;
        if (masters != null || Dm.truthy(replay_mode))
        {
            mutable_appearance hololine_overlay = mutable_appearance(icon, "holopad1_mask") as mutable_appearance;
            foreach (object master in Dm.each(masters))
            {
                if (master is mob_living_silicon_ai AI && Dm.truthy(AI.ai_holocolor))
                {
                    default_color = AI.ai_holocolor;
                    break;
                }
            }
            hololine_overlay.color = default_color;
            Dm.append(ref dot, hololine_overlay);
            // DEVIATION(DV12): . += emissive_appearance(icon, "holopad1_mask", src, alpha = src.alpha)
        }
        if (Dm.truthy(ringing))
        {
            Dm.append(ref dot, mutable_appearance(icon, "holopad_ringing_mask"));
            // DEVIATION(DV12): . += emissive_appearance(icon, "holopad_ringing_mask", src, alpha = src.alpha)
        }
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:640
    public virtual bool set_holo(datum owner = null, obj_effect_overlay_holo_pad_hologram h = null)
    {
        LAZYSET(ref masters, owner, h);
        LAZYSET(ref holorays, owner, Dm.@new<obj_effect_overlay_holoray>(loc));
        set_can_hear_flags(CAN_HEAR_MASTERS);
        if (owner is mob_living_silicon_ai AI)
        {
            AI.current = this;
            if (Dm.truthy(AI.ai_holocolor))
            {
                obj_effect_overlay_holoray owner_ray = holorays[owner] as obj_effect_overlay_holoray;
                owner_ray.color = AI.ai_holocolor;
            }
        }
        SetLightsAndPower();
        update_holoray(owner, get_turf(loc));
        return true;
    }

    // tg: code/game/machinery/hologram.dm:654
    public virtual bool clear_holo(datum owner = null)
    {
        qdel(masters?[owner]);
        unset_holo(owner);
        return true;
    }

    /**
     * Called by holocall to inform outgoing_call that the receiver picked up.
     */
    // tg: code/game/machinery/hologram.dm:661
    public virtual void callee_picked_up()
    {
        calling = FALSE;
        set_can_hear_flags(CAN_HEAR_HOLOCALL_USER);
    }

    /**
     * Called by holocall to inform outgoing_call that the call is terminated.
     */
    // tg: code/game/machinery/hologram.dm:668
    public virtual void callee_hung_up()
    {
        set_can_hear_flags(CAN_HEAR_HOLOCALL_USER, set_flag: false);
        calling = FALSE;
        outgoing_call = null;
    }

    // tg: code/game/machinery/hologram.dm:673
    public virtual bool unset_holo(datum user = null)
    {
        if (user is mob_living_silicon_ai AI && AI.current == this)
            AI.current = null;
        LAZYREMOVE(ref masters, user); // Discard AI from the list of those who use holopad
        if (!Dm.truthy(LAZYLEN(masters)))
            set_can_hear_flags(CAN_HEAR_MASTERS, set_flag: false);
        qdel(holorays?[user]);
        LAZYREMOVE(ref holorays, user);
        SetLightsAndPower();
        return true;
    }

    //Try to transfer hologram to another pad that can project on T
    // tg: code/game/machinery/hologram.dm:686
    public virtual bool transfer_to_nearby_pad(turf T = null, datum holo_owner = null)
    {
        obj_effect_overlay_holo_pad_hologram h = masters[holo_owner] as obj_effect_overlay_holo_pad_hologram;
        if (h == null || h.HC != null) //Holocalls can't change source.
            return false;
        foreach (obj_machinery_holopad another in Dm.each<obj_machinery_holopad>(holopads))
        {
            if (another == this)
                continue;
            if (another.validate_location(T))
            {
                unset_holo(holo_owner);
                if (another.masters != null && Dm.truthy(another.masters[holo_owner]))
                    another.clear_holo(holo_owner);
                another.set_holo(holo_owner, h);
                return true;
            }
        }
        return false;
    }

    // tg: code/game/machinery/hologram.dm:702
    public virtual bool validate_user(datum owner = null)
    {
        if (QDELETED(owner))
            return false;
        if (!isliving(owner))
            return true;
        mob_living user = owner as mob_living;
        if (Dm.truthy(user.incapacitated) || user.client == null)
            return false;
        return true;
    }

    //Can we display holos there
    //Area check instead of line of sight check because this is a called a lot if AI wants to move around.
    // tg: code/game/machinery/hologram.dm:714
    public virtual bool validate_location(turf T = null, bool check_los = false)
    {
        if (T.z == z && get_dist(T, this) <= holo_range && T.loc == get_area(this))
            return true;
        else
            return false;
    }

    // tg: code/game/machinery/hologram.dm:720
    public virtual async DmTask<bool> move_hologram(datum owner = null, turf new_turf = null)
    {
        if (!Dm.truthy(LAZYLEN(masters)) || !Dm.truthy(masters[owner]))
            return true;
        obj_effect_overlay_holo_pad_hologram holo = masters[owner] as obj_effect_overlay_holo_pad_hologram;
        bool transferred = false;
        if (!validate_location(new_turf))
        {
            if (!transfer_to_nearby_pad(new_turf, owner))
                return false;
            else
                transferred = true;
        }
        //All is good.
        await holo.abstract_move(new_turf);
        SET_PLANE(holo, ABOVE_GAME_PLANE, new_turf);
        if (!transferred)
            update_holoray(owner, new_turf);
        return true;
    }

    // tg: code/game/machinery/hologram.dm:738
    public virtual void update_holoray(datum holo_owner = null, turf new_turf = null)
    {
        obj_effect_overlay_holo_pad_hologram holo = masters[holo_owner] as obj_effect_overlay_holo_pad_hologram;
        obj_effect_overlay_holoray owner_ray = holorays[holo_owner] as obj_effect_overlay_holoray;
        float disty = holo.y - owner_ray.y;
        float distx = holo.x - owner_ray.x;
        float newangle;
        if (disty == 0)
        {
            if (distx >= 0)
                newangle = 90;
            else
                newangle = 270;
        }
        else
        {
            newangle = arctan(distx / disty);
            if (disty < 0)
                newangle += 180;
            else if (distx < 0)
                newangle += 360;
        }
        matrix M = matrix();
        if (get_dist(get_turf(holo), new_turf) <= 1)
            animate(owner_ray, Dm.named("transform", turn(M.Scale(1, Dm.sqrt(distx * distx + disty * disty)), newangle)), Dm.named("time", 1));
        else
            owner_ray.transform = turn(M.Scale(1, Dm.sqrt(distx * distx + disty * disty)), newangle);
    }

    /// Project a holographic pointing arrow from this holopad toward a target
    // tg: code/game/machinery/hologram.dm:764
    public virtual async DmTask<obj> holo_point(atom target = null, object invisibility = null)
    {
        invisibility ??= 0;
        if (Dm.truthy(pointing))
            return null;
        turf pad_turf = get_turf(this);
        turf target_turf = get_turf(target);
        if (pad_turf == null || target_turf == null)
            return null;
        pointing = TRUE;
        obj_effect_temp_visual_point_holo visual = Dm.@new<obj_effect_temp_visual_point_holo>(pad_turf, invisibility);
        obj_effect_overlay_holoray point_ray = Dm.@new<obj_effect_overlay_holoray>(pad_turf);
        float disty = target_turf.y - pad_turf.y;
        float distx = target_turf.x - pad_turf.x;
        float distance = Dm.sqrt(distx * distx + disty * disty);
        float angle;
        if (disty == 0)
            angle = (distx >= 0) ? 90 : 270;
        else
        {
            angle = arctan(distx / disty);
            if (disty < 0)
                angle += 180;
            else if (distx < 0)
                angle += 360;
        }
        matrix M = matrix();
        point_ray.transform = turn(M.Scale(1, distance), angle);
        animate(visual, Dm.named("pixel_x", (target_turf.x - pad_turf.x) * ICON_SIZE_X + target.pixel_x), Dm.named("pixel_y", (target_turf.y - pad_turf.y) * ICON_SIZE_Y + target.pixel_y), Dm.named("time", 1.7f), Dm.named("easing", EASE_OUT));
        await set_light(2);
        icon_state = Dm.text("[]1", base_icon_state);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_holo_point), point_ray), 2.5f * SECONDS, file: "code/game/machinery/hologram.dm", line: 791);
        return visual;
    }

    /// Called after the holo-point expires to restore the holopad's state
    // tg: code/game/machinery/hologram.dm:795
    public virtual void clear_holo_point(obj_effect_overlay_holoray point_ray = null)
    {
        qdel(point_ray);
        pointing = FALSE;
        SetLightsAndPower();
    }

    // RECORDED MESSAGES

    // tg: code/game/machinery/hologram.dm:802
    public virtual obj_effect_overlay_holo_pad_hologram setup_replay_holo(datum_holorecord record = null)
    {
        obj_effect_overlay_holo_pad_hologram hologram = Dm.@new<obj_effect_overlay_holo_pad_hologram>(loc); //Spawn a blank effect at the location.
        image work_off = record.caller_image;
        hologram.icon = work_off.icon;
        hologram.icon_state = work_off.icon_state;
        hologram.copy_overlays(work_off as IDmAppearance, TRUE);
        hologram.makeHologram();

        datum_language_holder holder = hologram.get_language_holder();
        holder.selected_language = record.language;
        hologram.mouse_opacity = MOUSE_OPACITY_TRANSPARENT; //So you can't click on it.
        hologram.layer = FLY_LAYER; //Above all the other objects/mobs. Or the vast majority of them.
        SET_PLANE_EXPLICIT(hologram, ABOVE_GAME_PLANE, this);
        Waiting.NoSleep(hologram.set_anchored(TRUE)); //So space wind cannot drag it.
        hologram.name = Dm.text("[] (Hologram)", record.caller_name); //If someone decides to right click.
        set_holo(record, hologram);

        visible_message(span_notice(I18n.T("A holographic image of [] flickers to life before your eyes!", record.caller_name)));
        return hologram;
    }

    // tg: code/game/machinery/hologram.dm:822
    public virtual async DmTask replay_start()
    {
        if (disk == null)
        {
            await say("Please insert the disc to play the recording.");
            return;
        }

        if (disk.record == null)
        {
            await say("There is no record on the disc. Please check the disk.");
            return;
        }

        if (!Dm.truthy(replay_mode))
        {
            replay_mode = TRUE;
            replay_holo = setup_replay_holo(disk.record);
            SetLightsAndPower();
            await replay_entry(1);
        }
    }

    // tg: code/game/machinery/hologram.dm:837
    public virtual object replay_stop()
    {
        if (disk == null || disk.record == null)
            return FALSE;
        if (Dm.truthy(replay_mode))
        {
            replay_mode = FALSE;
            offset = FALSE;
            clear_holo(disk.record);
            QDEL_NULL(ref replay_holo);
            SetLightsAndPower();
        }
        return null;
    }

    // tg: code/game/machinery/hologram.dm:847
    public virtual void record_start__proc(mob_living user = null) // rules 2.4: the var record_start keeps the name
    {
        if (user == null || disk == null || disk.record != null)
            return;
        disk.record = Dm.@new<datum_holorecord>();
        record_mode = TRUE;
        set_can_hear_flags(CAN_HEAR_RECORD_MODE);
        record_start = world.time;
        record_user = user;
        disk.record.set_caller_image(user);
    }

    // tg: code/game/machinery/hologram.dm:857
    public virtual void record_message(mob_living speaker = null, object message = null, object language = null)
    {
        if (!Dm.truthy(record_mode))
            return;
        //make this command so you can have multiple languages in single record
        if ((!Dm.truthy(disk.record.caller_name) || Dm.eq(disk.record.caller_name, "Unknown")) && speaker != null)
            disk.record.caller_name = speaker.name;
        if (!Dm.truthy(disk.record.language))
            disk.record.language = language as Type;
        else if (!Dm.eq(language, disk.record.language))
            Dm.append(ref disk.record.entries, Dm.list(Dm.list(HOLORECORD_LANGUAGE, language)));

        float current_delay = 0;
        foreach (DmList entry in Dm.each<DmList>(disk.record.entries))
        {
            if (!Dm.eq(entry[1], HOLORECORD_DELAY))
                continue;
            current_delay += Dm.num(entry[2]);
        }

        float time_delta = world.time - record_start - current_delay;

        if (time_delta >= 1)
            Dm.append(ref disk.record.entries, Dm.list(Dm.list(HOLORECORD_DELAY, time_delta)));
        Dm.append(ref disk.record.entries, Dm.list(Dm.list(HOLORECORD_SAY, message)));
        if (length(disk.record.entries) >= HOLORECORD_MAX_LENGTH)
            record_stop();
    }

    // tg: code/game/machinery/hologram.dm:882
    public virtual async DmTask replay_entry(float entry_number = 0)
    {
        while (true) // tg ends the proc with .(entry_number+1): a tail call, a loop here
        {
            if (!Dm.truthy(replay_mode))
                return;
            if (!Dm.truthy(length(disk.record.entries))) // check for zero entries such as photographs and no text recordings
                return; // and pretty much just display them statically untill manually stopped
            if (length(disk.record.entries) < entry_number)
            {
                if (Dm.truthy(loop_mode))
                    entry_number = 1;
                else
                {
                    replay_stop();
                    return;
                }
            }
            DmList entry = disk.record.entries[entry_number] as DmList;
            object command = entry[1];
            switch (Dm.str(command))
            {
                case HOLORECORD_SAY:
                    object message = entry[2];
                    if (replay_holo != null)
                        await replay_holo.say(message, sanitize: FALSE);
                    break;
                case HOLORECORD_SOUND:
                    playsound(this, entry[2], 50, TRUE);
                    break;
                case HOLORECORD_DELAY:
                    _addtimer(Dm.@new<datum_callback>(this, nameof(replay_entry), entry_number + 1), Dm.num(entry[2]), file: "code/game/machinery/hologram.dm", line: 905);
                    return;
                case HOLORECORD_LANGUAGE:
                {
                    datum_language_holder holder = replay_holo.get_language_holder();
                    holder.selected_language = entry[2];
                    break;
                }
                case HOLORECORD_PRESET:
                {
                    Type preset_type = entry[2] as Type;
                    datum_preset_holoimage H = Dm.@new<datum_preset_holoimage>(preset_type);
                    image work_off = await H.build_image();
                    replay_holo.icon = work_off.icon;
                    replay_holo.icon_state = work_off.icon_state;
                    replay_holo.copy_overlays(work_off as IDmAppearance, TRUE);
                    break;
                }
                case HOLORECORD_RENAME:
                    replay_holo.name = Dm.str(entry[2]) + " (Hologram)";
                    break;
            }
            entry_number = entry_number + 1;
        }
    }

    // tg: code/game/machinery/hologram.dm:924
    public virtual void record_stop()
    {
        if (Dm.truthy(record_mode))
        {
            record_mode = FALSE;
            record_user = null;
            set_can_hear_flags(CAN_HEAR_RECORD_MODE, false);
        }
    }

    // tg: code/game/machinery/hologram.dm:930
    public virtual void record_clear()
    {
        if (disk?.record != null)
            QDEL_NULL(ref disk.record);
    }
}

// tg: code/game/machinery/hologram.dm:120
public partial class obj_machinery_holopad_secure : obj_machinery_holopad
{
    // tg: code/game/machinery/hologram.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_circuitboard_machine_holopad board = circuit_value as obj_item_circuitboard_machine_holopad;
        board.secure = TRUE;
        board.build_path = typeof(obj_machinery_holopad_secure);
        return dot;
    }
}

// tg: code/game/machinery/hologram.dm:131
public partial class obj_machinery_holopad_tutorial : obj_machinery_holopad
{
    // tg: code/game/machinery/hologram.dm:138
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(proximity_range))
            proximity_monitor = Dm.@new<datum_proximity_monitor>(this, proximity_range);
        if (Dm.truthy(mapload))
        {
            obj_item_disk_holodisk new_disk = Dm.locate<obj_item_disk_holodisk>(loc);
            if (new_disk != null && disk == null)
            {
                Waiting.NoSleep(new_disk.forceMove(this));
                disk = new_disk;
            }
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR));
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:170
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (user == null)
            return null;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(is_operational))
            return null;
        if (Dm.truthy(replay_mode))
            replay_stop();
        else if (disk?.record != null)
            await replay_start();
        return null;
    }

    // tg: code/game/machinery/hologram.dm:179
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        if (!isliving(proximity_check_mob))
            return null;
        if (!Dm.truthy(replay_mode) && disk?.record != null)
            await replay_start();
        return null;
    }
}

// tg: code/game/machinery/hologram.dm:935
public partial class obj_effect_overlay_holo_pad_hologram : obj_effect_overlay
{
    // tg: code/game/machinery/hologram.dm:942
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_holographic_nature)));
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:946
    public override object Destroy(float? force = null)
    {
        Impersonation = null;
        if (!QDELETED(HC))
            HC.Disconnect(HC.calling_holopad);
        HC = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/hologram.dm:953
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/game/machinery/hologram.dm:956
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (Impersonation != null)
            return await Impersonation.examine(user);
        return await base.examine(user, __extra);
    }
}

// tg: code/game/machinery/hologram.dm:961
public partial class obj_effect_overlay_holoray : obj_effect_overlay
{
    // tg: code/game/machinery/hologram.dm:975
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(render_target))
        {
            render_target = Dm.text("holoray#[]", TgState.Current.obj_effect_overlay_holoray__uid); // var/static/uid
            TgState.Current.obj_effect_overlay_holoray__uid++;
        }
        // Let's GLOW BROTHER! (Doing it like this is the most robust option compared to duped overlays)
        // DEVIATION(DV12): glow = new /atom/movable/render_step/emissive(null, src), its overlay and update_overlays_on_z: the emissive glow
        return dot;
    }

    // tg: code/game/machinery/hologram.dm:985
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref glow);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/hologram.dm:82  var/static/force_answer_call
    internal bool obj_machinery_holopad__force_answer_call;
    // tg: code/game/machinery/hologram.dm:83  var/static/list/holopads
    internal DmList obj_machinery_holopad__holopads;
    // tg: code/game/machinery/hologram.dm:109  var/static/list/hovering_mob_typechecks
    internal DmList obj_machinery_holopad__hovering_mob_typechecks;
    // tg: code/game/machinery/hologram.dm:978  var/static/uid
    internal float obj_effect_overlay_holoray__uid;
}
