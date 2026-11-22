// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/honkbots/honkbot_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_bot_honk : datum_action_cooldown_mob_cooldown_bot
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_abilities.dm:11
    public override async DmTask<object> Activate(atom target = null)
    {
        playsound(owner, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE, -1);
        if (post_honk_callback != null)
            await post_honk_callback.Invoke();
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_abilities.dm:17
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        post_honk_callback = null;
        return dot;
    }
}
