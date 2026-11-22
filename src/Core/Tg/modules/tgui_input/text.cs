// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/text.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice D (D241, the succumb alert's last words): the whole file, as number.cs: the proc waits for the TextInputModal window
// (protocol 16) and the client's submit or cancel ends the wait. The preferences it reads (tgui_input, tgui_input_large,
// tgui_input_swapped) are not translated (GAP(lead, modules/client/preferences.cs)): their defaults (TRUE, FALSE, TRUE), so the
// plain BYOND input branch is never taken. The var block is generated.

public static partial class GlobalProcs
{
    /**
     * Creates a TGUI window with a text input. Returns the user's response.
     *
     * This proc should be used to create windows for text entry that the caller will wait for a response from.
     * If tgui fancy chat is turned off: Will return a normal input. If max_length is specified, will return
     * stripped_multiline_input.
     *
     * Arguments:
     * * user - The user to show the text input to.
     * * message - The content of the text input, shown in the body of the TGUI window.
     * * title - The title of the text input modal, shown on the top of the TGUI window.
     * * default - The default (or current) value, shown as a placeholder.
     * * max_length - Specifies a max length for input. By default is infinity.
     * * multiline -  Bool that determines if the input box is much larger. Good for large messages, laws, etc.
     * * encode - Toggling this determines if input is filtered via html_encode. Setting this to FALSE gives raw input.
     * * timeout - The timeout of the textbox, after which the modal will close and qdel itself. Set to zero for no timeout.
     * * ui_state - The TGUI UI state that will be returned in ui_state(). Default: always_state
     */
    // tg: code/modules/tgui_input/text.dm:18
    public static async DmTask<object> tgui_input_text(object user = null, object message = null, object title = null, object @default = null, object max_length = null, object multiline = null, object encode = null, object timeout = null, datum_ui_state ui_state = null)
    {
        message ??= "";
        title ??= "Text Input";
        multiline ??= FALSE;
        encode ??= TRUE;
        timeout ??= 0;
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

        // Client does NOT have tgui_input on: Returns regular input
        // if(!user.client.prefs.read_preference(/datum/preference/toggle/tgui_input)): GAP(lead), the preference's default (TRUE).
        datum_tgui_input_text text_input = Dm.@new<datum_tgui_input_text>(user__mob, message, title, @default, max_length, multiline, encode, timeout, ui_state);
        await text_input.ui_interact(user__mob);
        await text_input.wait();
        if (Dm.truthy(text_input))
        {
            dot = text_input.entry;
            qdel(text_input);
        }
        return dot;
    }
}

/**
 * tgui_input_text
 *
 * Datum used for instantiating and using a TGUI-controlled text input that prompts the user with
 * a message and has an input for text entry.
 */
// tg: code/modules/tgui_input/text.dm:58
public partial class datum_tgui_input_text : datum
{
    // tg: code/modules/tgui_input/text.dm:82
    [DmParams("user", "message", "title", "default", "max_length", "multiline", "encode", "timeout", "ui_state")]
    public override async DmTask New(params object[] __extra)
    {
        object message = Dm.arg(__extra, 1);
        object title = Dm.arg(__extra, 2);
        object @default = Dm.arg(__extra, 3);
        object max_length = Dm.arg(__extra, 4);
        object multiline = Dm.arg(__extra, 5);
        object encode = Dm.arg(__extra, 6);
        object timeout = Dm.arg(__extra, 7);
        object ui_state = Dm.arg(__extra, 8);
        this.@default = @default;
        this.encode = encode;
        this.max_length = max_length;
        this.message = message;
        this.multiline = multiline;
        this.title = title;
        this.state = ui_state as datum_ui_state;
        if (Dm.truthy(timeout))
        {
            this.timeout = timeout;
            start_time = world.time;
            // QDEL_IN(src, timeout) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), Dm.num(timeout), file: "code/modules/tgui_input/text.dm", line: 94);
        }
    }

    // tg: code/modules/tgui_input/text.dm:96
    public override object Destroy(float? force = null)
    {
        SStgui.close_uis(this);
        state = null;
        return base.Destroy(force);
    }

    /**
     * Waits for a user's response to the tgui_input_text's prompt before returning. Returns early if
     * the window was closed by the user.
     */
    // tg: code/modules/tgui_input/text.dm:105
    public virtual async DmTask wait()
    {
        while (!Dm.truthy(entry) && !Dm.truthy(closed) && !QDELETED(this))
            await stoplag(1);
    }

    // tg: code/modules/tgui_input/text.dm:109
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "TextInputModal");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/tgui_input/text.dm:115
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        closed = TRUE;
    }

    // tg: code/modules/tgui_input/text.dm:119
    public override object ui_state(mob user = null)
    {
        return state;
    }

    // tg: code/modules/tgui_input/text.dm:122
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["large_buttons"] = FALSE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_large): GAP, the default
        data["max_length"] = max_length;
        data["message"] = message;
        data["multiline"] = multiline;
        data["placeholder"] = @default; // Default is a reserved keyword
        data["swapped_buttons"] = TRUE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_swapped): GAP, the default
        data["title"] = title;
        return data;
    }

    // tg: code/modules/tgui_input/text.dm:133
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (Dm.truthy(timeout))
            data["timeout"] = Math.Clamp((Dm.num(timeout) - (world.time - Dm.num(start_time)) - 1 * SECONDS) / (Dm.num(timeout) - 1 * SECONDS), 0f, 1f); // CLAMP01 (__DEFINES/maths.dm), inline
        return data;
    }

    // tg: code/modules/tgui_input/text.dm:139
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "submit":
                if (Dm.truthy(max_length))
                {
                    if (length_char(@params["entry"]) > Dm.num(max_length))
                        throw Dm.CRASH(Dm.text("[] typed a text string longer than the max length", World.Current.usr), "code/modules/tgui_input/text.dm", 146);
                    if (Dm.truthy(encode) && length_char(html_encode(@params["entry"])) > Dm.num(max_length))
                        to_chat(World.Current.usr, span_notice(I18n.T("Your message was clipped due to special character usage.")));
                }
                set_entry(@params["entry"]);
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

    /**
     * Sets the return value for the tgui text proc.
     * If html encoding is enabled, the text will be encoded.
     * This can sometimes result in a string that is longer than the max length.
     * If the string is longer than the max length, it will be clipped.
     */
    // tg: code/modules/tgui_input/text.dm:160
    public virtual void set_entry(object entry = null)
    {
        if (entry != null)
        {
            object converted_entry = Dm.truthy(encode) ? html_encode(entry) : entry;
            this.entry = Dm.truthy(max_length) ? trim(converted_entry, Dm.num(max_length) + 1) : converted_entry; // PREVENT_CHARACTER_TRIM_LOSS (__DEFINES/text.dm:65), inline
        }
    }
}
