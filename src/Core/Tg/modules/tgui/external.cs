// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/external.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148, protocol version 16, DV44): the whole file. The src_object API of tgui (ui_interact, ui_data, ui_static_data,
// the update procs, ui_act, ui_assets, ui_host, ui_state, ui_close), the uiclose verb and tgui_Topic. tgui_Topic decodes the
// browser's href; the native window's messages arrive decoded (ClientSeam.TguiMessage) and enter at the same point, the
// window's on_message. The vars tgui_open_uis (mob) and tgui_windows (client) are generated; their list() is set in the
// hand-written dm_init_vars of mob (mob_defines.cs) and client (client_defines.cs).

public partial class datum
{
    /**
     * public
     *
     * Used to open and update UIs.
     * If this proc is not implemented properly, the UI will not update correctly.
     *
     * required user mob The mob who opened/is using the UI.
     * optional ui datum/tgui The UI to be updated, if it exists.
     */
    // tg: code/modules/tgui/external.dm:17
    public virtual async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        return FALSE; // Not implemented.
    }

    /**
     * public
     *
     * Data to be sent to the UI.
     * This must be implemented for a UI to work.
     *
     * required user mob The mob interacting with the UI.
     *
     * return list Data to be sent to the UI.
     */
    // tg: code/modules/tgui/external.dm:30
    public virtual async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(); // Not implemented.
    }

    /**
     * public
     *
     * Static Data to be sent to the UI.
     *
     * Static data differs from normal data in that it's large data that should be
     * sent infrequently. This is implemented optionally for heavy uis that would
     * be sending a lot of redundant data frequently. Gets squished into one
     * object on the frontend side, but the static part is cached.
     *
     * required user mob The mob interacting with the UI.
     *
     * return list Static Data to be sent to the UI.
     */
    // tg: code/modules/tgui/external.dm:47
    public virtual async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list();
    }

    /**
     * public
     *
     * Forces an update on static data. Should be done manually whenever something
     * happens to change static data.
     *
     * required user the mob currently interacting with the ui
     * optional ui ui to be updated
     * always_instant when set to true stops the ui update cooldown from happening
     */
    // tg: code/modules/tgui/external.dm:61
    public virtual async DmTask update_static_data(mob user = null, datum_tgui ui = null, object always_instant = null)
    {
        if (!Dm.truthy(ui))
            ui = SStgui.get_open_ui(user, this) as datum_tgui;
        if (Dm.truthy(ui))
            await ui.send_full_update(always_instant: always_instant);
    }

    /**
     * public
     *
     * Will force an update on static data for all viewers.
     * Should be done manually whenever something happens to
     * change static data.
     */
    // tg: code/modules/tgui/external.dm:73
    public virtual async DmTask update_static_data_for_all_viewers()
    {
        foreach (datum_tgui window in Dm.each_cast<datum_tgui>(open_uis))
            await window.send_full_update();
    }

    /**
     * public
     *
     * Will force an update on non-static data for all viewers.
     * Use when you are manually controlling UI data updates,
     * such as when you are not using the auto-update system.
     */
    // tg: code/modules/tgui/external.dm:83
    public virtual async DmTask update_data_for_all_viewers()
    {
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(open_uis))
            await ui.send_update();
    }

    /**
     * public
     *
     * Called on a UI when the UI receieves a href.
     * Think of this as Topic().
     *
     * required action string The action/button that has been invoked by the user.
     * required params list A list of parameters attached to the button.
     *
     * return bool If the user's input has been handled and the UI should update.
     */
    // tg: code/modules/tgui/external.dm:99
    public virtual async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_UI_ACT]))
            this._SendSignal(COMSIG_UI_ACT, Dm.list(this, World.Current.usr, action, @params));
        // If UI is not interactive or usr calling Topic is not the UI user, bail.
        if (!Dm.truthy(ui) || ui.status != UI_INTERACTIVE)
            return TRUE;
        if (Dm.eq(action, "change_ui_state"))
        {
            // var/mob/living/user = ui.user
            // user.client.prefs.write_preference(GLOB.preference_entries[layout_prefs_used], params["new_state"])
            // GAP(lead, modules/client/preferences.cs): the preferences are not translated; a window's layout choice is not kept.
            Dm.Deferred("code/modules/tgui/external.dm:107", "ui_act: change_ui_state writes the tgui layout preference (the preferences, M10+)");
        }
        return null;
    }

    /**
     * public
     *
     * Called on an object when a tgui object is being created, allowing you to
     * push various assets to tgui, for examples spritesheets.
     *
     * return list List of asset datums or file paths.
     */
    // tg: code/modules/tgui/external.dm:118
    public virtual async DmTask<object> ui_assets(mob user = null)
    {
        return Dm.list();
    }

    /**
     * private
     *
     * The UI's host object (usually src_object).
     * This allows modules/datums to have the UI attached to them,
     * and be a part of another object.
     */
    // tg: code/modules/tgui/external.dm:128
    public virtual object ui_host(mob user = null)
    {
        return this; // Default src.
    }

    /**
     * private
     *
     * The UI's state controller to be used for created uis
     * This is a proc over a var for memory reasons
     */
    // tg: code/modules/tgui/external.dm:137
    public virtual object ui_state(mob user = null)
    {
        return GLOB.default_state;
    }

    /**
     * public
     *
     * Called on a UI's object when the UI is closed, not to be confused with
     * client/verb/uiclose(), which closes the ui window
     */
    // tg: code/modules/tgui/external.dm:175
    [SignalHandler]
    public virtual void ui_close(mob user = null)
    {
    }
}

public partial class client
{
    /**
     * verb
     *
     * Called by UIs when they are closed.
     * Must be a verb so winset() can call it.
     *
     * required uiref ref The UI that was closed.
     */
    // tg: code/modules/tgui/external.dm:186  GAME_VERB_NATIVE(/client, uiclose, "uiclose", null, window_id as text)
    public virtual void uiclose(object window_id = null, params object[] __extra)
    {
        mob user = this?.mob;
        if (!Dm.truthy(user))
            return;
        // Close all tgui datums based on window_id.
        SStgui.force_close_window(user, window_id);
    }
}

public static partial class GlobalProcs
{
    /**
     * Middleware for /client/Topic.
     *
     * return bool If TRUE, prevents propagation of the topic call.
     */
    // tg: code/modules/tgui/external.dm:201
    // DEVIATION(DV44): tgui_Topic decodes the browser's href (href_list["tgui"], ["type"], ["window_id"], the payload's JSON). The
    // native window sends tg's message type, the window's id and the payload already decoded; ClientSeam.TguiMessage locates the
    // window as this proc does (:226-235) and passes the message to the window's on_message (:243). The log, cacheReloaded and
    // invalid-JSON branches have no native counterpart.
}
