// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/vibebot/vibebot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3). The piano is inert until instruments: the vibebot's song is a row.

// tg: code/modules/mob/living/basic/bots/vibebot/vibebot.dm:1
public partial class mob_living_basic_bot_vibebot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot.dm:23
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        grant_actions_by_list(Dm.list(Dm.kv(typeof(datum_action_cooldown_mob_cooldown_bot_vibe), BB_VIBEBOT_PARTY_ABILITY)));
        obj_item_instrument_piano_synth piano = Dm.@new<obj_item_instrument_piano_synth>(this);
        ((datum_ai_controller)ai_controller).set_blackboard_key(BB_SONG_INSTRUMENT, piano);
        update_appearance(UPDATE_ICON);
        return dot;
    }
}
