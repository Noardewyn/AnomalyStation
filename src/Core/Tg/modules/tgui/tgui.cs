// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/tgui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148, protocol version 16, DV44): the whole file. The window is the client's native window (tgui_window.cs);
// send_assets has nothing to send (the client draws tg's interfaces from its own kit and icons), the ByondUi elements are
// never opened, and the preferences the payload reads (layout, tgui_lock, ui_scale) are not translated (GAP: null). log_tgui
// (the tgui log) is deferred with the game log (M10). The var block is generated.

/**
 * tgui datum (represents a UI).
 */
// tg: code/modules/tgui/tgui.dm:9
[DmPath("/datum/tgui")]
public partial class datum_tgui : datum
{
    /**
     * public
     *
     * Create a new UI.
     *
     * required user mob The mob who opened/is using the UI.
     * required src_object datum The object or datum which owns the UI.
     * required interface string The interface used to render the UI.
     * optional title string The title of the UI.
     * optional ui_x int Deprecated: Window width.
     * optional ui_y int Deprecated: Window height.
     *
     * return datum/tgui The requested UI.
     */
    // tg: code/modules/tgui/tgui.dm:59
    [DmParams("user", "src_object", "interface", "title", "ui_x", "ui_y")]
    public override async DmTask New(params object[] __extra)
    {
        mob user = Dm.arg(__extra, 0) as mob;
        datum src_object = Dm.arg(__extra, 1) as datum;
        object @interface = Dm.arg(__extra, 2);
        object title = Dm.arg(__extra, 3);
        object ui_x = Dm.arg(__extra, 4);
        object ui_y = Dm.arg(__extra, 5);
        // log_tgui(user, "new [interface]", src_object = src_object): the tgui log (the game log, M10)
        this.user = user;
        this.src_object = src_object;
        this.window_key = Dm.text("[]-main", REF(src_object));
        this.@interface = @interface;
        if (Dm.truthy(title))
            this.title = title;
        this.state = src_object.ui_state(user) as datum_ui_state;
        // Deprecated
        if (Dm.truthy(ui_x) && Dm.truthy(ui_y))
            this.window_size = Dm.list(ui_x, ui_y);
    }

    // tg: code/modules/tgui/tgui.dm:76
    public override object Destroy(float? force = null)
    {
        user = null;
        src_object = null;
        return base.Destroy(force);
    }

    /**
     * public
     *
     * Open this UI (and initialize it with data).
     *
     * return bool - TRUE if a new pooled window is opened, FALSE in all other situations including if a new pooled window didn't open because one already exists.
     */
    // tg: code/modules/tgui/tgui.dm:88
    public virtual async DmTask<object> open()
    {
        if (!Dm.truthy(user.client))
            return FALSE;
        if (Dm.truthy(window))
            return FALSE;
        await process_status();
        if (status < UI_UPDATE)
            return FALSE;
        window = SStgui.request_pooled_window(user) as datum_tgui_window;
        if (!Dm.truthy(window))
            return FALSE;
        opened_at = world.time;
        window.acquire_lock(this);
        if (!Dm.truthy(window.is_ready()))
        {
            // assets = list(get_asset_datum(/datum/asset/simple/tgui)): DEVIATION(DV44), the client's kit is the tgui bundle.
            window.initialize(strict_mode: TRUE, assets: Dm.list());
        }
        else
        {
            window.send_message("ping");
            // DEVIATION(DV44): a suspended window's page shows itself again and sends "visible" (as initialize answers it for a new one),
            // which the map views wait for (map_view.dm display_to): without it a reused window never showed the character preview
            Waiting.NoSleep(window.on_message("visible"));
        }
        send_assets();
        window.send_message("update", await get_payload(
            with_data: TRUE,
            with_static_data: TRUE));
        SStgui.on_open(this);

        return TRUE;
    }

    // tg: code/modules/tgui/tgui.dm:114
    public virtual void send_assets()
    {
        // var/flush_queue = window.send_asset(get_asset_datum(/datum/asset/simple/namespaced/fontawesome)), tgfont,
        // icon_ref_map, src_object.ui_assets(user); if (flush_queue) user.client.browse_queue_flush()
        // DEVIATION(DV44): no assets go to a native window: the client has its own icon font and tg's icons.
    }

    /**
     * public
     *
     * Close the UI.
     *
     * optional can_be_suspended bool
     */
    // tg: code/modules/tgui/tgui.dm:133
    public virtual void close(float? can_be_suspended = null)
    {
        can_be_suspended ??= TRUE;
        if (Dm.truthy(closing))
            return;
        closing = TRUE;
        // If we don't have window_id, open proc did not have the opportunity
        // to finish, therefore it's safe to skip this whole block.
        if (Dm.truthy(window))
        {
            // Windows you want to keep are usually blue screens of death
            // and we want to keep them around, to allow user to read
            // the error message properly.
            window.release_lock();
            window.close(can_be_suspended);
            src_object.ui_close(user);
            SStgui.on_close(this);

            if (Dm.truthy(user.client))
                terminate_byondui_elements();
        }
        state = null;
        qdel(this);
    }

    /**
     * public
     *
     * Sends a message to the front end to push the UI window to position 0,0
     *
     * optional can_be_suspended bool
     */
    // tg: code/modules/tgui/tgui.dm:162
    public virtual void reset_ui_position()
    {
        if (Dm.truthy(window))
            window.send_message("resetposition");
    }

    /**
     * public
     *
     * Closes all ByondUI elements, left dangling by a forceful TGUI exit,
     * such as via Alt+F4, closing or terminating the process
     *
     */
    // tg: code/modules/tgui/tgui.dm:177
    public virtual void terminate_byondui_elements()
    {
        // set waitfor = FALSE; for(var/byondui_element in open_byondui_elements) winset(user.client, byondui_element, list("parent" = ""))
        // DEVIATION(DV44): a native window opens no ByondUi map elements (open_byondui_elements stays empty).
    }

    /**
     * public
     *
     * Enable/disable auto-updating of the UI.
     *
     * required value bool Enable/disable auto-updating.
     */
    // tg: code/modules/tgui/tgui.dm:190
    public virtual void set_autoupdate(object autoupdate = null)
    {
        this.autoupdate = Dm.num(autoupdate);
    }

    /**
     * public
     *
     * Replace current ui.state with a new one.
     *
     * required state datum/ui_state/state Next state
     */
    // tg: code/modules/tgui/tgui.dm:200
    public virtual void set_state(datum_ui_state state = null)
    {
        this.state = state;
    }

    /**
     * public
     *
     * Makes an asset available to use in tgui.
     *
     * required asset datum/asset
     *
     * return bool - true if an asset was actually sent
     */
    // tg: code/modules/tgui/tgui.dm:212
    public virtual object send_asset(datum asset = null)
    {
        if (!Dm.truthy(window))
            throw Dm.CRASH("send_asset() was called either without calling open() first or when open() did not return TRUE.", "code/modules/tgui/tgui.dm", 214);
        return window.send_asset(asset);
    }

    /**
     * public
     *
     * Send a full update to the client (includes static data).
     *
     * optional custom_data list Custom data to send instead of ui_data.
     * optional force bool Send an update even if UI is not interactive.
     * optional always_instant bool Send and update regardless of the cooldown.
     */
    // tg: code/modules/tgui/tgui.dm:226
    public virtual async DmTask send_full_update(object custom_data = null, object force = null, object always_instant = null)
    {
        if (!Dm.truthy(user.client) || !Dm.truthy(initialized) || Dm.truthy(closing))
            return;
        // COOLDOWN_FINISHED(src, refresh_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!Dm.truthy(always_instant) && !(refresh_cooldown <= world.time))
        {
            refreshing = TRUE;
            // COOLDOWN_TIMELEFT(src, refresh_cooldown) (__DEFINES/cooldowns.dm:117), inline
            float timeleft = Math.Max(0, refresh_cooldown - world.time);
            _addtimer(Dm.@new<datum_callback>(this, nameof(send_full_update), custom_data, force), timeleft, TIMER_UNIQUE, file: "code/modules/tgui/tgui.dm", line: 230);
            return;
        }
        refreshing = FALSE;
        bool should_update_data = Dm.truthy(force) || status >= UI_UPDATE;
        window.send_message("update", await get_payload(
            custom_data,
            with_data: should_update_data ? TRUE : FALSE,
            with_static_data: TRUE));
        // COOLDOWN_START(src, refresh_cooldown, TGUI_REFRESH_FULL_UPDATE_COOLDOWN) (__DEFINES/cooldowns.dm:110), inline
        if (!Dm.truthy(always_instant))
            refresh_cooldown = world.time + TGUI_REFRESH_FULL_UPDATE_COOLDOWN;
    }

    /**
     * public
     *
     * Send a partial update to the client (excludes static data).
     *
     * optional custom_data list Custom data to send instead of ui_data.
     * optional force bool Send an update even if UI is not interactive.
     */
    // tg: code/modules/tgui/tgui.dm:249
    public virtual async DmTask send_update(object custom_data = null, object force = null)
    {
        if (!Dm.truthy(user.client) || !Dm.truthy(initialized) || Dm.truthy(closing))
            return;
        bool should_update_data = Dm.truthy(force) || status >= UI_UPDATE;
        window.send_message("update", await get_payload(
            custom_data,
            with_data: should_update_data ? TRUE : FALSE));
    }

    /**
     * private
     *
     * Package the data to send to the UI, as JSON.
     *
     * return list
     */
    // tg: code/modules/tgui/tgui.dm:264
    public virtual async DmTask<object> get_payload(object custom_data = null, object with_data = null, object with_static_data = null)
    {
        DmList json_data = Dm.list();
        json_data["config"] = Dm.list(
            Dm.kv("title", title),
            Dm.kv("status", status),
            Dm.kv("interface", Dm.list(
                Dm.kv("name", @interface),
                // user.client.prefs.read_preference(src_object.layout_prefs_used): GAP(lead, modules/client/preferences.cs), the
                // preferences are not translated (the window's layout choice).
                Dm.kv("layout", null)
            )),
            Dm.kv("refreshing", refreshing),
            Dm.kv("window", Dm.list(
                Dm.kv("key", window_key),
                Dm.kv("size", window_size),
                // read_preference(/datum/preference/toggle/tgui_lock) and (/datum/preference/toggle/ui_scale): GAP, as above.
                Dm.kv("locked", null),
                Dm.kv("scale", null)
            )),
            Dm.kv("client", Dm.list(
                Dm.kv("ckey", user.client.ckey),
                Dm.kv("address", user.client.address),
                Dm.kv("computer_id", user.client.computer_id)
            )),
            Dm.kv("user", Dm.list(
                Dm.kv("name", Dm.text("[]", user)),
                Dm.kv("observer", isobserver(user) ? TRUE : FALSE)
            ))
        );
        object data = Dm.truthy(custom_data) ? custom_data : (Dm.truthy(with_data) ? await src_object.ui_data(user) : with_data);
        if (Dm.truthy(src_object._listen_lookup?[COMSIG_UI_DATA]))
            src_object._SendSignal(COMSIG_UI_DATA, Dm.list(src_object, user, data));
        if (Dm.truthy(data))
            json_data["data"] = data;
        object static_data = Dm.truthy(with_static_data) ? await src_object.ui_static_data(user) : with_static_data;
        if (Dm.truthy(static_data))
            json_data["static_data"] = static_data;
        if (Dm.truthy(src_object.tgui_shared_states))
            json_data["shared"] = src_object.tgui_shared_states;
        return json_data;
    }

    /**
     * private
     *
     * Run an update cycle for this UI. Called internally by SStgui
     * every second or so.
     */
    // tg: code/modules/tgui/tgui.dm:311
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object force = Dm.arg(__extra, 0) ?? FALSE;
        if (Dm.truthy(closing))
            return null;
        datum host = src_object.ui_host(user) as datum;
        // If the object or user died (or something else), abort.
        if (QDELETED(src_object) || QDELETED(host) || QDELETED(user) || QDELETED(window))
        {
            close(can_be_suspended: FALSE);
            return null;
        }
        // Validate ping
        if (!Dm.truthy(initialized) && Dm.num(world.time) - Dm.num(opened_at) > TGUI_PING_TIMEOUT)
        {
            // log_tgui(user, "Error: Zombie window detected, closing.", window = window, src_object = src_object): the tgui log (M10)
            close(can_be_suspended: FALSE);
            return null;
        }
        // Update through a normal call to ui_interact
        if (status != UI_DISABLED && (Dm.truthy(autoupdate) || Dm.truthy(force)))
        {
            Waiting.NoSleep(src_object.ui_interact(user, this));
            return null;
        }
        // Update status only
        object needs_update = Waiting.NoSleep(process_status());
        if (status <= UI_CLOSE)
        {
            close();
            return null;
        }
        if (Dm.truthy(needs_update))
            window.send_message("update", Waiting.NoSleep(get_payload()));
        return null;
    }

    /**
     * private
     *
     * Updates the status, and returns TRUE if status has changed.
     */
    // tg: code/modules/tgui/tgui.dm:343
    public virtual async DmTask<object> process_status()
    {
        float prev_status = status;
        status = Dm.num(await src_object.ui_status(user, state));
        return prev_status != status ? TRUE : FALSE;
    }

    /**
     * private
     *
     * Callback for handling incoming tgui messages.
     */
    // tg: code/modules/tgui/tgui.dm:353
    public virtual async DmTask<object> on_message(object type = null, DmList payload = null, DmList href_list = null)
    {
        // Pass act type messages to ui_act
        if (Dm.truthy(type) && Dm.eq(copytext(type, 1, 5), "act/"))
        {
            object act_type = copytext(type, 5);
            // log_tgui(user, "Action: [act_type] [href_list["payload"]]", window = window, src_object = src_object): the tgui log (M10)
            await process_status();
            // DEFAULT_QUEUE_OR_CALL_VERB(VERB_CALLBACK(src, PROC_REF(on_act_message), act_type, payload, state))
            // (__DEFINES/verb_manager.dm:32, :25-28): the macro builds its callback argument twice, once to queue and once to call.
            if (!Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(this, nameof(on_act_message), act_type, payload, state), VERB_DEFAULT_QUEUE_THRESHOLD, null)))
                Dm.@new<datum_callback_verb_callback>(this, nameof(on_act_message), act_type, payload, state).InvokeAsync();
            return FALSE;
        }
        switch (type)
        {
            case "ready":
                // Send a full update when the user manually refreshes the UI
                if (Dm.truthy(initialized))
                    await send_full_update();
                initialized = TRUE;
                break;
            case "ping/reply":
                initialized = TRUE;
                break;
            case "suspend":
                close(can_be_suspended: TRUE);
                break;
            case "close":
                close(can_be_suspended: FALSE);
                break;
            case "log":
                if (Dm.truthy(href_list?["fatal"]))
                    close(can_be_suspended: FALSE);
                break;
            case "setSharedState":
                if (status != UI_INTERACTIVE)
                    return null;
                // LAZYINITLIST(src_object.tgui_shared_states)
                src_object.tgui_shared_states ??= Dm.list();
                src_object.tgui_shared_states[href_list["key"]] = href_list["value"];
                SStgui.update_uis(src_object);
                break;
            case TGUI_MANAGED_BYONDUI_TYPE_RENDER:
            {
                object byond_ui_id = payload?[TGUI_MANAGED_BYONDUI_PAYLOAD_ID];
                if (!Dm.truthy(byond_ui_id) || Dm.length(open_byondui_elements) > TGUI_MANAGED_BYONDUI_LIMIT)
                    return null;
                // LAZYOR(open_byondui_elements, byond_ui_id)
                open_byondui_elements ??= Dm.list();
                Dm.combine(ref open_byondui_elements, byond_ui_id);
                break;
            }
            case TGUI_MANAGED_BYONDUI_TYPE_UNMOUNT:
            {
                object byond_ui_id = payload?[TGUI_MANAGED_BYONDUI_PAYLOAD_ID];
                if (!Dm.truthy(byond_ui_id))
                    return null;
                // LAZYREMOVE(open_byondui_elements, byond_ui_id)
                if (open_byondui_elements != null)
                {
                    open_byondui_elements.Remove(byond_ui_id);
                    if (open_byondui_elements.Count == 0)
                        open_byondui_elements = null;
                }
                break;
            }
        }
        return null;
    }

    /// Wrapper for behavior to potentially wait until the next tick if the server is overloaded
    // tg: code/modules/tgui/tgui.dm:394
    public virtual async DmTask on_act_message(object act_type = null, object payload = null, object state = null)
    {
        if (QDELETED(this) || QDELETED(src_object))
            return;
        if (Dm.truthy(await src_object.ui_act(act_type, payload as DmList, this, state as datum_ui_state)))
            SStgui.update_uis(src_object);
    }
}
