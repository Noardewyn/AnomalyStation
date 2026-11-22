// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/tgui_window.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148, protocol version 16, DV44): the window datum, translated with its BYOND output replaced by the native
// window's. tg's window is a browser control in the player's BYOND client: initialize writes tgui.html into it with
// browse(), send_message writes a JSON message into it with output(), close removes it with browse(null), and the page's
// JavaScript answers with Topic calls (tgui_Topic -> on_message). Here the client draws the window itself (D148):
// - browse(html) and browse(null) and output(message) become records in client.dm_tgui_output (TguiOutput), which the
//   replicator sends to the player's client as protocol 16 UiWindow messages (the message's JSON, as TGUI_CREATE_MESSAGE
//   json_encodes it, without the url_encode of the browser transport);
// - the page's "ready" after loading and its "ping/reply" to a ping are answered by the window itself at once (the native
//   window is ready as soon as it is made: there is no page to load);
// - assets (spritesheets, fonts) are not sent: the client has tg's icons and its own icon font;
// - the oversized payload chunks, openLink, chat/resend and the cache reload have no native counterpart (an act's payload
//   fits one Action message).
// The pool, the locks, the status, the message queue and the subscriber are tg's.

/// <summary>DV44: one thing tg's window writes into the player's BYOND client (browse or output), for the replicator to send.</summary>
/// <param name="WindowId">The window's id (tg's <c>TGUI_WINDOW_ID</c>: <c>"tgui-window-N"</c>).</param>
/// <param name="Kind">What it is.</param>
/// <param name="Json">For a message: tg's message as JSON (<c>{"type": ..., "payload": ...}</c>); else null.</param>
public sealed record TguiOutput(string WindowId, TguiOutputKind Kind, string Json);

/// <summary>DV44: the kinds of <see cref="TguiOutput"/>.</summary>
public enum TguiOutputKind : byte
{
    /// <summary><c>client &lt;&lt; browse(html, "window=[id]")</c>: the window is (re)made, empty.</summary>
    Initialize = 0,

    /// <summary><c>client &lt;&lt; output(message, "[id].browser:update")</c>: a message for the window's page.</summary>
    Message = 1,

    /// <summary><c>client &lt;&lt; browse(null, "window=[id]")</c>: the window is closed.</summary>
    Close = 2,

    /// <summary>
    /// M10x slice G6: tg's other browse windows, <c>client &lt;&lt; browse(html, "window=[name];size=WxH")</c> (a ticket panel, the
    /// ticket lists): <see cref="TguiOutput.WindowId"/> is the window's name and the JSON is <c>{"html", "title", "width", "height"}</c>
    /// (<see cref="ClientSeam.Browse"/>); <c>browse(null, "window=[name]")</c> is a <see cref="Close"/> with that name.
    /// </summary>
    Browse = 3,
}

public partial class client
{
    /// <summary>
    /// DV44: what tg's tgui windows wrote into this client since the replicator last took it, oldest first (BYOND's browse and
    /// output to the window's browser control). Presentation output, like the sound and message sinks: not world state.
    /// </summary>
    [NotHashed]
    public readonly List<TguiOutput> dm_tgui_output = new();

    /// <summary>The most records <see cref="dm_tgui_output"/> keeps for a client nothing takes them from (the oldest go).</summary>
    public const int DmTguiOutputCap = 512;

    internal void dm_tgui_write(TguiOutput output)
    {
        if (dm_tgui_output.Count >= DmTguiOutputCap)
            dm_tgui_output.RemoveAt(0);
        dm_tgui_output.Add(output);
    }
}

// tg: code/modules/tgui/tgui_window.dm:6
[DmPath("/datum/tgui_window")]
public partial class datum_tgui_window : datum
{
    // GAP(types): the generated var block types id as a typepath; it is the window's id text ("tgui-window-N").
    // tg: code/modules/tgui/tgui_window.dm:7
    public object id;

    // tg: __DEFINES/tgui.dm  #define TGUI_WINDOW_ID(index) "tgui-window-[index]"
    public static string TGUI_WINDOW_ID(object index) => Dm.text("tgui-window-[]", index);

    // tg: __DEFINES/tgui.dm  #define TGUI_WINDOW_INDEX(window_id) text2num(copytext(window_id, 13))
    public static float? TGUI_WINDOW_INDEX(object window_id) => text2num(copytext(window_id, 13));

    /**
     * public
     *
     * Create a new tgui window.
     *
     * required client /client
     * required id string A unique window identifier.
     */
    // tg: code/modules/tgui/tgui_window.dm:38
    [DmParams("client", "id", "pooled")]
    public override async DmTask New(params object[] __extra)
    {
        client client = Dm.arg(__extra, 0) as client;
        object id = Dm.arg(__extra, 1);
        object pooled = Dm.arg(__extra, 2) ?? FALSE;
        await base.New(__extra);
        this.id = id;
        this.client = client;
        this.client.tgui_windows[id] = this;
        this.pooled = pooled;
        if (Dm.truthy(pooled))
            this.pool_index = TGUI_WINDOW_INDEX(id);
    }

    /**
     * public
     *
     * Initializes the window with a fresh page. Puts window into the "loading"
     * state. You can begin sending messages right after initializing. Messages
     * will be put into the queue until the window finishes loading.
     *
     * optional strict_mode bool - Enables strict error handling and BSOD.
     * optional assets list - List of assets to load during initialization.
     * optional inline_html string - Custom HTML to inject.
     * optional inline_js string - Custom JS to inject.
     * optional inline_css string - Custom CSS to inject.
     */
    // tg: code/modules/tgui/tgui_window.dm:60
    public virtual void initialize(object strict_mode = null, object assets = null, object inline_html = null, object inline_js = null, object inline_css = null)
    {
        strict_mode ??= FALSE;
        assets ??= Dm.list();
        inline_html ??= "";
        inline_js ??= "";
        inline_css ??= "";
        // log_tgui(client, context = "[id]/initialize", window = src): the tgui log (M10)
        if (!Dm.truthy(client))
            return;
        this.initial_assets = assets;
        this.initial_inline_html = inline_html;
        this.initial_inline_js = inline_js;
        this.initial_inline_css = inline_css;
        status = TGUI_WINDOW_LOADING;
        fatally_errored = FALSE;
        // :79-114: the page (SStgui.basehtml with the window id, strict mode, the assets' urls, the inline html/js/css),
        // client << browse(html, "window=[id];[options]"), is_browser = winexists(client, id) == "BROWSER", the on-close winset.
        // DEVIATION(DV44): the native window is made by the client; is_browser stays FALSE and its close button sends "close".
        client.dm_tgui_write(new TguiOutput(Dm.str(id), TguiOutputKind.Initialize, null));
        // DEVIATION(DV44): the page's "ready" (sent by tgui once it has loaded) is answered here, at once.
        Waiting.NoSleep(on_message("ready"));
        // DEVIATION(DV44): and its "visible" (sent once the window shows), which the map views wait for (map_view.dm display_to; M10 slice D).
        Waiting.NoSleep(on_message("visible"));
    }

    /**
     * public
     *
     * Reinitializes the panel with previous data used for initialization.
     */
    // tg: code/modules/tgui/tgui_window.dm:121
    public virtual void reinitialize()
    {
        initialize(
            strict_mode: initial_strict_mode,
            assets: initial_assets,
            inline_html: initial_inline_html,
            inline_js: initial_inline_js,
            inline_css: initial_inline_css);
        // Resend assets
        foreach (object asset in Dm.each(sent_assets))
            send_asset(asset as datum);
    }

    /**
     * public
     *
     * Checks if the window is ready to receive data.
     *
     * return bool
     */
    // tg: code/modules/tgui/tgui_window.dm:137
    public virtual object is_ready()
    {
        return status == TGUI_WINDOW_READY ? TRUE : FALSE;
    }

    /**
     * public
     *
     * Checks if the window can be sanely suspended.
     *
     * return bool
     */
    // tg: code/modules/tgui/tgui_window.dm:147
    public virtual object can_be_suspended()
    {
        // client?.prefs?.read_preference(/datum/preference/toggle/tgui_unlimited_windows): GAP(lead,
        // modules/client/preferences.cs), the preferences are not translated: the preference's default (FALSE).
        object unlimited_windows = FALSE;
        return (!Dm.truthy(fatally_errored)
            && Dm.truthy(pooled)
            && Dm.num(pool_index) > 0
            && (Dm.truthy(unlimited_windows) || Dm.num(pool_index) <= TGUI_WINDOW_SOFT_LIMIT)
            && status == TGUI_WINDOW_READY) ? TRUE : FALSE;
    }

    /**
     * public
     *
     * Acquire the window lock. Pool will not be able to provide this window
     * to other UIs for the duration of the lock.
     *
     * Can be given an optional tgui datum, which will be automatically
     * subscribed to incoming messages via the on_message proc.
     *
     * optional ui /datum/tgui
     */
    // tg: code/modules/tgui/tgui_window.dm:167
    public virtual void acquire_lock(datum_tgui ui = null)
    {
        locked = TRUE;
        locked_by = ui;
    }

    /**
     * public
     *
     * Release the window lock.
     */
    // tg: code/modules/tgui/tgui_window.dm:176
    public virtual void release_lock()
    {
        // Clean up assets sent by tgui datum which requested the lock
        if (Dm.truthy(locked))
            sent_assets = Dm.list();
        locked = FALSE;
        locked_by = null;
    }

    /**
     * public
     *
     * Subscribes the datum to consume window messages on a specified proc.
     *
     * Note, that this supports only one subscriber, because code for that
     * is simpler and therefore faster. If necessary, this can be rewritten
     * to support multiple subscribers.
     */
    // tg: code/modules/tgui/tgui_window.dm:192
    public virtual void subscribe(datum @object = null, object @delegate = null)
    {
        subscriber_object = @object;
        subscriber_delegate = @delegate;
    }

    /**
     * public
     *
     * Unsubscribes the datum. Do not forget to call this when cleaning up.
     */
    // tg: code/modules/tgui/tgui_window.dm:201
    public virtual void unsubscribe(datum @object = null)
    {
        subscriber_object = null;
        subscriber_delegate = null;
    }

    /**
     * public
     *
     * Close the UI.
     *
     * optional can_be_suspended bool
     */
    // tg: code/modules/tgui/tgui_window.dm:212
    public virtual void close(float? can_be_suspended = null)
    {
        can_be_suspended ??= TRUE;
        if (!Dm.truthy(client))
            return;
        if (Dm.truthy(can_be_suspended) && Dm.truthy(this.can_be_suspended()))
        {
            // log_tgui(client, context = "[id]/close (suspending)", window = src): the tgui log (M10)
            visible = FALSE;
            status = TGUI_WINDOW_READY;
            send_message("suspend");
            return;
        }
        // log_tgui(client, context = "[id]/close", window = src): the tgui log (M10)
        release_lock();
        visible = FALSE;
        status = TGUI_WINDOW_CLOSED;
        message_queue = null;
        // Do not close the window to give user some time
        // to read the error message.
        if (!Dm.truthy(fatally_errored))
        {
            // client << browse(null, "window=[id]")
            client.dm_tgui_write(new TguiOutput(Dm.str(id), TguiOutputKind.Close, null)); // DEVIATION(DV44)
        }
    }

    /**
     * public
     *
     * Sends a message to tgui window.
     *
     * required type string Message type
     * required payload list Message payload
     * optional force bool Send regardless of the ready status.
     */
    // tg: code/modules/tgui/tgui_window.dm:243
    public virtual void send_message(object type = null, object payload = null, object force = null)
    {
        if (!Dm.truthy(client))
            return;
        // TGUI_CREATE_MESSAGE(type, payload) (__DEFINES/tgui.dm): url_encode(json_encode(list("type" = type, "payload" = payload)))
        // DEVIATION(DV44): the JSON without the browser transport's url_encode.
        string message = json_encode(Dm.list(Dm.kv("type", type), Dm.kv("payload", payload)));
        // Place into queue if window is still loading
        if (!Dm.truthy(force) && status != TGUI_WINDOW_READY)
        {
            message_queue ??= Dm.list();
            ((DmList)message_queue).Add(message);
            return;
        }
        // client << output(message, is_browser ? "[id]:update" : "[id].browser:update")
        dm_output(message); // DEVIATION(DV44)
        // DEVIATION(DV44): the page's answer to a ping, at once (tgui's ping handler replies "ping/reply" with the payload).
        if (Dm.eq(type, "ping"))
            Waiting.NoSleep(on_message("ping/reply", payload));
    }

    /**
     * public
     *
     * Sends a raw payload to tgui window.
     *
     * required message string JSON+urlencoded blob to send.
     * optional force bool Send regardless of the ready status.
     */
    // tg: code/modules/tgui/tgui_window.dm:265
    public virtual void send_raw_message(object message = null, object force = null)
    {
        if (!Dm.truthy(client))
            return;
        // Place into queue if window is still loading
        if (!Dm.truthy(force) && status != TGUI_WINDOW_READY)
        {
            message_queue ??= Dm.list();
            ((DmList)message_queue).Add(message);
            return;
        }
        dm_output(Dm.str(message)); // DEVIATION(DV44)
    }

    /**
     * public
     *
     * Makes an asset available to use in tgui.
     *
     * required asset datum/asset
     *
     * return bool - TRUE if any assets had to be sent to the client
     */
    // tg: code/modules/tgui/tgui_window.dm:284
    public virtual object send_asset(datum asset = null)
    {
        if (!Dm.truthy(client) || !Dm.truthy(asset))
            return null;
        // sent_assets |= list(asset); . = asset.send(client); the spritesheet's stylesheet message; send_raw_message(the url
        // mappings). DEVIATION(DV44): a native window takes no assets (no asset datum is translated).
        return FALSE;
    }

    /**
     * private
     *
     * Sends queued messages if the queue wasn't empty.
     */
    // tg: code/modules/tgui/tgui_window.dm:303
    public virtual void flush_message_queue()
    {
        if (!Dm.truthy(client) || !Dm.truthy(message_queue))
            return;
        foreach (object message in Dm.each(message_queue as DmList))
            dm_output(Dm.str(message)); // client << output(message, ...): DEVIATION(DV44)
        message_queue = null;
    }

    /**
     * public
     *
     * Replaces the inline HTML content.
     *
     * required inline_html string HTML to inject
     */
    // tg: code/modules/tgui/tgui_window.dm:319
    public virtual void replace_html(object inline_html = null)
    {
        // client << output(url_encode(inline_html), "[id].browser:replaceHtml"): DEVIATION(DV44), a native window has no
        // inline HTML (only the chat panel and the say modal use it, not this window's interfaces).
    }

    /**
     * private
     *
     * Callback for handling incoming tgui messages.
     */
    // tg: code/modules/tgui/tgui_window.dm:330
    public virtual async DmTask on_message(object type = null, object payload = null, object href_list = null)
    {
        // Status can be READY if user has refreshed the window.
        if (Dm.eq(type, "ready") && status == TGUI_WINDOW_READY)
        {
            // Resend the assets
            foreach (object asset in Dm.each(sent_assets))
                send_asset(asset as datum);
        }
        // Mark this window as fatally errored which prevents it from
        // being suspended.
        if (Dm.eq(type, "log") && Dm.truthy((href_list as DmList)?["fatal"]))
            fatally_errored = TRUE;
        // Mark window as ready since we received this message from somewhere
        if (status != TGUI_WINDOW_READY)
        {
            status = TGUI_WINDOW_READY;
            flush_message_queue();
        }
        // Pass message to UI that requested the lock
        if (Dm.truthy(locked) && Dm.truthy(locked_by))
        {
            object prevent_default = await locked_by.on_message(type, payload as DmList, href_list as DmList);
            if (Dm.truthy(prevent_default))
                return;
        }
        // Pass message to the subscriber
        else if (Dm.truthy(subscriber_object))
        {
            object prevent_default = await Dm.Call(subscriber_object, Dm.str(subscriber_delegate), type, payload, href_list);
            if (Dm.truthy(prevent_default))
                return;
        }
        // If not handled, its probably a built in message
        switch (type)
        {
            case "ping":
                send_message("ping/reply", payload);
                break;
            case "ping/set":
                // client?.avgping = payload["ping"]: the ping display (the stat panel, M9)
                break;
            case "visible":
                visible = TRUE;
                if (Dm.truthy(this._listen_lookup?[COMSIG_TGUI_WINDOW_VISIBLE]))
                    this._SendSignal(COMSIG_TGUI_WINDOW_VISIBLE, Dm.list(this, client));
                break;
            case "suspend":
                close(can_be_suspended: TRUE);
                break;
            case "close":
                close(can_be_suspended: FALSE);
                break;
            case "openLink":
            case "cacheReloaded":
            case "chat/resend":
            case "oversizedPayloadRequest":
            case "payloadChunk":
                // :375-394: client << link(url), reinitialize, SSchat.handle_resend, the oversized payload chunks.
                // DEVIATION(DV44): the native window sends none of these.
                break;
        }
    }

    // DEVIATION(DV44): BYOND's output() of a message to the window's browser control.
    private void dm_output(string message)
    {
        client.dm_tgui_write(new TguiOutput(Dm.str(id), TguiOutputKind.Message, message));
    }
}
