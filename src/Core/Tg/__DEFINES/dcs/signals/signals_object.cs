// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/signals/signals_object.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like signal macro COMSIG_TRAITOR_ITEM_USED (syndicate A2: the uplink's refunds, uplink_reimburse.dm;
// rules 11.1); the constant signals of the file are generated into Defines.

public static partial class GlobalProcs
{
    /// Called when a holoparasite/guardiancreator is used.
    // tg: code/__DEFINES/dcs/signals/signals_object.dm:314  #define COMSIG_TRAITOR_ITEM_USED(type) "traitor_item_used_[type]"
    public static string COMSIG_TRAITOR_ITEM_USED(object type) => Dm.text("traitor_item_used_[]", type);
}
