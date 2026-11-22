// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/signals/signals_atom/signals_atom_x_act.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like signal macros COMSIG_ATOM_TOOL_ACT and COMSIG_ATOM_SECONDARY_TOOL_ACT (M4a P1: tool_act,
// game/atom/atom_tool_acts.dm; rules 11.1); the constant signals of the file are generated into Defines.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/dcs/signals/signals_atom/signals_atom_x_act.dm:74  #define COMSIG_ATOM_TOOL_ACT(tooltype) "tool_act_[tooltype]"
    public static string COMSIG_ATOM_TOOL_ACT(object tooltype) => Dm.text("tool_act_[]", tooltype);

    // tg: code/__DEFINES/dcs/signals/signals_atom/signals_atom_x_act.dm:78  #define COMSIG_ATOM_SECONDARY_TOOL_ACT(tooltype) "tool_secondary_act_[tooltype]"
    public static string COMSIG_ATOM_SECONDARY_TOOL_ACT(object tooltype) => Dm.text("tool_secondary_act_[]", tooltype);
}
