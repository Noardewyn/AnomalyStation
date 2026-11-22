// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/tgui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148, protocol version 16, DV44): the whole file. PreInit and OnConfigLoad build basehtml, the browser page
// with tgui's helpers and the storage CDN iframe; the native window has no page (DEVIATION(DV44): basehtml stays null).
// Shutdown (close_all_uis at world shutdown) and stat_entry (the MC stat panel) are not declared: the port's Master does not
// call Shutdown on subsystems yet and has no stat panel. The var block (name, wait 9, SS_NO_INIT, priority, runlevels,
// current_run, all_uis, basehtml) is generated.

// tg: code/controllers/subsystem/tgui.dm:14  SUBSYSTEM_DEF(tgui)
[DmPath("/datum/controller/subsystem/tgui")]
public partial class datum_controller_subsystem_tgui : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(tgui) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SStgui) (__DEFINES/MC.dm:29) expanded
    // inline (it reads src).
    // tg: code/controllers/subsystem/tgui.dm:14
    public override async DmTask New(params object[] __extra)
    {
        if (SStgui != this)
        {
            if (SStgui is datum_controller_subsystem_tgui)
            {
                Recover();
                qdel(SStgui);
            }
            SStgui = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/tgui.dm:27
    public override void PreInit()
    {
        // basehtml = file2text('tgui/public/tgui.html') with the inline helpers, the ntos-error styles and the copyright line.
        // DEVIATION(DV44): the native window has no page.
    }

    // tg: code/controllers/subsystem/tgui.dm:40
    public override async DmTask OnConfigLoad()
    {
        // The storage CDN iframe of basehtml (asset_transport, storage_cdn_iframe). DEVIATION(DV44): no page.
    }

    // tg: code/controllers/subsystem/tgui.dm:62
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.current_run = all_uis.Copy();
        // Cache for sanic speed (lists are references anyways)
        DmList current_run = this.current_run;
        while (current_run.len > 0)
        {
            datum_tgui ui = current_run[current_run.len] as datum_tgui;
            current_run.len--;
            // TODO: Move user/src_object check to process()
            if (Dm.truthy(ui?.user) && Dm.truthy(ui.src_object))
                ui.process(wait * 0.1f);
            else
                ui.close(0);
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    /**
     * public
     *
     * Requests a usable tgui window from the pool.
     * Returns null if pool was exhausted.
     *
     * required user mob
     * return datum/tgui
     */
    // tg: code/controllers/subsystem/tgui.dm:86
    public virtual object request_pooled_window(mob user = null)
    {
        if (!Dm.truthy(user.client))
            return null;
        DmList windows = user.client.tgui_windows;
        object window_id = null;
        // user.client.prefs?.read_preference(/datum/preference/toggle/tgui_unlimited_windows): GAP(lead,
        // modules/client/preferences.cs), the preferences are not translated: the preference's default (FALSE).
        float window_limit = TGUI_WINDOW_HARD_LIMIT;
        datum_tgui_window window = null;
        bool window_found = false;
        // Find a usable window
        for (float i = 1, i__end = window_limit; i <= i__end; i++)
        {
            window_id = datum_tgui_window.TGUI_WINDOW_ID(i);
            window = windows[window_id] as datum_tgui_window;
            // As we are looping, create missing window datums
            if (!Dm.truthy(window))
                window = Dm.@new<datum_tgui_window>(user.client, window_id, TRUE);
            // Skip windows with acquired locks
            if (Dm.truthy(window.locked))
                continue;
            if (window.status == TGUI_WINDOW_READY)
                return window;
            if (window.status == TGUI_WINDOW_CLOSED)
            {
                window.status = TGUI_WINDOW_LOADING;
                window_found = true;
                break;
            }
        }
        if (!window_found)
        {
            // log_tgui(user, "Error: Pool exhausted", context = "SStgui/request_pooled_window"): the tgui log (M10)
            return null;
        }
        return window;
    }

    /**
     * public
     *
     * Force closes all tgui windows.
     *
     * required user mob
     */
    // tg: code/controllers/subsystem/tgui.dm:125
    public virtual void force_close_all_windows(mob user = null)
    {
        // log_tgui(user, context = "SStgui/force_close_all_windows"): the tgui log (M10)
        if (Dm.truthy(user.client))
        {
            user.client.tgui_windows = Dm.list();
            float window_limit = TGUI_WINDOW_HARD_LIMIT; // read_preference(tgui_unlimited_windows): GAP, as above
            for (float i = 1, i__end = window_limit; i <= i__end; i++)
            {
                object window_id = datum_tgui_window.TGUI_WINDOW_ID(i);
                // user << browse(null, "window=[window_id]")
                user.client.dm_tgui_write(new TguiOutput(Dm.str(window_id), TguiOutputKind.Close, null)); // DEVIATION(DV44)
            }
        }
    }

    /**
     * public
     *
     * Force closes the tgui window by window_id.
     *
     * required user mob
     * required window_id string
     */
    // tg: code/controllers/subsystem/tgui.dm:143
    public virtual void force_close_window(mob user = null, object window_id = null)
    {
        // log_tgui(user, context = "SStgui/force_close_window"): the tgui log (M10)
        // Close all tgui datums based on window_id.
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(user.tgui_open_uis))
        {
            if (Dm.truthy(ui.window) && Dm.eq(ui.window.id, window_id))
                ui.close(can_be_suspended: FALSE);
        }
        // Close window directly just to be sure.
        // user << browse(null, "window=[window_id]")
        if (Dm.truthy(user.client))
            user.client.dm_tgui_write(new TguiOutput(Dm.str(window_id), TguiOutputKind.Close, null)); // DEVIATION(DV44)
    }

    /**
     * public
     *
     * Try to find an instance of a UI, and push an update to it.
     *
     * required user mob The mob who opened/is using the UI.
     * required src_object datum The object/datum which owns the UI.
     * optional ui datum/tgui The UI to be updated, if it exists.
     * optional force_open bool If the UI should be re-opened instead of updated.
     *
     * return datum/tgui The found UI.
     */
    // tg: code/controllers/subsystem/tgui.dm:165
    public virtual async DmTask<object> try_update_ui(mob user = null, datum src_object = null, datum_tgui ui = null)
    {
        // Look up a UI if it wasn't passed
        if (ui == null)
            ui = get_open_ui(user, src_object) as datum_tgui;
        // Couldn't find a UI.
        if (ui == null)
            return null;
        await ui.process_status();
        // UI ended up with the closed status
        // or is actively trying to close itself.
        // FIXME: Doesn't actually fix the paper bug.
        if (ui.status <= UI_CLOSE)
        {
            ui.close();
            return null;
        }
        await ui.send_update();
        return ui;
    }

    /**
     * public
     *
     * Get a open UI given a user and src_object.
     *
     * required user mob The mob who opened/is using the UI.
     * required src_object datum The object/datum which owns the UI.
     *
     * return datum/tgui The found UI.
     */
    // tg: code/controllers/subsystem/tgui.dm:193
    public virtual object get_open_ui(mob user = null, datum src_object = null)
    {
        // No UIs opened for this src_object
        if (!Dm.truthy(Dm.length(src_object?.open_uis)))
            return null;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(src_object.open_uis))
        {
            // Make sure we have the right user
            if (ui.user == user)
                return ui;
        }
        return null;
    }

    /**
     * public
     *
     * Update all UIs attached to src_object.
     *
     * required src_object datum The object/datum which owns the UIs.
     *
     * return int The number of UIs updated.
     */
    // tg: code/controllers/subsystem/tgui.dm:212
    public virtual object update_uis(datum src_object = null)
    {
        // No UIs opened for this src_object
        if (!Dm.truthy(Dm.length(src_object?.open_uis)))
            return 0;
        float count = 0;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(src_object.open_uis))
        {
            // Check if UI is valid.
            if (Dm.truthy(ui?.src_object) && Dm.truthy(ui.user) && Dm.truthy(ui.src_object.ui_host(ui.user)))
            {
                // INVOKE_ASYNC(ui, TYPE_PROC_REF(/datum/tgui, process), wait * 0.1, TRUE)
                datum_tgui ui__s = ui;
                Waiting.DetachSync(() => ui__s.process(wait * 0.1f, TRUE));
                count++;
            }
        }
        return count;
    }

    /**
     * public
     *
     * Close all UIs attached to src_object.
     *
     * required src_object datum The object/datum which owns the UIs.
     *
     * return int The number of UIs closed.
     */
    // tg: code/controllers/subsystem/tgui.dm:233
    public virtual object close_uis(datum src_object = null)
    {
        // No UIs opened for this src_object
        if (!Dm.truthy(Dm.length(src_object?.open_uis)))
            return 0;
        float count = 0;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(src_object.open_uis))
        {
            // Check if UI is valid.
            if (Dm.truthy(ui?.src_object) && Dm.truthy(ui.user) && Dm.truthy(ui.src_object.ui_host(ui.user)))
            {
                ui.close();
                count++;
            }
        }
        return count;
    }

    /**
     * public
     *
     * Close all UIs regardless of their attachment to src_object.
     *
     * return int The number of UIs closed.
     */
    // tg: code/controllers/subsystem/tgui.dm:251
    public virtual object close_all_uis()
    {
        float count = 0;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(all_uis))
        {
            // Check if UI is valid.
            if (Dm.truthy(ui?.src_object) && Dm.truthy(ui.user) && Dm.truthy(ui.src_object.ui_host(ui.user)))
            {
                ui.close();
                count++;
            }
        }
        return count;
    }

    /**
     * public
     *
     * Update all UIs belonging to a user.
     *
     * required user mob The mob who opened/is using the UI.
     * optional src_object datum If provided, only update UIs belonging this src_object.
     *
     * return int The number of UIs updated.
     */
    // tg: code/controllers/subsystem/tgui.dm:270
    public virtual object update_user_uis(mob user = null, datum src_object = null)
    {
        float count = 0;
        if (Dm.length(user?.tgui_open_uis) == 0)
            return count;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(user.tgui_open_uis))
        {
            if (src_object == null || ui.src_object == src_object)
            {
                ui.process(wait * 0.1f, 1);
                count++;
            }
        }
        return count;
    }

    /**
     * public
     *
     * Close all UIs belonging to a user.
     *
     * required user mob The mob who opened/is using the UI.
     * optional src_object datum If provided, only close UIs belonging this src_object.
     *
     * return int The number of UIs closed.
     */
    // tg: code/controllers/subsystem/tgui.dm:291
    public virtual object close_user_uis(mob user = null, datum src_object = null)
    {
        float count = 0;
        if (Dm.length(user?.tgui_open_uis) == 0)
            return count;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(user.tgui_open_uis))
        {
            if (src_object == null || ui.src_object == src_object)
            {
                ui.close();
                count++;
            }
        }
        return count;
    }

    /**
     * public
     *
     * Resets the position of all UIs belonging to a user.
     *
     * required user mob The mob who opened/is using the UI.
     * optional src_object datum If provided, only reset UIs belonging this src_object.
     *
     * return int The number of UIs reset.
     */
    // tg: code/controllers/subsystem/tgui.dm:312
    public virtual object reset_ui_position(mob user = null, datum src_object = null)
    {
        float count = 0;
        if (Dm.length(user?.tgui_open_uis) == 0)
            return count;
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(user.tgui_open_uis))
        {
            if (src_object == null || ui.src_object == src_object)
            {
                ui.reset_ui_position();
                count++;
            }
        }
        return count;
    }

    /**
     * private
     *
     * Add a UI to the list of open UIs.
     *
     * required ui datum/tgui The UI to be added.
     */
    // tg: code/controllers/subsystem/tgui.dm:330
    public virtual void on_open(datum_tgui ui = null)
    {
        if (ui.user != null)
            Dm.combine(ref ui.user.tgui_open_uis, ui);
        // LAZYOR(ui.src_object.open_uis, ui)
        ui.src_object.open_uis ??= Dm.list();
        Dm.combine(ref ui.src_object.open_uis, ui);
        Dm.combine(ref all_uis, ui);
    }

    /**
     * private
     *
     * Remove a UI from the list of open UIs.
     *
     * required ui datum/tgui The UI to be removed.
     *
     * return bool If the UI was removed or not.
     */
    // tg: code/controllers/subsystem/tgui.dm:343
    public virtual object on_close(datum_tgui ui = null)
    {
        // Remove it from the list of processing UIs.
        Dm.remove(ref all_uis, ui);
        Dm.remove(ref current_run, ui);
        // If the user exists, remove it from them too.
        if (Dm.truthy(ui.user))
            Dm.remove(ref ui.user.tgui_open_uis, ui);
        if (Dm.truthy(ui.src_object))
        {
            // LAZYREMOVE(ui.src_object.open_uis, ui)
            if (ui.src_object.open_uis != null)
            {
                Dm.remove(ref ui.src_object.open_uis, ui);
                if (ui.src_object.open_uis.Count == 0)
                    ui.src_object.open_uis = null;
            }
        }
        return TRUE;
    }

    /**
     * private
     *
     * Handle client logout, by closing all their UIs.
     *
     * required user mob The mob which logged out.
     */
    // tg: code/controllers/subsystem/tgui.dm:361
    public virtual void on_logout(mob user = null)
    {
        close_user_uis(user);
    }

    /**
     * private
     *
     * Handle clients switching mobs, by transferring their UIs.
     *
     * required user source The client's original mob.
     * required user target The client's new mob.
     *
     * return bool If the UIs were transferred.
     */
    // tg: code/controllers/subsystem/tgui.dm:374
    public virtual object on_transfer(mob source = null, mob target = null)
    {
        // The old mob had no open UIs.
        if (Dm.length(source?.tgui_open_uis) == 0)
            return FALSE;
        if (target.tgui_open_uis == null || !istype(target.tgui_open_uis, typeof(DmList)))
            target.tgui_open_uis = Dm.list();
        // Transfer all the UIs.
        foreach (datum_tgui ui in Dm.each_cast<datum_tgui>(source.tgui_open_uis))
        {
            // Inform the UIs of their new owner.
            ui.user = target;
            Dm.append(ref target.tgui_open_uis, ui);
        }
        // Clear the old list.
        source.tgui_open_uis.Cut();
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/tgui.dm:14  SUBSYSTEM_DEF(tgui): GLOBAL_REAL(SStgui, /datum/controller/subsystem/tgui)
    internal datum_controller_subsystem_tgui SStgui;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_tgui SStgui => ref TgState.Current.SStgui;
}
