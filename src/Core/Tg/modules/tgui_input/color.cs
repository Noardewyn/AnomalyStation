// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice D3 (lobby.md 8, the preferences' colour buttons): the whole file, as text.cs: the proc waits for the ColorPickerModal
// window and the client's submit or cancel ends the wait. The preferences it reads (tgui_input, tgui_input_large, tgui_input_swapped)
// are not translated (GAP(lead, modules/client/preferences.cs)): their defaults (TRUE, FALSE, TRUE), so the plain BYOND input branch
// is never taken. `choice` is declared here: the types tool reads `var/choice` as a type path.

public static partial class GlobalProcs
{
    /**
     * Creates a TGUI color picker window and returns the user's response.
     *
     * This proc should be used to create a color picker that the caller will wait for a response from.
     * Arguments:
     * * user - The user to show the picker to.
     * * title - The of the picker modal, shown on the top of the TGUI window.
     * * timeout - The timeout of the picker, after which the modal will close and qdel itself. Set to zero for no timeout.
     * * autofocus - The bool that controls if this picker should grab window focus.
     */
    // tg: code/modules/tgui_input/color.dm:11
    public static async DmTask<object> tgui_color_picker(object user = null, object message = null, object title = null, object @default = null, object timeout = null, object autofocus = null, Action<object> on_preview = null)
    {
        @default ??= "#000000";
        timeout ??= 0;
        autofocus ??= TRUE;
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
        // Client does NOT have tgui_input on: Returns regular input
        // if(!user.client.prefs.read_preference(/datum/preference/toggle/tgui_input)): GAP(lead), the preference's default (TRUE).
        datum_tgui_color_picker picker = Dm.@new<datum_tgui_color_picker>(user__mob, message, title, @default, timeout, autofocus);
        picker.on_preview = on_preview;
        await picker.ui_interact(user__mob);
        await picker.wait();
        if (Dm.truthy(picker))
        {
            dot = picker.choice;
            qdel(picker);
        }
        return dot;
    }
}

/**
 * # tgui_color_picker
 *
 * Datum used for instantiating and using a TGUI-controlled color picker.
 */
// tg: code/modules/tgui_input/color.dm:35
[DmPath("/datum/tgui_color_picker")]
public partial class datum_tgui_color_picker : datum
{
    /// The color the user selected, null if no selection has been made
    // tg: code/modules/tgui_input/color.dm:43
    public object choice;

    /// DEVIATION(DV230): the colour the window shows while it is being picked (the "preview" act), handed to on_preview once a tick
    public object preview_choice, previewed;
    public Action<object> on_preview;

    // tg: code/modules/tgui_input/color.dm:53
    [DmParams("user", "message", "title", "default", "timeout", "autofocus")]
    public override async DmTask New(params object[] __extra)
    {
        object message = Dm.arg(__extra, 1);
        object title = Dm.arg(__extra, 2);
        object @default = Dm.arg(__extra, 3);
        object timeout = Dm.arg(__extra, 4);
        object autofocus = Dm.arg(__extra, 5);
        this.autofocus = autofocus;
        this.title = title;
        this.@default = @default;
        this.message = message;
        if (Dm.truthy(timeout))
        {
            this.timeout = timeout;
            start_time = world.time;
            // QDEL_IN(src, timeout) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), Dm.num(timeout), file: "code/modules/tgui_input/color.dm", line: 61);
        }
    }

    // tg: code/modules/tgui_input/color.dm:63
    public override object Destroy(float? force = null)
    {
        SStgui.close_uis(this);
        return base.Destroy(force);
    }

    /**
     * Waits for a user's response to the tgui_color_picker's prompt before returning. Returns early if
     * the window was closed by the user.
     */
    // tg: code/modules/tgui_input/color.dm:71
    public virtual async DmTask wait()
    {
        while (!Dm.truthy(choice) && !Dm.truthy(closed) && !QDELETED(this))
        {
            if (on_preview != null && !Dm.eq(preview_choice, previewed))
                on_preview(previewed = preview_choice);
            await stoplag(1);
        }
    }

    // tg: code/modules/tgui_input/color.dm:75
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ColorPickerModal");
            await ui.open();
            ui.set_autoupdate(Dm.num(timeout) > 0 ? TRUE : FALSE);
        }
        return null;
    }

    // tg: code/modules/tgui_input/color.dm:82
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        closed = TRUE;
    }

    // tg: code/modules/tgui_input/color.dm:86
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/tgui_input/color.dm:89
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["autofocus"] = autofocus;
        data["large_buttons"] = FALSE; // !user.client?.prefs || read_preference(/datum/preference/toggle/tgui_input_large): GAP, the default
        data["swapped_buttons"] = TRUE; // !user.client?.prefs || read_preference(/datum/preference/toggle/tgui_input_swapped): GAP, the default
        data["title"] = title;
        data["default_color"] = @default;
        data["message"] = message;
        return data;
    }

    // tg: code/modules/tgui_input/color.dm:98
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (Dm.truthy(timeout))
            data["timeout"] = Math.Clamp((Dm.num(timeout) - (world.time - Dm.num(start_time)) - 1 * SECONDS) / (Dm.num(timeout) - 1 * SECONDS), 0f, 1f); // CLAMP01 (__DEFINES/maths.dm), inline
        return data;
    }

    // tg: code/modules/tgui_input/color.dm:103
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "submit":
                string raw_data = LOWER_TEXT(@params["entry"]);
                object hex = sanitize_hexcolor(raw_data, desired_format: 6, include_crunch: TRUE);
                if (!Dm.truthy(hex))
                    return dot;
                set_choice(hex);
                closed = TRUE;
                SStgui.close_uis(this);
                return TRUE;
            case "preview": // DEVIATION(DV230)
                object preview_hex = sanitize_hexcolor(LOWER_TEXT(@params["entry"]), desired_format: 6, include_crunch: TRUE);
                if (Dm.truthy(preview_hex))
                    preview_choice = preview_hex;
                return FALSE;
            case "cancel":
                closed = TRUE;
                SStgui.close_uis(this);
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/tgui_input/color.dm:122
    public virtual void set_choice(object choice = null)
    {
        this.choice = choice;
    }
}
