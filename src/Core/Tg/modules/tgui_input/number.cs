// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/number.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file. The proc waits like show_radial_menu (DV40's pattern): the window is a tgui window
// (NumberInputModal, protocol 16) and the client's submit or cancel ends the wait. The preferences it reads (tgui_input,
// tgui_input_large, tgui_input_swapped) are not translated (GAP(lead, modules/client/preferences.cs)): their defaults
// (TRUE, FALSE, TRUE). The var block is generated.

public static partial class GlobalProcs
{
    /**
     * Creates a TGUI window with a number input. Returns the user's response as num | null.
     *
     * This proc should be used to create windows for number entry that the caller will wait for a response from.
     * If tgui fancy chat is turned off: Will return a normal input. If a max or min value is specified, will
     * validate the input inside the UI and ui_act.
     *
     * Arguments:
     * * user - The user to show the number input to.
     * * message - The content of the number input, shown in the body of the TGUI window.
     * * title - The title of the number input modal, shown on the top of the TGUI window.
     * * default - The default (or current) value, shown as a placeholder. Users can press refresh with this.
     * * max_value - Specifies a maximum value. If none is set, any number can be entered. Pressing "max" defaults to 1000.
     * * min_value - Specifies a minimum value. Often 0.
     * * timeout - The timeout of the number input, after which the modal will close and qdel itself. Set to zero for no timeout.
     * * round_value - whether the inputted number is rounded down into an integer.
     * * ui_state - The TGUI UI state that will be returned in ui_state(). Default: always_state
     */
    // tg: code/modules/tgui_input/number.dm:18
    public static async DmTask<object> tgui_input_number(object user = null, object message = null, object title = null, object @default = null, object max_value = null, object min_value = null, object timeout = null, object round_value = null, datum_ui_state ui_state = null)
    {
        title ??= "Number Input";
        @default ??= 0;
        max_value ??= 10000;
        min_value ??= 0;
        timeout ??= 0;
        round_value ??= TRUE;
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
        datum_tgui_input_number number_input = Dm.@new<datum_tgui_input_number>(user__mob, message, title, @default, max_value, min_value, timeout, round_value, ui_state);
        await number_input.ui_interact(user__mob);
        await number_input.wait();
        if (Dm.truthy(number_input))
        {
            dot = number_input.entry;
            qdel(number_input);
        }
        return dot;
    }
}

/**
 * # tgui_input_number
 *
 * Datum used for instantiating and using a TGUI-controlled number input that prompts the user with
 * a message and has an input for number entry.
 */
// tg: code/modules/tgui_input/number.dm:48
[DmPath("/datum/tgui_input_number")]
public partial class datum_tgui_input_number : datum
{
    // tg: code/modules/tgui_input/number.dm:72
    [DmParams("user", "message", "title", "default", "max_value", "min_value", "timeout", "round_value", "ui_state")]
    public override async DmTask New(params object[] __extra)
    {
        object message = Dm.arg(__extra, 1);
        object title = Dm.arg(__extra, 2);
        object @default = Dm.arg(__extra, 3);
        object max_value = Dm.arg(__extra, 4);
        object min_value = Dm.arg(__extra, 5);
        object timeout = Dm.arg(__extra, 6);
        object round_value = Dm.arg(__extra, 7);
        object ui_state = Dm.arg(__extra, 8);
        this.@default = @default;
        this.max_value = max_value;
        this.message = message;
        this.min_value = min_value;
        this.title = title;
        this.round_value = round_value;
        this.state = ui_state as datum_ui_state;
        if (Dm.truthy(timeout))
        {
            this.timeout = timeout;
            start_time = world.time;
            // QDEL_IN(src, timeout) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), Dm.num(timeout), file: "code/modules/tgui_input/number.dm", line: 84);
        }
        /// Checks for empty numbers - bank accounts, etc.
        if (Dm.eq(max_value, 0))
        {
            this.min_value = 0;
            if (Dm.truthy(@default))
                this.@default = 0;
        }
        /// Sanity check
        if (Dm.num(@default) < Dm.num(min_value))
            this.@default = min_value;
        if (Dm.num(@default) > Dm.num(max_value))
            throw Dm.CRASH("Default value is greater than max value.", "code/modules/tgui_input/number.dm", 93);
    }

    // tg: code/modules/tgui_input/number.dm:95
    public override object Destroy(float? force = null)
    {
        SStgui.close_uis(this);
        state = null;
        return base.Destroy(force);
    }

    /**
     * Waits for a user's response to the tgui_input_number's prompt before returning. Returns early if
     * the window was closed by the user.
     */
    // tg: code/modules/tgui_input/number.dm:104
    public virtual async DmTask wait()
    {
        while (!Dm.truthy(entry) && !Dm.truthy(closed) && !QDELETED(this))
            await stoplag(1);
    }

    // tg: code/modules/tgui_input/number.dm:108
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "NumberInputModal");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/tgui_input/number.dm:114
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        closed = TRUE;
    }

    // tg: code/modules/tgui_input/number.dm:118
    public override object ui_state(mob user = null)
    {
        return state;
    }

    // tg: code/modules/tgui_input/number.dm:121
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["init_value"] = @default; // Default is a reserved keyword
        data["large_buttons"] = FALSE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_large): GAP, the default
        data["max_value"] = max_value;
        data["message"] = message;
        data["min_value"] = min_value;
        data["swapped_buttons"] = TRUE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_swapped): GAP, the default
        data["title"] = title;
        data["round_value"] = round_value;
        return data;
    }

    // tg: code/modules/tgui_input/number.dm:133
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (Dm.truthy(timeout))
            data["timeout"] = Math.Clamp((Dm.num(timeout) - (world.time - Dm.num(start_time)) - 1 * SECONDS) / (Dm.num(timeout) - 1 * SECONDS), 0f, 1f); // CLAMP01 (__DEFINES/maths.dm), inline
        return data;
    }

    // tg: code/modules/tgui_input/number.dm:139
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "submit":
            {
                if (!isnum(@params["entry"]))
                    throw Dm.CRASH(Dm.text("A non number was input into tgui input number by []", World.Current.usr), "code/modules/tgui_input/number.dm", 146);
                object choice = Dm.truthy(round_value) ? Dm.round(Dm.num(@params["entry"])) : @params["entry"];
                if (Dm.num(choice) > Dm.num(max_value))
                    throw Dm.CRASH(Dm.text("A number greater than the max value was input into tgui input number by []", World.Current.usr), "code/modules/tgui_input/number.dm", 149);
                if (Dm.num(choice) < Dm.num(min_value))
                    throw Dm.CRASH(Dm.text("A number less than the min value was input into tgui input number by []", World.Current.usr), "code/modules/tgui_input/number.dm", 151);
                set_entry(choice);
                closed = TRUE;
                SStgui.close_uis(this);
                return TRUE;
            }
            case "cancel":
                closed = TRUE;
                SStgui.close_uis(this);
                return TRUE;
        }
        return dot;
    }

    /**
     * Sets the return value for the tgui_input_number.
     * Arguments:
     * * entry - The value to return.
     */
    // tg: code/modules/tgui_input/number.dm:161
    public virtual void set_entry(object entry = null)
    {
        this.entry = entry;
    }
}
