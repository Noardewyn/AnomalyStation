// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/strained_muscles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Strained Muscles (the speed boost that tires the changeling more every four seconds and the
// collapse after it). The var block is generated (D111).

//Strained Muscles: Temporary speed boost at the cost of rapid damage
//Limited because of space suits and such; ideally, used for a quick getaway

// tg: code/modules/antagonists/changeling/powers/strained_muscles.dm:4
public partial class datum_action_changeling_strained_muscles : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/strained_muscles.dm:17
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        active = Dm.not(active);
        if (Dm.truthy(active))
            to_chat(user, span_notice(I18n.T("Our muscles tense and strengthen.")));
        else
        {
            user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_strained_muscles));
            to_chat(user, span_notice(I18n.T("Our muscles relax.")));
            if (stacks >= 10)
            {
                to_chat(user, span_danger(I18n.T("We collapse in exhaustion.")));
                await user.Paralyze(60);
                await user.emote("gasp");
            }
        }

        Waiting.Detach(muscle_loop(user as mob_living_carbon));

        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/strained_muscles.dm:34
    public override async DmTask Remove(mob user = null)
    {
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_strained_muscles));
        await base.Remove(user);
    }

    // tg: code/modules/antagonists/changeling/powers/strained_muscles.dm:38
    public virtual async DmTask muscle_loop(mob_living_carbon user = null)
    {
        while (Dm.truthy(active))
        {
            if (QDELETED(this) || QDELETED(user))
                return;

            await user.add_movespeed_modifier(typeof(datum_movespeed_modifier_strained_muscles));
            if (IS_UNCONSCIOUS_OR_CRIT(user) || user.staminaloss >= 90)
            {
                active = Dm.not(active);
                to_chat(user, span_notice(I18n.T("Our muscles relax without the energy to strengthen them.")));
                await user.Paralyze(40);
                user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_strained_muscles));
                break;
            }

            stacks++;

            await user.adjust_stamina_loss(stacks * 1.3f); //At first the changeling may regenerate stamina fast enough to nullify fatigue, but it will stack

            if (stacks == 11) //Warning message that the stacks are getting too high
                to_chat(user, span_warning(I18n.T("Our legs are really starting to hurt...")));

            await sleep(4 * SECONDS);
        }

        while (!Dm.truthy(active) && stacks != 0) //Damage stacks decrease fairly rapidly while not in sanic mode
        {
            if (QDELETED(this) || QDELETED(user))
                return;

            stacks--;
            await sleep(2 * SECONDS);
        }
    }
}
