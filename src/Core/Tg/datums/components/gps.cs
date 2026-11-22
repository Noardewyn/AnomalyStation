// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/gps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base component, a signal in GLOB.GPS_list, and the handheld GPS (/datum/component/gps/item and tgui's Gps window, silicons
// SI2: the mining cyborg's GPS); the kheiral cuffs' GPS comes with the cuffs.

///GPS component. Atoms that have this show up on gps. Pretty simple stuff.
// tg: code/datums/components/gps.dm:5
public partial class datum_component_gps : datum_component
{
    // tg: code/datums/components/gps.dm:11
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        string _gpstag = Dm.arg(__extra, 0) as string ?? "COM0";
        object _tracking = Dm.arg(__extra, 1) ?? TRUE;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        gpstag = _gpstag;
        tracking = Dm.num(_tracking);
        Dm.append(ref GLOB.GPS_list, this);
        return null;
    }

    // tg: code/datums/components/gps.dm:18
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.GPS_list, this);
        return base.Destroy(force);
    }
}

///GPS component subtype. Only gps/item's can be used to open the UI.
// tg: code/datums/components/gps.dm:42
public partial class datum_component_gps_item : datum_component_gps
{
    // tg: code/datums/components/gps.dm:49
    [DmParams("_gpstag", "_tracking", "emp_proof", "state", "overlay_state", "debug")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object emp_proof = Dm.arg(__extra, 2) ?? FALSE;
        datum_ui_state state = Dm.arg<datum_ui_state>(__extra, 3);
        object overlay_state = Dm.arg(__extra, 4) ?? "working";
        object debug = Dm.arg(__extra, 5) ?? FALSE;
        object dot = await base.Initialize(__extra);
        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE) || !isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        if (state == null)
            state = GLOB.default_state;
        this.state = state;
        debug_mode = Dm.num(debug);

        atom A = (atom)parent;
        if (Dm.truthy(overlay_state))
            A.add_overlay(overlay_state);
        A.name = Dm.text("[] ([])", Dm.initial(A, (atom p) => p.name), gpstag);
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(interact));

        if (Dm.truthy(debug_mode) && Dm.truthy(tracking))
            RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(tag_the_floor));

        if (!Dm.truthy(emp_proof))
            RegisterSignal(parent, COMSIG_ATOM_EMP_ACT, nameof(on_emp_act));

        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_CLICK_ALT, nameof(on_click_alt));
        return dot;
    }

    // tg: code/datums/components/gps.dm:74
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(tagged))
            clear();
        return base.Destroy(force);
    }

    ///Called on COMSIG_ITEM_ATTACK_SELF
    // tg: code/datums/components/gps.dm:80
    [SignalHandler]
    public virtual void interact(datum source = null, mob user = null)
    {
        if (user != null)
            Waiting.Detach(ui_interact(user)); // INVOKE_ASYNC(src, PROC_REF(ui_interact), user)
    }

    ///Called on COMSIG_MOVABLE_MOVED
    // tg: code/datums/components/gps.dm:87
    [SignalHandler]
    public virtual void tag_the_floor(atom_movable mover = null, turf old_loc = null)
    {
        if (!Dm.truthy(debug_mode))
            return;

        turf tagged_turf = get_turf(mover);
        if (tagged_turf != null)
        {
            tagged_turf.color = rgb(rand(0, 255), rand(0, 255), rand(0, 255)); // RANDOM_COLOUR
            tagged_turf.maptext = MAPTEXT(Dm.text("[],[],[]", tagged_turf.x, tagged_turf.y, tagged_turf.z));
            LAZYOR(ref tagged, tagged_turf);
        }
    }

    ///Called on COMSIG_MOVABLE_MOVED
    // tg: code/datums/components/gps.dm:100
    [SignalHandler]
    public virtual void clear()
    {
        while (Dm.truthy(tagged.len))
        {
            turf tagged_turf = (turf)pop(tagged);
            tagged_turf.color = Dm.initial(tagged_turf, (turf p) => p.color);
            tagged_turf.maptext = Dm.initial(tagged_turf, (turf p) => p.maptext);
        }
        tagged = null; // LAZYNULL(tagged)
    }

    ///Called on COMSIG_ATOM_EXAMINE
    // tg: code/datums/components/gps.dm:110
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("Alt-click to switch it [].", Dm.truthy(tracking) ? "off" : "on")));
    }

    ///Called on COMSIG_ATOM_EMP_ACT
    // tg: code/datums/components/gps.dm:116
    [SignalHandler]
    public virtual void on_emp_act(datum source = null, object severity = null, object protection = null)
    {
        if (Dm.truthy(Dm.bitand(protection, EMP_PROTECT_SELF)))
            return;
        emped = TRUE;
        atom A = (atom)parent;
        A.cut_overlay("working");
        A.add_overlay("emp");
        _addtimer(Dm.@new<datum_callback>(this, nameof(reboot)), 300, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/datums/components/gps.dm", line: 124); //if a new EMP happens, remove the old timer so it doesn't reactivate early
        SStgui.close_uis(this); //Close the UI control if it is open.
    }

    ///Restarts the GPS after getting turned off by an EMP.
    // tg: code/datums/components/gps.dm:128
    public virtual void reboot()
    {
        emped = FALSE;
        atom A = (atom)parent;
        A.cut_overlay("emp");
        A.add_overlay("working");
    }

    ///Calls toggletracking
    // tg: code/datums/components/gps.dm:135
    [SignalHandler]
    public virtual object on_click_alt(datum source = null, mob user = null)
    {
        Waiting.NoSleep(toggletracking(user));
        return CLICK_ACTION_SUCCESS;
    }

    ///Toggles the tracking for the gps
    // tg: code/datums/components/gps.dm:142
    public virtual async DmTask toggletracking(mob user = null)
    {
        if (!Dm.truthy(await user.can_perform_action((atom)parent, Dm.bitor(ALLOW_RESTING, ALLOW_PAI))))
            return; //user not valid to use gps
        if (Dm.truthy(emped))
        {
            to_chat(user, span_warning(I18n.T("It's busted!")));
            return;
        }
        atom A = (atom)parent;
        if (Dm.truthy(tracking))
        {
            A.cut_overlay("working");
            to_chat(user, span_notice(I18n.T("[] is no longer tracking, or visible to other GPS devices.", parent)));
            tracking = FALSE;
        }
        else
        {
            A.add_overlay("working");
            to_chat(user, span_notice(I18n.T("[] is now tracking, and visible to other GPS devices.", parent)));
            tracking = TRUE;
        }

        if (Dm.truthy(debug_mode))
        {
            if (Dm.truthy(tracking))
                RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(tag_the_floor));
            else
            {
                UnregisterSignal(parent, COMSIG_MOVABLE_MOVED);
                clear();
            }
        }
    }

    // tg: code/datums/components/gps.dm:165
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(emped))
        {
            to_chat(user, span_hear(I18n.T("[] fizzles weakly.", parent)));
            return null;
        }
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Gps");
            await ui.open();
        }
        ui.set_autoupdate(updating);
        return null;
    }

    // tg: code/datums/components/gps.dm:175
    public override object ui_state(mob user = null)
    {
        return state;
    }

    // tg: code/datums/components/gps.dm:178
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["power"] = tracking;
        data["tag"] = gpstag;
        data["updating"] = updating;
        data["globalmode"] = global_mode;
        if (!Dm.truthy(tracking) || Dm.truthy(emped)) //Do not bother scanning if the GPS is off or EMPed
            return data;

        turf curr = get_turf((atom)parent);
        data["currentArea"] = Dm.text("[]", get_area_name(curr, TRUE));
        data["currentCoords"] = Dm.text("[], [], []", curr.x, curr.y, curr.z);

        DmList signals = Dm.list();
        data["signals"] = Dm.list();

        foreach (datum_component_gps gps in Dm.each_cast<datum_component_gps>(GLOB.GPS_list))
        {
            if (gps == this || Dm.truthy(gps.emped) || !Dm.truthy(gps.tracking))
                continue;
            turf pos = get_turf((atom)gps.parent);
            if (pos == null || (!Dm.truthy(global_mode) && pos.z != curr.z))
                continue;
            DmList signal = Dm.list();
            signal["entrytag"] = gps.gpstag; //Name or 'tag' of the GPS
            signal["coords"] = Dm.text("[], [], []", pos.x, pos.y, pos.z);
            // Distance is calculated for the same z-level only, and direction is calculated for crosslinked/neighboring and same z-levels.
            if (pos.z == curr.z)
            {
                signal["dist"] = max(get_dist(curr, pos), 0); //Distance between the src and remote GPS turfs
                signal["degrees"] = round(Dm.num(get_angle(curr, pos))); //0-360 degree directional bearing, for more precision.
            }
            else
            {
                object angle = get_linked_z_angle(curr.z, pos.z);
                if (angle != null)
                    signal["degrees"] = angle;
            }
            signals = Dm.add(signals, Dm.list(signal)) as DmList; //Add this signal to the list of signals
        }
        data["signals"] = signals;
        return data;
    }

    // tg: code/datums/components/gps.dm:215
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "rename"))
        {
            atom parentasatom = (atom)parent;
            object a = await tgui_input_text(usr, I18n.T("Enter the desired tag"), I18n.T("GPS Tag"), gpstag, max_length: 20);
            if (QDELETED(ui) || !Dm.eq(ui.status, UI_INTERACTIVE))
                return dot;
            if (!Dm.truthy(a))
                return dot;

            gpstag = a as string;
            dot = TRUE;
            usr.log_message(Dm.text("renamed [] to \"[] ([])\".", parentasatom, Dm.initial(parentasatom, (atom p) => p.name), gpstag), LOG_GAME);
            parentasatom.name = Dm.text("[] ([])", Dm.initial(parentasatom, (atom p) => p.name), gpstag);
        }
        else if (Dm.eq(action, "power"))
        {
            await toggletracking(usr);
            dot = TRUE;
        }
        else if (Dm.eq(action, "updating"))
        {
            updating = !Dm.truthy(updating) ? TRUE : FALSE;
            dot = TRUE;
        }
        else if (Dm.eq(action, "globalmode"))
        {
            global_mode = !Dm.truthy(global_mode) ? TRUE : FALSE;
            dot = TRUE;
        }
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/datums/components/gps.dm:2  GLOBAL_LIST_EMPTY(GPS_list)
    public DmList GPS_list;

    // tg: (GLOBAL_MANAGED(GPS_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalGPS_list()
    {
        GPS_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "GPS_list");
    }
}
