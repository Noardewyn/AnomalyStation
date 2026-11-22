// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_context.dm (M9 slice F: the pull and
// grab screentips)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/carbon/human/human_context.dm:1
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_context.dm:1
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (!ishuman(user))
            return dot;

        if (user == this)
            return dot;

        if (pulledby == user)
        {
            switch (user.grab_state)
            {
                case GRAB_PASSIVE:
                    context[SCREENTIP_CONTEXT_CTRL_LMB] = "Grip";
                    break;
                case GRAB_AGGRESSIVE:
                    context[SCREENTIP_CONTEXT_CTRL_LMB] = "Choke";
                    break;
                case GRAB_NECK:
                    context[SCREENTIP_CONTEXT_CTRL_LMB] = "Strangle";
                    break;
                default:
                    return dot;
            }
        }
        else
            context[SCREENTIP_CONTEXT_CTRL_LMB] = "Pull";

        return CONTEXTUAL_SCREENTIP_SET;
    }
}
