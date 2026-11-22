// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/secbot/secbot_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/secbot/secbot_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_bot_sword : datum_action_cooldown_mob_cooldown_bot
{
    // tg: code/modules/mob/living/basic/bots/secbot/secbot_abilities.dm:8
    public override async DmTask<object> Activate(atom target = null)
    {
        obj_item_melee_energy_sword_saber my_sword = Dm.locate<obj_item_melee_energy_sword_saber>(owner);
        Waiting.Detach(my_sword.attack_self(owner));
        return TRUE;
    }
}
