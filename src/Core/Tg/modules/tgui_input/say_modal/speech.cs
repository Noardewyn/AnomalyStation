// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui_input/say_modal/speech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md). The window's messages go out through the presentation seam (modal.cs's file comment).

public partial class datum_tgui_say : datum
{
    /**
     * Alters text when players are injured.
     * Adds text, trims left and right side
     *
     * Arguments:
     *  payload - a string list containing entry & channel
     * Returns:
     *  string - the altered entry
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:10
    public virtual object alter_entry(DmList payload = null)
    {
        object entry = payload["entry"];
        DmList phrases = (alter_phrases is var or__1 && Dm.truthy(or__1) ? or__1 : hurt_phrases) as DmList;

        /// No OOC leaks
        if (!Dm.truthy(entry) || Dm.eq(payload["channel"], OOC_CHANNEL) || Dm.eq(payload["channel"], ME_CHANNEL) || Dm.eq(payload["channel"], PRAY_CHANNEL))
            return Dm.pick(phrases);
        /// Random trimming for larger sentences
        if (Dm.length(entry) > 50)
            entry = trim(entry, Dm.rand(40, 50));
        else
        {
            /// Otherwise limit trim to just last letter
            if (Dm.length(entry) > 1)
                entry = trim(entry, Dm.length(entry));
        }
        return Dm.text("[]-[]", entry, Dm.pick(phrases));
    }

    /**
     * Delegates the speech to the proper channel.
     *
     * Arguments:
     * 	entry - the text to broadcast
     * 	channel - the channel to broadcast in
     * Returns:
     *  boolean - on success or failure
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:35
    public virtual object delegate_speech(object entry = null, object channel = null)
    {
        if (Dm.eq(channel, SAY_CHANNEL))
        {
            client.mob.say_verb(entry);
            return TRUE;
        }
        if (Dm.eq(channel, RADIO_CHANNEL))
        {
            client.mob.say_verb(Dm.add(";", entry));
            return TRUE;
        }
        if (Dm.eq(channel, ME_CHANNEL))
        {
            client.mob.me_verb(entry);
            return TRUE;
        }
        if (Dm.eq(channel, OOC_CHANNEL))
        {
            client.ooc(entry);
            return TRUE;
        }
        if (Dm.eq(channel, ADMIN_CHANNEL))
        {
            // tg: :46  SSadmin_verbs.dynamic_invoke_verb(client, /datum/admin_verb/cmd_admin_say, entry): the admin tools (M10)
            Dm.Deferred("code/modules/tgui_input/say_modal/speech.dm:46", "delegate_speech: the admin channel (asay; the admin tools, M10+)");
            return TRUE;
        }
        if (Dm.eq(channel, PRAY_CHANNEL))
        {
            client.mob.pray(entry);
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Force say handler.
     * Sends a message to the say modal to send its current value.
     * Arguments:
     * 	alter_phrases - Optional list of alternate suffixes to blurt out
     * 	immediate - If [TRUE], the say must be invoked inline due to side effects that may cause the mob to be unable to speak
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:65
    public virtual void force_say(DmList alter_phrases = null, float? immediate = null)
    {
        immediate ??= FALSE;
        this.alter_phrases = alter_phrases;
        if (Dm.truthy(immediate))
        {
            if (!Dm.truthy(saved_text))
            {
                saved_text = "";
                if (!Dm.truthy(saved_channel))
                    saved_channel = SAY_CHANNEL;
            }

            handle_entry("force", Dm.list(Dm.kv("type", "force"), Dm.kv("entry", saved_text), Dm.kv("channel", saved_channel)));
            DmWorldState.Current.Sinks.Messages.SayModal(client, "close", 0, false);
        }
        else
            DmWorldState.Current.Sinks.Messages.SayModal(client, "force", 0, false);

        stop_typing();
    }

    /**
     * Exports whatever text is currently in the input box to this datum
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:83
    public virtual void save_text()
    {
        saved_text = null;
        DmWorldState.Current.Sinks.Messages.SayModal(client, "save", 0, false);
    }

    /**
     * Handles text entry and forced speech.
     *
     * Arguments:
     *  type - a string "entry" or "force" based on how this function is called
     *  payload - a string list containing entry & channel
     * Returns:
     *  boolean - success or failure
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:126
    public virtual object handle_entry(object type = null, DmList payload = null)
    {
        if (!Dm.truthy(payload?["channel"]) || payload["entry"] == null)
            throw Dm.CRASH(Dm.text("[] entered in a null payload to the chat window.", usr), "code/modules/tgui_input/say_modal/speech.dm", 128);
        if (Dm.length(payload["entry"]) > max_length)
            throw Dm.CRASH(Dm.text("[] has entered more characters than allowed into a TGUI-Say", usr), "code/modules/tgui_input/say_modal/speech.dm", 130);
        if (Dm.eq(type, "entry"))
        {
            delegate_speech(payload["entry"], payload["channel"]);
            return TRUE;
        }
        if (Dm.eq(type, "force"))
        {
            object target_channel = payload["channel"];
            if (Dm.eq(target_channel, ME_CHANNEL) || Dm.eq(target_channel, OOC_CHANNEL) || Dm.eq(target_channel, PRAY_CHANNEL))
                target_channel = SAY_CHANNEL; // No ooc leaks
            delegate_speech(alter_entry(payload), target_channel);
            return TRUE;
        }
        if (Dm.eq(type, "save"))
        {
            saved_text = ""; // so we can differentiate null (nothing saved) and empty (nothing typed)
            object target_channel = payload["channel"];
            if (Dm.eq(target_channel, SAY_CHANNEL) || Dm.eq(target_channel, RADIO_CHANNEL))
            {
                saved_text = Dm.str(payload["entry"]); // only save IC text
                saved_channel = target_channel;
            }
            return TRUE;
        }
        return FALSE;
    }
}

public partial class mob_living_carbon_human : mob_living_carbon
{
    /**
     * Makes the player force say what's in their current input box.
     * Arguments:
     * * alter_phrases - Optional list of alternate suffixes to blurt out
     * * immediate - If [TRUE], the say must be invoked inline due to side effects that may cause the mob to be unable to speak
     * * major - If [TRUE], a "major action" triggered the force say, which may have additional side effects
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:94
    public virtual object force_say(DmList alter_phrases = null, float? immediate = null, float? major = null)
    {
        immediate ??= FALSE;
        major ??= TRUE;
        if (IS_UNCONSCIOUS_OR_CRIT(this) || !Dm.truthy(client?.tgui_say?.window_open))
            return FALSE;
        client.tgui_say.force_say(alter_phrases, immediate);
        // tg: :98-101  log_speech_indicators (the admin logs, M10)
        if (Dm.truthy(_listen_lookup?[COMSIG_HUMAN_FORCESAY]))
            _SendSignal(COMSIG_HUMAN_FORCESAY, Dm.list(this, major));
        return null;
    }
}

public partial class mob : atom_movable
{
    /**
     * Gets whatever text is currently in this mob's say box and returns it.
     *
     * Note: Sleeps, due to waiting for say to respond.
     */
    // tg: code/modules/tgui_input/say_modal/speech.dm:109
    public virtual async DmTask<object> get_typing_text()
    {
        if (!Dm.truthy(client?.tgui_say?.window_open))
            return null;
        client.tgui_say.save_text();
        float safety = world.time;
        while (!(istext(client?.tgui_say?.saved_text) || world.time - safety > 2 * SECONDS))
            await stoplag(); // tg's UNTIL(X) (__DEFINES/_helpers.dm:31): while(!(X)) stoplag()
        return client?.tgui_say?.saved_text;
    }
}
