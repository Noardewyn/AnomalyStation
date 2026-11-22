// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (X3).

// tg: code/modules/mob/living/silicon/ai/examine.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/examine.dm:1
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = Dm.list();
        if (stat == DEAD)
            Dm.append(ref dot, span_warning(I18n.T("[] appear[] to be non functional.", p_They(), p_s())));
        Dm.append(ref dot, span_notice(I18n.T("[] floor <b>bolts</b> are [].", p_Their(), Dm.truthy(is_anchored) ? I18n.T("tightened") : I18n.T("loose"))));
        if (Dm.truthy(is_anchored))
        {
            if (!Dm.truthy(opened))
            {
                if (!Dm.truthy(emagged))
                    Dm.append(ref dot, span_notice(I18n.T("[] access panel is [], but could be <b>pried</b> open.", p_Their(), stat == DEAD ? I18n.T("damaged") : I18n.T("closed and locked"))));
                else
                    Dm.append(ref dot, span_warning(I18n.T("[] access panel lock is sparking, the cover can be <b>pried</b> open.", p_Their())));
            }
            else
                Dm.append(ref dot, span_notice(I18n.T("[] neural network connection could be <b>cut</b>, the access panel cover can be <b>pried</b> back into place.", p_Their())));
        }
        if (stat != DEAD)
        {
            if (Dm.truthy(get_brute_loss()))
            {
                if (Dm.num(get_brute_loss()) < 30)
                    Dm.append(ref dot, span_warning(I18n.T("[] look[] slightly dented.", p_They(), p_s())));
                else
                    Dm.append(ref dot, span_warning(I18n.T("<B>[] look[] severely dented!</B>", p_They(), p_s())));
            }
            if (Dm.truthy(get_fire_loss()))
            {
                if (Dm.num(get_fire_loss()) < 30)
                    Dm.append(ref dot, span_warning(I18n.T("[] look[] slightly charred.", p_They(), p_s())));
                else
                    Dm.append(ref dot, span_warning(I18n.T("<B>[] casing is melted and heat-warped!</B>", p_Their())));
            }
            if (deployed_shell != null)
                Dm.append(ref dot, I18n.T("The wireless networking light is blinking."));
            else if (!Dm.truthy(shunted) && client == null)
                Dm.append(ref dot, I18n.T("[]Core.exe has stopped responding! NTOS is searching for a solution to the problem...", this));
        }

        Dm.append(ref dot, await base.examine(user, __extra));
        return dot;
    }
}
