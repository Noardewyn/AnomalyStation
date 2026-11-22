// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/list.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 4 (2026-10-16; the grenade's multitool fuse list, then the M5 leftovers' blast door controller and canister
// relabel): the whole file. The proc waits like tgui_input_number (number.cs): the window is a tgui window (ListInputWindow,
// protocol 16) and the client's submit or cancel ends the wait. The preferences it reads (tgui_input, tgui_input_large,
// tgui_input_swapped) are not translated (GAP(lead, modules/client/preferences.cs)): their defaults (TRUE, FALSE, TRUE).
// `choice` is declared here as tg's untyped var (the generator's V3 grep took it for a typepath var: tgui_input_list returns
// any listed value, a mob, a text or a path); the rest of the var block is generated (D111, the Hand mode's complement).

public static partial class GlobalProcs
{
    /**
     * Creates a TGUI input list window and returns the user's response.
     *
     * This proc should be used to create alerts that the caller will wait for a response from.
     * Arguments:
     * * user - The user to show the input box to.
     * * message - The content of the input box, shown in the body of the TGUI window.
     * * title - The title of the input box, shown on the top of the TGUI window.
     * * items - The options that can be chosen by the user, each string is assigned a button on the UI.
     * * default - If an option is already preselected on the UI. Current values, etc.
     * * timeout - The timeout of the input box, after which the menu will close and qdel itself. Set to zero for no timeout.
     */
    // tg: code/modules/tgui_input/list.dm:12
    public static async DmTask<object> tgui_input_list(object user = null, object message = null, object title = null, DmList items = null, object @default = null, object timeout = null, datum_ui_state ui_state = null)
    {
        title ??= "Select";
        timeout ??= 0;
        ui_state ??= GLOB.always_state;
        object dot = null;
        if (!Dm.truthy(user))
            user = World.Current.usr;
        if (!Dm.truthy(length(items)))
            return null;
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

        /// Client does NOT have tgui_input on: Returns regular input
        // if(!user.client.prefs.read_preference(/datum/preference/toggle/tgui_input)): GAP(lead), the preference's default (TRUE).
        datum_tgui_list_input input = Dm.@new<datum_tgui_list_input>(user__mob, message, title, items, @default, timeout, ui_state);
        if (Dm.truthy(input.invalid))
        {
            qdel(input);
            return null;
        }
        await input.ui_interact(user__mob);
        await input.wait();
        if (Dm.truthy(input))
        {
            dot = input.choice;
            qdel(input);
        }
        return dot;
    }
}

/**
 * # tgui_list_input
 *
 * Datum used for instantiating and using a TGUI-controlled list input that prompts the user with
 * a message and shows a list of selectable options
 */
// tg: code/modules/tgui_input/list.dm:47
[DmPath("/datum/tgui_list_input")]
public partial class datum_tgui_list_input : datum
{
    /// The button that the user has pressed, null if no selection has been made
    // tg: code/modules/tgui_input/list.dm:57
    public object choice;

    // tg: code/modules/tgui_input/list.dm:71  (var/static/regex/whitelistedWords: one instance for the type, made at first use)
    private static readonly System.Text.RegularExpressions.Regex whitelistedWords = new System.Text.RegularExpressions.Regex("([^ -耀]+)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    // tg: code/modules/tgui_input/list.dm:71
    [DmParams("user", "message", "title", "items", "default", "timeout", "ui_state")]
    public override async DmTask New(params object[] __extra)
    {
        object message = Dm.arg(__extra, 1);
        object title = Dm.arg(__extra, 2);
        DmList items = Dm.arg<DmList>(__extra, 3);
        object @default = Dm.arg(__extra, 4);
        object timeout = Dm.arg(__extra, 5);
        object ui_state = Dm.arg(__extra, 6);
        this.title = title;
        this.message = message;
        this.items = Dm.list();
        this.items_map = Dm.list();
        this.@default = @default;
        this.state = ui_state as datum_ui_state;
        DmList repeat_items = Dm.list();
        // Gets rid of illegal characters
        foreach (object i in Dm.each<object>(items))
        {
            if (!Dm.truthy(i))
                continue;
            object string_key = whitelistedWords.Replace(Dm.text("[]", i), "");
            //avoids duplicated keys E.g: when areas have the same name
            string_key = avoid_assoc_duplicate_keys(string_key, repeat_items);
            Dm.append(this.items, string_key);
            this.items_map[string_key] = i;
        }

        if (length(this.items) == 0)
            invalid = TRUE;
        if (Dm.truthy(timeout))
        {
            this.timeout = timeout;
            start_time = world.time;
            // QDEL_IN(src, timeout) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), Dm.num(timeout), file: "code/modules/tgui_input/list.dm", line: 92);
        }
    }

    // tg: code/modules/tgui_input/list.dm:94
    public override object Destroy(float? force = null)
    {
        SStgui.close_uis(this);
        state = null;
        items?.Cut();
        items_map?.Cut();
        return base.Destroy(force);
    }

    /**
     * Waits for a user's response to the tgui_list_input's prompt before returning. Returns early if
     * the window was closed by the user.
     */
    // tg: code/modules/tgui_input/list.dm:105
    public virtual async DmTask wait()
    {
        // (QDELETED(src): a timed-out window's qdel ends the wait, as tg's deleted datum ends its loop's src)
        while (!Dm.truthy(choice) && !Dm.truthy(closed) && !QDELETED(this))
            await stoplag(1);
    }

    // tg: code/modules/tgui_input/list.dm:109
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ListInputWindow");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/tgui_input/list.dm:115
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        closed = TRUE;
    }

    // tg: code/modules/tgui_input/list.dm:119
    public override object ui_state(mob user = null)
    {
        return state;
    }

    // tg: code/modules/tgui_input/list.dm:122
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["init_value"] = Dm.truthy(@default) ? @default : items[1];
        data["items"] = items;
        data["large_buttons"] = FALSE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_large): GAP, the default
        data["message"] = message;
        data["swapped_buttons"] = TRUE; // user.client.prefs.read_preference(/datum/preference/toggle/tgui_input_swapped): GAP, the default
        data["title"] = title;
        return data;
    }

    // tg: code/modules/tgui_input/list.dm:132
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (Dm.truthy(timeout))
            data["timeout"] = Math.Clamp((Dm.num(timeout) - (world.time - Dm.num(start_time)) - 1 * SECONDS) / (Dm.num(timeout) - 1 * SECONDS), 0f, 1f); // CLAMP01 (__DEFINES/maths.dm), inline
        return data;
    }

    // tg: code/modules/tgui_input/list.dm:138
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "submit":
                if (!Dm.@in(@params["entry"], items))
                    return dot;
                set_choice(items_map[@params["entry"]]);
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

    // tg: code/modules/tgui_input/list.dm:157
    public virtual void set_choice(object choice = null)
    {
        this.choice = choice;
    }
}
