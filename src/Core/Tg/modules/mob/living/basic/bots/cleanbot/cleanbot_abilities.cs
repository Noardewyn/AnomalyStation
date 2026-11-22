// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/cleanbot/cleanbot_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_bot : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_abilities.dm:7
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!isbot(owner))
            return TRUE;
        mob_living_basic_bot bot_owner = (mob_living_basic_bot)owner;
        if (Dm.truthy(Dm.bitand(bot_owner.bot_mode_flags, BOT_MODE_ON)))
            return TRUE;
        if (Dm.truthy(feedback))
            bot_owner.balloon_alert(bot_owner, "power off!");
        return FALSE;
    }
}

// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_abilities.dm:20
public partial class datum_action_cooldown_mob_cooldown_bot_foam : datum_action_cooldown_mob_cooldown_bot
{
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_abilities.dm:30
    public override async DmTask<object> Activate(atom target = null)
    {
        owner.visible_message(span_danger(Dm.text("[] whirs and bubbles violently, before releasing a plume of froth!", owner)));
        await do_foam(foam_range, owner, owner.loc as turf); // gaps F1
        StartCooldown();
        return TRUE;
    }
}
