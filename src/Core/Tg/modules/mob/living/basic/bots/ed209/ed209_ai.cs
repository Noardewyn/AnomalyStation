// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/ed209/ed209_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/ed209/ed209_ai.dm:4
public partial class datum_ai_controller_basic_controller_bot_ed209 : datum_ai_controller_basic_controller_bot
{
    // tg: code/modules/mob/living/basic/bots/ed209/ed209_ai.dm:20
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_SET(BB_CURRENT_TARGET), nameof(on_target_set));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209_ai.dm:27
    [SignalHandler]
    public virtual void on_target_set()
    {
        datum_action_cooldown_bot_announcement announcement = blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        mob_living_basic_bot_secbot_ed209 my_bot = (mob_living_basic_bot_secbot_ed209)pawn;
        DmList final_list = Dm.truthy(my_bot.sheriffized)
            ? Dm.list("Why y'all causin trouble in my town?", "Fill your hands, you son of a bitch.", "Aint nobody causin trouble in MY jurisdiction.")
            : Dm.list("Scumbag alert!", "Threat detected!");
        announcement.announce(pick(final_list));
    }
}
