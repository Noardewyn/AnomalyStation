// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/say_modal/modal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md, protocol version 38). tg's say window is a tgui window in BYOND's skin (SKIN_TGUISAY) that the
// player's keys show and fill on the client; it talks to this datum through window.send_message and window.subscribe. The port's
// window is the Godot client's (D250, a port of tgui-say): the client opens it on the keys itself (tg's tgui_say_create_open_command
// writes the open command into the keybinding macro, so it never reaches the server either), its messages to this datum arrive as the
// Say action (ClientSeam.Say calls on_message), and send_message goes out through the presentation seam (SayModal). So /datum/tgui_window
// and the skin's winset calls have no counterpart here.

// tg: code/modules/tgui_input/say_modal/modal.dm:2  /client/var/datum/tgui_say/tgui_say: declared by the generated var block of /client.

/**
 * The tgui say modal. This initializes an input window which hides until
 * the user presses one of the speech hotkeys. Once something is entered, it will
 * delegate the speech to the proper channel.
 */
// tg: code/modules/tgui_input/say_modal/modal.dm:23
public partial class datum_tgui_say : datum
{
    /// Injury phrases to blurt out
    // tg: code/modules/tgui_input/say_modal/modal.dm:27  var/static/list/hurt_phrases
    public static ref DmList hurt_phrases => ref TgState.Current.datum_tgui_say__hurt_phrases;

    /** Creates the new input window to exist in the background. */
    // tg: code/modules/tgui_input/say_modal/modal.dm:42
    [DmParams("client", "id")]
    public override async DmTask New(params object[] __extra)
    {
        client client = Dm.arg<client>(__extra, 0);
        await base.New();
        this.client = client;
        // tg: :44-47  window = new(client, id); winset(client, SKIN_TGUISAY, "size=1,1;is-visible=0;"); window.subscribe(src,
        // PROC_REF(on_message)); window.is_browser = TRUE: the client's own window (see the file comment)
    }

    /**
     * After a brief period, injects the scripts into
     * the window to listen for open commands.
     */
    // tg: code/modules/tgui_input/say_modal/modal.dm:53
    public virtual async DmTask initialize()
    {
        // Sleep to defer initialization to after client constructor
        await Dm.sleep(3 * SECONDS);
        // tg: :57-61  window.initialize(strict_mode = TRUE, inline_css, inline_js): the window's scripts are the client's; its "ready"
        // answer is the load below (the port's window is ready as soon as the client has joined)
        if (client == null || QDELETED(client))
            return;
        load();
    }

    /**
     * Ensures nothing funny is going on window load.
     * Minimizes the window, sets max length, closes all
     * typing and thinking indicators. This is triggered
     * as soon as the window sends the "ready" message.
     */
    // tg: code/modules/tgui_input/say_modal/modal.dm:69
    public virtual object load()
    {
        window_open = FALSE;

        // tg: :72  winset(client, SKIN_TGUISAY, "pos=848,500;is-visible=0;"): the client's window

        DmWorldState.Current.Sinks.Messages.SayModal(client, "props", (int)max_length, Dm.truthy(safe_read_pref(client, typeof(datum_preference_toggle_tgui_say_light_mode))));
        // tg: :74-78  "scale" = the ui_scale preference: the client scales its own HUD (its options' HUD scale)

        stop_thinking();
        return TRUE;
    }

    /**
     * Sets the window as "opened" server side, though it is already
     * visible to the user. We do this to set local vars &
     * start typing (if enabled and in an IC channel). Logs the event.
     *
     * Arguments:
     * payload - A list containing the channel the window was opened in.
     */
    // tg: code/modules/tgui_input/say_modal/modal.dm:92
    public virtual object open(DmList payload = null)
    {
        if (!Dm.truthy(payload?["channel"]))
            throw Dm.CRASH("No channel provided to an open TGUI-Say", "code/modules/tgui_input/say_modal/modal.dm", 94);
        window_open = TRUE;
        saved_text = "";
        if (!Dm.eq(payload["channel"], OOC_CHANNEL) && !Dm.eq(payload["channel"], ADMIN_CHANNEL) && !Dm.eq(payload["channel"], PRAY_CHANNEL))
            start_thinking();
        // tg: :99-100  log_speech_indicators when the indicators are off: the speech indicator log (the admin logs, M10)
        return TRUE;
    }

    /**
     * Closes the window serverside. Closes any open chat bubbles
     * regardless of preference. Logs the event.
     */
    // tg: code/modules/tgui_input/say_modal/modal.dm:107
    public virtual void close()
    {
        window_open = FALSE;
        stop_thinking();
        // tg: :110-111  log_speech_indicators when the indicators are off (the admin logs, M10)
    }

    /**
     * The equivalent of ui_act, this waits on messages from the window
     * and delegates actions.
     */
    // tg: code/modules/tgui_input/say_modal/modal.dm:117
    public virtual object on_message(object type = null, DmList payload = null)
    {
        if (Dm.eq(type, "ready"))
        {
            load();
            return TRUE;
        }
        if (Dm.eq(type, "open"))
        {
            open(payload);
            return TRUE;
        }
        if (Dm.eq(type, "close"))
        {
            close();
            return TRUE;
        }
        if (Dm.eq(type, "thinking"))
        {
            if (Dm.eq(payload["visible"], TRUE))
            {
                start_thinking();
                return TRUE;
            }
            if (Dm.eq(payload["visible"], FALSE))
            {
                stop_thinking();
                return TRUE;
            }
            return FALSE;
        }
        if (Dm.eq(type, "typing"))
        {
            start_typing();
            return TRUE;
        }
        if (Dm.eq(type, "entry") || Dm.eq(type, "force") || Dm.eq(type, "save"))
        {
            handle_entry(type, payload);
            return TRUE;
        }
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/tgui_input/say_modal/modal.dm:27  var/static/list/hurt_phrases
    internal DmList datum_tgui_say__hurt_phrases = Dm.list("GACK!", "GLORF!", "OOF!", "AUGH!", "OW!", "URGH!", "HRNK!");
}
