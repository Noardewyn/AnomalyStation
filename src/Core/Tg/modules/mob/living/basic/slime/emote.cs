// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Only the moods' run_emote (gaps package C4); the other emotes are vars.

// tg: code/modules/mob/living/basic/slime/emote.dm:27
public partial class datum_emote_slime_mood : datum_emote_slime
{
    // tg: code/modules/mob/living/basic/slime/emote.dm:31
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        mob_living_basic_slime slime_user = user as mob_living_basic_slime;
        slime_user.current_mood = Dm.text("[]", mood_key);
        await slime_user.regenerate_icons();
        return dot;
    }
}
