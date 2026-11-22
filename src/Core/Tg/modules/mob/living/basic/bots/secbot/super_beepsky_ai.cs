// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/secbot/super_beepsky_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/secbot/super_beepsky_ai.dm:1
public partial class datum_ai_controller_basic_controller_bot_secbot_super_beepsky : datum_ai_controller_basic_controller_bot_secbot
{
    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky_ai.dm:3
    public override void on_target_set()
    {
        base.on_target_set();
        mob_living_basic_bot_secbot_grievous super_beeps = (mob_living_basic_bot_secbot_grievous)pawn;
        if (!Dm.truthy(super_beeps.sword_active))
            Waiting.Detach(super_beeps.weapon.attack_self(super_beeps));
        super_beeps.visible_message(Dm.text("<b>[]</b> points at []!", super_beeps, blackboard[BB_CURRENT_TARGET]));
    }
}
