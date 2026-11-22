// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/laws.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326).

// tg: code/modules/mob/living/silicon/ai/laws.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/laws.dm:1  GAME_VERB_PROC_DESC(/mob/living/silicon/ai, show_laws_verb, "Show Laws", ..., "AI Commands"): the verb proc
    public virtual void show_laws_verb(params object[] __extra)
    {
        Waiting.Detach(__gvb_show_laws_verb(Dm.list()));
    }

    // tg: code/modules/mob/living/silicon/ai/laws.dm:1  the body
    public virtual async DmTask __gvb_show_laws_verb(DmList structured_args = null)
    {
        if (Dm.eq((usr as mob)?.stat, DEAD))
            return; //won't work if dead
        await show_laws();
    }

    // tg: code/modules/mob/living/silicon/ai/laws.dm:6
    public override async DmTask<object> try_sync_laws()
    {
        foreach (mob_living_silicon_robot borgo in Dm.each<mob_living_silicon_robot>(connected_robots))
        {
            if (Dm.truthy(await borgo.try_sync_laws()))
            {
                to_chat(borgo, span_bold(I18n.T("Your AI has reminded you of your laws:")));
                await borgo.show_laws();
            }
        }
        return null;
    }
}
