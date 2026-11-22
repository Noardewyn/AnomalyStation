// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/mimic_voice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Mimic Voice (the human's override_voice set to an entered name, at the cost of chemical
// regeneration). The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/mimic_voice.dm:1
public partial class datum_action_changeling_mimicvoice : datum_action_changeling
{
    // Fake Voice
    // tg: code/modules/antagonists/changeling/powers/mimic_voice.dm:12
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        mob_living_carbon_human human_user = (mob_living_carbon_human)user; // the DM parameter is typed mob/living/carbon/human (rules 3.3)
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (Dm.truthy(human_user.override_voice))
        {
            changeling.chem_recharge_slowdown -= 0.25f;
            human_user.override_voice = "";
            to_chat(user, span_notice(I18n.T("We return our vocal glands to their original position.")));
            return null;
        }

        object mimic_voice = await sanitize_name(await tgui_input_text(user, I18n.T("Enter a name to mimic"), I18n.T("Mimic Voice"), max_length: MAX_NAME_LEN));
        if (!Dm.truthy(mimic_voice))
            return null;
        await base.sting_action(user, target);
        changeling.chem_recharge_slowdown += 0.25f;
        human_user.override_voice = Dm.str(mimic_voice);
        to_chat(user, span_notice(I18n.T("We shape our glands to take the voice of <b>[]</b>, this will slow down regenerating chemicals while active.", mimic_voice)));
        to_chat(user, span_notice(I18n.T("Use this power again to return to our original voice and return chemical production to normal levels.")));
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/mimic_voice.dm:30
    public override async DmTask Remove(mob user = null)
    {
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (Dm.truthy(human_user.override_voice))
        {
            changeling?.chem_recharge_slowdown = max(0, changeling.chem_recharge_slowdown - 0.25f);
            human_user.override_voice = "";
            to_chat(user, span_notice(I18n.T("Our vocal glands return to their original position.")));
        }
        await base.Remove(user);
    }
}
