// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/regenerate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Regenerate (the limbs and organs regrown with the loud sound). The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/regenerate.dm:1
public partial class datum_action_changeling_regenerate : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/regenerate.dm:10
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        if (!iscarbon(user))
        {
            user.balloon_alert(user, I18n.T("nothing missing!"));
            return FALSE;
        }

        await base.sting_action(user, target);
        to_chat(user, span_notice(I18n.T("You feel an itching, both inside and outside as your tissues knit and reknit.")));
        mob_living_carbon carbon_user = user as mob_living_carbon;
        float got_limbs_back = Dm.num(length(carbon_user.get_missing_limbs()) >= 1);
        await carbon_user.fully_heal(HEAL_BODY);
        // Occurs after fully heal so the ling themselves can hear the sound effects (if deaf prior)
        if (Dm.truthy(got_limbs_back))
        {
            playsound(user, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);
            carbon_user.visible_message(
                span_warning(I18n.T("[]'s missing limbs reform, making a loud, grotesque sound!", user)),
                span_userdanger(I18n.T("Your limbs regrow, making a loud, crunchy sound and giving you great pain!")),
                span_hear(I18n.T("You hear organic matter ripping and tearing!")));
            await carbon_user.emote("scream");
        }

        return TRUE;
    }
}
