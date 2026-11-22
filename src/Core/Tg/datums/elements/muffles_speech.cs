// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/muffles_speech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A: the whole file but for the spells (not in the core version, not-translated.md section 70): a muzzle's wearer is
// muffled ("mmmf") and makes only a noise for an audible emote. Nothing casts a spell, so try_spellcast is never called.

// tg: code/datums/elements/muffles_speech.dm:1
public partial class datum_element_muffles_speech : datum_element
{
    // tg: code/datums/elements/muffles_speech.dm:3
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ITEM_EQUIPPED, nameof(equipped));
        RegisterSignal(target, COMSIG_ITEM_DROPPED, nameof(dropped));
        return dot;
    }

    // tg: code/datums/elements/muffles_speech.dm:11
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        return dot;
    }

    // tg: code/datums/elements/muffles_speech.dm:15
    [SignalHandler]
    public virtual void equipped(obj_item source = null, mob user = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(source.slot_flags, slot)))
        {
            RegisterSignal(user, COMSIG_MOB_SAY, nameof(muzzle_talk));
            RegisterSignal(user, COMSIG_MOB_PRE_EMOTED, nameof(emote_override));
            RegisterSignal(user, COMSIG_MOB_BEFORE_SPELL_CAST, nameof(try_spellcast));
        }
    }

    // tg: code/datums/elements/muffles_speech.dm:22
    [SignalHandler]
    public virtual void dropped(obj_item source = null, mob user = null)
    {
        UnregisterSignal(user, Dm.list(COMSIG_MOB_PRE_EMOTED, COMSIG_MOB_SAY, COMSIG_MOB_BEFORE_SPELL_CAST));
    }

    // tg: code/datums/elements/muffles_speech.dm:26
    [SignalHandler]
    public virtual object emote_override(mob_living source = null, object key = null, object @params = null, object type_override = null, object intentional = null, datum_emote emote = null)
    {
        if (!Dm.truthy(emote.hands_use_check) && Dm.truthy(Dm.bitand(emote.emote_type, EMOTE_AUDIBLE)))
        {
            source.audible_message(I18n.T("makes a []noise.", pick("strong ", "weak ", "")), audible_message_flags: Dm.bitor(EMOTE_MESSAGE, ALWAYS_SHOW_SELF_MESSAGE));
            return COMPONENT_CANT_EMOTE;
        }
        return NONE;
    }

    // tg: code/datums/elements/muffles_speech.dm:33
    [SignalHandler]
    public virtual void muzzle_talk(datum source = null, DmList speech_args = null)
    {
        if (HAS_TRAIT(source, TRAIT_SIGN_LANG))
            return;
        object spoken_message = speech_args[SPEECH_MESSAGE];
        if (Dm.truthy(spoken_message))
        {
            DmList words = splittext(spoken_message, " ");
            object yell_suffix = Dm.copytext(spoken_message, findtext(spoken_message, "!"));
            spoken_message = "";

            for (float ind = 1; ind <= length(words); ind++)
            {
                string new_word = "";
                float letters = Dm.length(words[ind]) + Dm.rand(-1, 1);
                for (float i = 1; i <= letters; i++)
                    new_word += "m";
                new_word += "f";
                words[ind] = Dm.truthy(yell_suffix) ? uppertext(new_word) : new_word;
            }
            spoken_message = Dm.text("[][]", jointext(words, " "), yell_suffix);
        }
        speech_args[SPEECH_MESSAGE] = spoken_message;
    }

    // tg: code/datums/elements/muffles_speech.dm:52
    [SignalHandler]
    public virtual object try_spellcast(mob_living source = null, datum spell = null, params object[] __extra)
    {
        Dm.NotTranslated("code/datums/elements/muffles_speech.dm:52", "try_spellcast: a muffled spell's invocation (spells, not in the core version)");
        return NONE;
    }
}
