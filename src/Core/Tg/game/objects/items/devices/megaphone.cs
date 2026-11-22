// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/megaphone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298; the suicide: gaps DV2). The text-to-speech filter's strings have no client: the span and the sound are what a player notices.

// tg: code/game/objects/items/devices/megaphone.dm:1
public partial class obj_item_megaphone : obj_item
{
    // tg: code/game/objects/items/devices/megaphone.dm:14  (gaps DV2)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is uttering [] last words into \\the []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        spamcheck = 0;//so they dont have to worry about recharging
        await user.say(I18n.T("AAAAAAAAAAAARGHHHHH"), forced: "megaphone suicide");//he must have died while coding this
        return OXYLOSS;
    }

    // tg: code/game/objects/items/devices/megaphone.dm:20
    [DmParams("equipper", "slot")]
    public override async DmTask equipped__proc(mob equipper = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(equipper, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
        {
            RegisterSignal(equipper, COMSIG_MOB_SAY, nameof(handle_speech));
            RegisterSignal(equipper, COMSIG_LIVING_TREAT_MESSAGE, nameof(add_tts_filter));
        }
    }

    // tg: code/game/objects/items/devices/megaphone.dm:26
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, Dm.list(COMSIG_MOB_SAY, COMSIG_LIVING_TREAT_MESSAGE));
        return dot;
    }

    // tg: code/game/objects/items/devices/megaphone.dm:30
    [SignalHandler]
    public virtual void handle_speech(mob_living user = null, DmList speech_args = null)
    {
        if (HAS_TRAIT(user, TRAIT_SIGN_LANG) || user.get_active_held_item() != this)
            return;
        if (spamcheck > Dm.num(world.time))
        {
            to_chat(user, span_warning(I18n.T("\\The [] needs to recharge!", this)));
        }
        else
        {
            playsound(loc, Dm.resource("sound/items/megaphone.ogg"), 100, FALSE, TRUE);
            speech_args[SPEECH_SPANS] = Dm.bitor(speech_args[SPEECH_SPANS], voicespan);
        }
    }

    // tg: code/game/objects/items/devices/megaphone.dm:40
    [SignalHandler]
    public virtual void add_tts_filter(mob_living_carbon user = null, DmList message_args = null)
    {
        if (HAS_TRAIT(user, TRAIT_SIGN_LANG) || user.get_active_held_item() != this)
            return;
        if (spamcheck > Dm.num(world.time))
            return;
        spamcheck = Dm.num(world.time) + 5 * SECONDS;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            ///somewhat compressed and ear-grating, crusty and noisy with a bit of echo.
            message_args[TREAT_TTS_FILTER_ARG] = Dm.add(message_args[TREAT_TTS_FILTER_ARG], "acrusher=samples=9:level_out=7,aecho=delays=100:decays=0.4,aemphasis=type=emi,crystalizer=i=6,acontrast=60,rubberband=pitch=0.9");
        }
        else
        {
            ///A sharper and louder sound with a bit of echo
            message_args[TREAT_TTS_FILTER_ARG] = Dm.add(message_args[TREAT_TTS_FILTER_ARG], "acrusher=samples=2:level_out=6,aecho=delays=90:decays=0.3,aemphasis=type=cd,acontrast=30,crystalizer=i=5");
        }
    }

    // tg: code/game/objects/items/devices/megaphone.dm:54
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("voice synthesizer overloaded"));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        voicespan = Dm.list(SPAN_REALLYBIG, "userdanger");
        return TRUE;
    }
}
