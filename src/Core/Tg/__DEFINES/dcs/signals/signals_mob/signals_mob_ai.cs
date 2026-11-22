// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/signals/signals_mob/signals_mob_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like macros of the file (the constants are generated); and COMSIG_AI_OVERRIDE_SLOT_CHANGED of signals_ai_controller.dm, the same family.
public static partial class GlobalProcs
{
    /// Signal sent when a blackboard key is set to a new value
    // tg: code/__DEFINES/dcs/signals/signals_mob/signals_mob_ai.dm:2
    public static string COMSIG_AI_BLACKBOARD_KEY_SET(object blackboard_key) => Dm.text("ai_blackboard_key_set_[]", blackboard_key);
    /// Signal sent before a blackboard key is cleared, return COMPONENT_BLOCK_... to keep it
    // tg: code/__DEFINES/dcs/signals/signals_mob/signals_mob_ai.dm:5
    public static string COMSIG_AI_BLACKBOARD_KEY_PRECLEAR(object blackboard_key) => Dm.text("ai_blackboard_key_pre_clear_[]", blackboard_key);
    /// Signal sent when a blackboard key is cleared
    // tg: code/__DEFINES/dcs/signals/signals_mob/signals_mob_ai.dm:8
    public static string COMSIG_AI_BLACKBOARD_KEY_CLEARED(object blackboard_key) => Dm.text("ai_blackboard_key_clear_[]", blackboard_key);
    // tg: code/__DEFINES/dcs/signals/signals_ai_controller.dm:9
    public static string COMSIG_AI_OVERRIDE_SLOT_CHANGED(object id) => Dm.text("ai_override_slot_changed_[]", id);
}
