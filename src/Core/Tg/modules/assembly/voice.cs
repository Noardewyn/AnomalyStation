// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/voice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333): the voice analyzer, hearing what is said near it (not whispers or relayed lines): used in
// hand it records the next phrase (or the speaker's voice), then pulses two seconds after its click when it hears it again by
// its mode (inclusive: the phrase within a line; exclusive: the line itself; recognizer: the recorded voice; voice sensor: any
// speech), the mode cycled with a multitool. The var block is generated (D111); the static mode names are in TgState (rules 10.6).

// tg: code/modules/assembly/voice.dm:6
public partial class obj_item_assembly_voice : obj_item_assembly
{
    // tg: code/modules/assembly/voice.dm:1-4  the file-local modes (#undef at :123-126, rules 11.4)
    private const float INCLUSIVE_MODE = 1;
    private const float EXCLUSIVE_MODE = 2;
    private const float RECOGNIZER_MODE = 3;
    private const float VOICE_SENSOR_MODE = 4;

    // tg: code/modules/assembly/voice.dm:19  var/static/list/modes (rules 10.6)
    public static ref DmList modes => ref TgState.Current.obj_item_assembly_voice__modes;

    // tg: code/modules/assembly/voice.dm:28
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        become_hearing_sensitive(ROUNDSTART_TRAIT);
        return dot;
    }

    // tg: code/modules/assembly/voice.dm:32
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use a multitool to swap between \"inclusive\", \"exclusive\", \"recognizer\", and \"voice sensor\" mode.")));
        return dot;
    }

    // tg: code/modules/assembly/voice.dm:36
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (Dm.truthy(message_mods[WHISPER_MODE]) || Dm.truthy(message_mods[MODE_RELAY])) //Too quiet lad
            return FALSE;
        if (speaker == this)
            return FALSE;

        // raw_message can contain multiple spaces between words etc which are not seen in chat due to HTML rendering
        // this means if the teller records a message with e.g. double spaces or tabs, other people will not be able to trigger the sensor since they don't know how to perform the same combination
        raw_message = htmlrendertext(raw_message);

        if (Dm.truthy(listening) && !Dm.truthy(radio_freq))
            await record_speech(speaker, raw_message, message_language);
        else
        {
            if (Dm.truthy(check_activation(speaker, raw_message)))
                send_pulse();
        }
        return Dm.bitor(HEAR_HEARD, HEAR_UNDERSTOOD);
    }

    // tg: code/modules/assembly/voice.dm:54
    public virtual async DmTask record_speech(atom_movable speaker = null, object raw_message = null, object message_language = null)
    {
        if (mode == INCLUSIVE_MODE)
        {
            recorded = Dm.str(raw_message);
            listening = FALSE;
            await say(I18n.T("Activation message is '[]'.", recorded), sanitize: FALSE, language: message_language);
        }
        else if (mode == EXCLUSIVE_MODE)
        {
            recorded = Dm.str(raw_message);
            listening = FALSE;
            await say(I18n.T("Activation message is '[]'.", recorded), sanitize: FALSE, language: message_language);
        }
        else if (mode == RECOGNIZER_MODE)
        {
            recorded = Dm.str(speaker.get_voice());
            listening = FALSE;
            await say(I18n.T("Your voice pattern is saved."), language: message_language);
        }
        else if (mode == VOICE_SENSOR_MODE)
        {
            if (Dm.truthy(length(raw_message)))
                send_pulse();
        }
    }

    // tg: code/modules/assembly/voice.dm:72
    public virtual object check_activation(atom_movable speaker = null, object raw_message = null)
    {
        if (recorded == "")
            return FALSE;

        if (mode == INCLUSIVE_MODE)
        {
            if (Dm.truthy(findtext(raw_message, recorded)))
                return TRUE;
        }
        else if (mode == EXCLUSIVE_MODE)
        {
            if (Dm.eq(raw_message, recorded))
                return TRUE;
        }
        else if (mode == RECOGNIZER_MODE)
        {
            if (Dm.eq(speaker.get_voice(), recorded))
                return TRUE;
        }
        else if (mode == VOICE_SENSOR_MODE)
        {
            if (Dm.truthy(length(raw_message)))
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/assembly/voice.dm:92
    public virtual void send_pulse()
    {
        visible_message(I18n.T("clicks."), visible_message_flags: EMOTE_MESSAGE);
        playsound(this, Dm.resource("sound/effects/whirthunk.ogg"), 30);
        _addtimer(Dm.@new<datum_callback>(this, nameof(pulse)), 2 * SECONDS, file: "code/modules/assembly/voice.dm", line: 95);
    }

    // tg: code/modules/assembly/voice.dm:97
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        mode = Dm.mod(Dm.num(mode), modes.Count);
        mode++;
        to_chat(user, span_notice(I18n.T("You set [] into [] mode.", this, modes[Dm.num(mode)])));
        listening = FALSE;
        recorded = "";
        return TRUE;
    }

    // tg: code/modules/assembly/voice.dm:106
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(secured) || holder != null)
            return FALSE;
        listening = Dm.num(!Dm.truthy(listening));
        await say(I18n.T("[] recording input.", Dm.truthy(listening) ? I18n.T("Now") : I18n.T("No longer")));
        return TRUE;
    }

    // tg: code/modules/assembly/voice.dm:113
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (user == null)
            return FALSE;
        await activate();
        return TRUE;
    }

    // tg: code/modules/assembly/voice.dm:119
    public override async DmTask<object> toggle_secure()
    {
        object dot = await base.toggle_secure();
        listening = FALSE;
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/assembly/voice.dm:19  var/static/list/modes = list("inclusive", "exclusive", "recognizer", "voice sensor")
    internal DmList obj_item_assembly_voice__modes = Dm.list(
        "inclusive",
        "exclusive",
        "recognizer",
        "voice sensor");
}
