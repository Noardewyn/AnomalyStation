// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI's emotion display emotes (its status displays' faces). The var blocks are generated
// (D111).

// This might be worth making an abstract type.
// tg: code/modules/mob/living/silicon/ai/emote.dm:7
public partial class datum_emote_ai_emotion_display : datum_emote_ai
{
    // tg: code/modules/mob/living/silicon/ai/emote.dm:11
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        (user as mob_living_silicon_ai)?.apply_emote_display(emotion);
        return dot;
    }
}

// tg: code/modules/mob/living/silicon/ai/emote.dm:71
public partial class datum_emote_ai_emotion_display_friend_computer : datum_emote_ai_emotion_display
{
    // tg: code/modules/mob/living/silicon/ai/emote.dm:75
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS);

        if (frequency == null)
            return dot;

        datum_signal status_signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("command", "friendcomputer")));
        frequency.post_signal(null, status_signal); // tg posts from the emote datum (src); the port's post_signal takes an obj source, and the displays never read it
        return dot;
    }
}
