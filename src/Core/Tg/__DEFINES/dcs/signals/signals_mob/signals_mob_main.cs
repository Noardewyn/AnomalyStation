// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/signals/signals_mob/signals_mob_main.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like macros COMSIG_MOB_EMOTE_SOUND (M2 P2: the tongue registers it at insertion) and COMSIG_MOB_EMOTED (M8 slice F: the sad trombone implant) translated;
// the constants of signals_mob_main.dm are generated into Defines.

public static partial class GlobalProcs
{
    ///from base of datum/emote/get_sound(): (key, list/sounds)
    // tg: code/__DEFINES/dcs/signals/signals_mob/signals_mob_main.dm:167  #define COMSIG_MOB_EMOTE_SOUND(key) "mob_emote_sound_[key]"
    public static object COMSIG_MOB_EMOTE_SOUND(object key) => Dm.text("mob_emote_sound_[]", key);

    ///from base of /datum/emote/run_emote(): (key, params, type_override, intentional, emote)
    // tg: code/__DEFINES/dcs/signals/signals_mob/signals_mob_main.dm:183  #define COMSIG_MOB_EMOTED(emote_key) "mob_emoted_[emote_key]"
    public static string COMSIG_MOB_EMOTED(object emote_key) => Dm.text("mob_emoted_[]", emote_key);
}
