// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/mechlaunchpad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the orbital mech pad console. A mapped console connects to the pad beside it and to the pads of its id; a built
// one takes them from a multitool's buffer (its own pad first, then up to three others). Its MechpadConsole window selects,
// renames, unlinks and launches to a pad; a mech's melee on the console starts the launch with the console's random beeps. The
// launch flicks the pad, beeps, turns the mech south and calls the pad's supply pod. The var block is generated (D111).

// tg: code/game/machinery/computer/mechlaunchpad.dm:1
public partial class obj_machinery_computer_mechpad : obj_machinery_computer
{
    // tg: code/game/machinery/computer/mechlaunchpad.dm:126  #define MECH_LAUNCH_TIME (5 SECONDS) (file-local)
    private const float MECH_LAUNCH_TIME = 5 * SECONDS;

    // tg: code/game/machinery/computer/mechlaunchpad.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            connect_launchpad(find_pad() as obj_machinery_mechpad);
            return INITIALIZE_HINT_LATELOAD;
        }
        else
            id = Dm.text("handmade[]", REF(this));
        return dot;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:27
    public virtual void connect_launchpad(obj_machinery_mechpad pad = null)
    {
        if (connected_mechpad != null)
            return;
        connected_mechpad = pad;
        connected_mechpad.id = id;
        RegisterSignal(connected_mechpad, COMSIG_QDELETING, nameof(unconnect_launchpad));
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:34
    [SignalHandler]
    public virtual void unconnect_launchpad(obj_machinery_mechpad pad = null)
    {
        connected_mechpad = null;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:38
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        foreach (obj_machinery_mechpad pad in Dm.each<obj_machinery_mechpad>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_mechpad))))
        {
            if (pad == connected_mechpad)
                continue;
            if (pad.id != id)
                continue;
            add_pad(pad);
            if (Dm.num(mechpads.len) > maximum_pads)
                break;
        }
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:51
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            return await base.mech_melee_attack(mecha_attacker, user);
        object mech_dir = mecha_attacker.dir;
        balloon_alert(user, I18n.T("carefully starting launch process..."));
        Waiting.Detach(random_beeps(user, MECH_LAUNCH_TIME, 0.5f * SECONDS, 1.5f * SECONDS));
        if (!Dm.truthy(await do_after(user, MECH_LAUNCH_TIME, this, extra_checks: Dm.@new<datum_callback>(this, nameof(do_after_checks), mecha_attacker, mech_dir))))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return null;
        }
        obj_machinery_mechpad current_pad = mechpads[selected_id] as obj_machinery_mechpad;
        try_launch(user, current_pad);
        return null;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:65
    public virtual object do_after_checks(obj_vehicle_sealed_mecha mech = null, object mech_dir = null)
    {
        return Dm.num(Dm.eq(mech.dir, mech_dir) && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))));
    }

    /// A proc that makes random beeping sounds for a set amount of time, the sounds are separated by a random amount of time.
    // tg: code/game/machinery/computer/mechlaunchpad.dm:69
    public virtual async DmTask random_beeps(mob user = null, float? time = null, float? mintime = null, float? maxtime = null)
    {
        time ??= 0;
        mintime ??= 0;
        maxtime ??= 1;
        DmList beep_sounds = TgState.Current.obj_machinery_computer_mechpad__random_beeps__beep_sounds ??= Dm.list(Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), Dm.resource("sound/machines/terminal/terminal_prompt_deny.ogg"), Dm.resource("sound/machines/terminal/terminal_error.ogg"), Dm.resource("sound/machines/terminal/terminal_select.ogg"), Dm.resource("sound/machines/terminal/terminal_success.ogg")); // var/static/list/beep_sounds
        float time_to_spend = 0;
        float orig_time = time.Value;
        while (time > 0)
        {
            if (!Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)) && time != orig_time)
                return;
            time_to_spend = rand(mintime.Value, maxtime.Value);
            playsound(this, pick(beep_sounds), 75);
            time -= time_to_spend;
            await stoplag(time_to_spend);
        }
    }

    ///Tries to locate a pad in the cardinal directions, if it finds one it returns it
    // tg: code/game/machinery/computer/mechlaunchpad.dm:82
    public virtual object find_pad()
    {
        object found_mechpad = null;
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            found_mechpad = Dm.locate<obj_machinery_mechpad>(get_step(this, Dm.num(direction)));
            if (!Dm.truthy(found_mechpad))
                continue;
            return found_mechpad;
        }
        return null;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:90
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item_multitool multitool = tool as obj_item_multitool;
        object dot = NONE;
        if (!istype(multitool.buffer, typeof(obj_machinery_mechpad)))
            return dot;

        obj_machinery_mechpad buffered_pad = multitool.buffer as obj_machinery_mechpad;
        if (!(Dm.num(mechpads.len) < maximum_pads))
        {
            to_chat(user, span_warning(I18n.T("[] cannot handle any more connections!", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (buffered_pad == connected_mechpad)
        {
            to_chat(user, span_warning(I18n.T("[] cannot connect to its own mechpad!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (connected_mechpad == null && buffered_pad == find_pad())
        {
            if (Dm.@in(buffered_pad, mechpads))
                remove_pad(buffered_pad);
            connect_launchpad(buffered_pad);
            multitool.set_buffer(null);
            to_chat(user, span_notice(I18n.T("You connect the console to the pad with data from \\the []'s buffer.", multitool)));
            return ITEM_INTERACT_SUCCESS;
        }

        add_pad(buffered_pad);
        multitool.set_buffer(null);
        to_chat(user, span_notice(I18n.T("You upload the data from \\the []'s buffer.", multitool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:117
    public virtual void add_pad(obj_machinery_mechpad pad = null)
    {
        Dm.append(ref mechpads, pad);
        RegisterSignal(pad, COMSIG_QDELETING, nameof(remove_pad));
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:121
    [SignalHandler]
    public virtual void remove_pad(obj_machinery_mechpad pad = null)
    {
        Dm.remove(ref mechpads, pad);
        UnregisterSignal(pad, COMSIG_QDELETING);
    }

    /**
     * Tries to call the launch proc on the connected mechpad, returns if unavailable
     * Arguments:
     * * user - The user of the proc
     * * where - The mechpad that the connected mechpad will try to send a supply pod to
     */
    // tg: code/game/machinery/computer/mechlaunchpad.dm:132
    public virtual void try_launch(mob user = null, obj_machinery_mechpad where = null)
    {
        if (!Dm.truthy(can_launch(user, where)))
            return;
        flick("mechpad-launch", connected_mechpad);
        playsound(connected_mechpad, "sound/machines/beep/triple_beep.ogg", 50, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_launch), user, where), 1 * SECONDS, file: "code/game/machinery/computer/mechlaunchpad.dm", line: 137);
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:139
    public virtual async DmTask start_launch(mob user = null, obj_machinery_mechpad where = null)
    {
        if (!Dm.truthy(can_launch(user, where, silent: TRUE)))
            return;
        obj_vehicle_sealed_mecha mech = Dm.locate<obj_vehicle_sealed_mecha>(get_turf(connected_mechpad));
        await mech.setDir(SOUTH);
        await connected_mechpad.launch(where);
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:146
    public virtual object can_launch(mob user = null, obj_machinery_mechpad where = null, float? silent = null)
    {
        silent ??= FALSE;
        if (QDELETED(where))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("No destination!")));
            return FALSE;
        }
        if (connected_mechpad == null)
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("[] has no connected pad!", this)));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(connected_mechpad.machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(Dm.bitand(where.machine_stat, Dm.bitor(BROKEN, NOPOWER))))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("Pads are nonfunctional!")));
            return FALSE;
        }
        if (Dm.truthy(connected_mechpad.panel_open) || Dm.truthy(where.panel_open))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("Pads have open panels!")));
            return FALSE;
        }
        obj_vehicle_sealed_mecha mech = Dm.locate<obj_vehicle_sealed_mecha>(get_turf(connected_mechpad));
        if (mech == null)
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("[] detects no mecha on the pad!", this)));
            return FALSE;
        }
        if (Dm.truthy(where.mech_only) && Dm.truthy(Dm.locate<mob_living>(mech.get_all_contents())))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("The target pad does not allow lifeforms!")));
            return FALSE;
        }
        return TRUE;
    }

    ///Returns the pad of the value specified
    // tg: code/game/machinery/computer/mechlaunchpad.dm:176
    public virtual object get_pad(object number = null)
    {
        obj_machinery_mechpad pad = mechpads[number] as obj_machinery_mechpad;
        return pad;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:180
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MechpadConsole", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:187
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        DmList pad_list = Dm.list();
        for (float i = 1, i__end = length(mechpads); i <= i__end; i++)
        {
            obj_machinery_mechpad pad = get_pad(i) as obj_machinery_mechpad;
            DmList this_pad = Dm.list();
            this_pad["name"] = pad.display_name;
            this_pad["id"] = i;
            Dm.append(ref pad_list, Dm.list(this_pad));
        }
        data["mechpads"] = pad_list;
        data["selected_id"] = selected_id;
        data["connected_mechpad"] = Dm.num(connected_mechpad != null && !Dm.truthy(Dm.bitand(connected_mechpad.machine_stat, Dm.bitor(BROKEN, NOPOWER))));
        if (Dm.truthy(selected_id))
        {
            obj_machinery_mechpad current_pad = mechpads[selected_id] as obj_machinery_mechpad;
            if (current_pad == null)
                selected_id = null;
            else
            {
                data["pad_name"] = current_pad.display_name;
                data["pad_active"] = Dm.num(current_pad != null && !Dm.truthy(Dm.bitand(current_pad.machine_stat, Dm.bitor(BROKEN, NOPOWER))));
                data["mechonly"] = current_pad.mech_only;
            }
        }
        return data;
    }

    // tg: code/game/machinery/computer/mechlaunchpad.dm:209
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        obj_machinery_mechpad current_pad = mechpads[selected_id] as obj_machinery_mechpad;
        object sw__1 = action;
        if (Dm.eq(sw__1, "select_pad"))
            selected_id = text2num(@params["id"]);
        else if (Dm.eq(sw__1, "rename"))
        {
            object new_name = @params["name"];
            if (!Dm.truthy(new_name))
                return dot;
            current_pad.display_name = Dm.str(new_name);
        }
        else if (Dm.eq(sw__1, "remove"))
        {
            if (usr != null && Dm.eq(await tgui_alert(usr, "Are you sure?", "Unlink Orbital Pad", Dm.list("I'm Sure", "Abort")), "I'm Sure"))
            {
                remove_pad(current_pad);
                selected_id = null;
            }
        }
        else if (Dm.eq(sw__1, "launch"))
            try_launch(usr, current_pad);
        dot = TRUE;
        return dot;
    }
}

// The mech pad console's proc-level static (rules 10.6).
public sealed partial class TgState
{
    // tg: code/game/machinery/computer/mechlaunchpad.dm:73
    internal DmList obj_machinery_computer_mechpad__random_beeps__beep_sounds;
}
