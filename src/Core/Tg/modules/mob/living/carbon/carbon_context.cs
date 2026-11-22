// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_context.dm (M9 slice F: the screentips of
// a carbon, registered by its Initialize)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/carbon/carbon_context.dm:1
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_context.dm:1
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item != null)
        {
            context[SCREENTIP_CONTEXT_CTRL_SHIFT_LMB] = "Offer item";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (!ishuman(user))
            return dot;

        mob_living_carbon_human human_user = (mob_living_carbon_human)user;

        if (Dm.truthy(human_user.combat_mode))
            context[SCREENTIP_CONTEXT_LMB] = "Attack";
        else if (human_user == this)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Check injuries";

            if (Dm.truthy((get_bodypart(human_user.zone_selected) as obj_item_bodypart)?.cached_bleed_rate))
                context[SCREENTIP_CONTEXT_CTRL_LMB] = "Grab limb";
        }

        if (human_user != this)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Shove";

            if (!Dm.truthy(human_user.combat_mode))
            {
                if (Dm.eq(body_position, STANDING_UP))
                {
                    if (Dm.eq(check_zone(user.zone_selected), BODY_ZONE_HEAD) && Dm.truthy(get_bodypart(BODY_ZONE_HEAD)))
                        context[SCREENTIP_CONTEXT_LMB] = "Headpat";
                    else if (user.zone_selected == BODY_ZONE_PRECISE_GROIN && get_organ_by_type(typeof(obj_item_organ_tail)) != null)
                        context[SCREENTIP_CONTEXT_LMB] = "Pull tail";
                    else
                        context[SCREENTIP_CONTEXT_LMB] = "Hug";
                }
                else if (health >= 0 && !HAS_TRAIT(this, TRAIT_FAKEDEATH))
                    context[SCREENTIP_CONTEXT_LMB] = "Shake";
                else
                    context[SCREENTIP_CONTEXT_LMB] = "CPR";
            }
        }

        return CONTEXTUAL_SCREENTIP_SET;
    }
}
