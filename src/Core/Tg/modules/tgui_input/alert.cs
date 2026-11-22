// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file. The proc waits like show_radial_menu (DV40's pattern): the window is a tgui window
// (AlertModal, protocol 16) and the client's choice or cancel ends the wait. Past 3 buttons tg falls back to
// tgui_input_list, which is not translated yet (Deferred, null). The preferences it reads (tgui_input, tgui_input_large,
// tgui_input_swapped) are not translated (GAP(lead, modules/client/preferences.cs)): their defaults (TRUE, FALSE, TRUE).
// The var block is generated (choice is declared here: the generated block types it as a typepath).

public static partial class GlobalProcs
{
    /**
     * Creates a TGUI alert window and returns the user's response.
     *
     * This proc should be used to create alerts that the caller will wait for a response from.
     * Arguments:
     * * user - The user to show the alert to.
     * * message - The content of the alert, shown in the body of the TGUI window.
     * * title - The of the alert modal, shown on the top of the TGUI window.
     * * buttons - The options that can be chosen by the user, each string is assigned a button on the UI.
     * * timeout - The timeout of the alert, after which the modal will close and qdel itself. Set to zero for no timeout.
     * * autofocus - The bool that controls if this alert should grab window focus.
     * * ui_state - The TGUI UI state that will be returned in ui_state(). Default: always_state
     */
    // tg: code/modules/tgui_input/alert.dm:13
    public static async DmTask<object> tgui_alert(object user = null, object message = null, object title = null, DmList buttons = null, object timeout = null, object autofocus = null, datum_ui_state ui_state = null)
    {
        message ??= "";
        buttons ??= Dm.list("Ok");
        timeout ??= 0;
        autofocus ??= TRUE;
        ui_state ??= GLOB.always_state;
        object dot = null;
        if (!Dm.truthy(user))
            user = World.Current.usr;
        if (!istype(user, typeof(mob)))
        {
            if (istype(user, typeof(client)))
            {
                client client = (client)user;
                user = client.mob;
            }
            else
                return null;
        }
        mob user__mob = (mob)user;
        if (user__mob.client == null)
            return null;
        // A gentle nudge - you should not be using TGUI alert for anything other than a simple message.
        if (Dm.length(buttons) > 3)
        {
            // log_tgui(user, "Error: TGUI Alert initiated with too many buttons. Use a list.", "TguiAlert"): the tgui log (M10)
            // return tgui_input_list(user, message, title, buttons, timeout=timeout)
            Dm.Deferred("code/modules/tgui_input/alert.dm:30", "tgui_alert: more than 3 buttons fall back to tgui_input_list (the list input window, not translated yet)");
            return null;
        }
        // Client does NOT have tgui_input on: Returns regular input
        // if(!user.client.prefs.read_preference(/datum/preference/toggle/tgui_input)): GAP(lead), the preference's default (TRUE).
        datum_tgui_alert alert = Dm.@new<datum_tgui_alert>(user__mob, message, title, buttons, timeout, autofocus, ui_state);
        await alert.ui_interact(user__mob);
        await alert.wait();
        if (Dm.truthy(alert))
        {
            dot = alert.choice;
            qdel(alert);
        }
        return dot;
    }
}

/**
 * # tgui_alert
 *
 * Datum used for instantiating and using a TGUI-controlled modal that prompts the user with
 * a message and has buttons for responses.
 */
// tg: code/modules/tgui_input/alert.dm:49
[DmPath("/datum/tgui_alert")]
public partial class datum_tgui_alert : datum
{
    /// The button that the user has pressed, null if no selection has been made
    // tg: code/modules/tgui_input/alert.dm:57
    public object choice;

    // tg: code/modules/tgui_input/alert.dm:69
    [DmParams("user", "message", "title", "buttons", "timeout", "autofocus", "ui_state")]
    public override async DmTask New(params object[] __extra)
    {
        object message = Dm.arg(__extra, 1);
        object title = Dm.arg(__extra, 2);
        DmList buttons = Dm.arg(__extra, 3) as DmList;
        object timeout = Dm.arg(__extra, 4);
        object autofocus = Dm.arg(__extra, 5);
        object ui_state = Dm.arg(__extra, 6);
        this.autofocus = autofocus;
        this.buttons = buttons.Copy();
        this.message = message;
        this.title = title;
        this.state = ui_state as datum_ui_state;
        if (Dm.truthy(timeout))
        {
            this.timeout = timeout;
            start_time = world.time;
            // QDEL_IN(src, timeout) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), Dm.num(timeout), file: "code/modules/tgui_input/alert.dm", line: 78);
        }
    }

    // tg: code/modules/tgui_input/alert.dm:80
    public override object Destroy(float? force = null)
    {
        SStgui.close_uis(this);
        state = null;
        buttons?.Cut();
        return base.Destroy(force);
    }

    /**
     * Waits for a user's response to the tgui_alert's prompt before returning. Returns early if
     * the window was closed by the user.
     */
    // tg: code/modules/tgui_input/alert.dm:90
    public virtual async DmTask wait()
    {
        while (!Dm.truthy(choice) && !Dm.truthy(closed) && !QDELETED(this))
            await stoplag(1);
    }

    // tg: code/modules/tgui_input/alert.dm:94
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AlertModal");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/tgui_input/alert.dm:100
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        closed = TRUE;
    }

    // tg: code/modules/tgui_input/alert.dm:104
    public override object ui_state(mob user = null)
    {
        return state;
    }

    // tg: code/modules/tgui_input/alert.dm:107
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["autofocus"] = autofocus;
        data["buttons"] = buttons;
        data["message"] = message;
        data["large_buttons"] = FALSE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_large): GAP, the default
        data["swapped_buttons"] = TRUE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_swapped): GAP, the default
        data["title"] = title;
        return data;
    }

    // tg: code/modules/tgui_input/alert.dm:117
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (Dm.truthy(timeout))
            data["timeout"] = Math.Clamp((Dm.num(timeout) - (world.time - Dm.num(start_time)) - 1 * SECONDS) / (Dm.num(timeout) - 1 * SECONDS), 0f, 1f); // CLAMP01 (__DEFINES/maths.dm), inline
        return data;
    }

    // tg: code/modules/tgui_input/alert.dm:123
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "choose":
                if (!Dm.@in(@params["choice"], buttons))
                    throw Dm.CRASH(Dm.text("[] entered a non-existent button choice: []", World.Current.usr, @params["choice"]), "code/modules/tgui_input/alert.dm", 128);
                set_choice(@params["choice"]);
                closed = TRUE;
                SStgui.close_uis(this);
                return TRUE;
            case "cancel":
                closed = TRUE;
                SStgui.close_uis(this);
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/tgui_input/alert.dm:140
    public virtual void set_choice(object choice = null)
    {
        this.choice = choice;
    }
}
