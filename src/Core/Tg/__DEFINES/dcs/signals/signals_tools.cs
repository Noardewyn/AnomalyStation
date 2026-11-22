// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/signals/signals_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like signal macros COMSIG_TOOL_ATOM_ACTED_PRIMARY and COMSIG_TOOL_ATOM_ACTED_SECONDARY (M4a P1:
// tool_act, game/atom/atom_tool_acts.dm; rules 11.1); the constant signals of the file are generated into Defines.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/dcs/signals/signals_tools.dm:5  #define COMSIG_TOOL_ATOM_ACTED_PRIMARY(tooltype) "tool_atom_acted_[tooltype]"
    public static string COMSIG_TOOL_ATOM_ACTED_PRIMARY(object tooltype) => Dm.text("tool_atom_acted_[]", tooltype);

    // tg: code/__DEFINES/dcs/signals/signals_tools.dm:7  #define COMSIG_TOOL_ATOM_ACTED_SECONDARY(tooltype) "tool_atom_acted_[tooltype]"
    public static string COMSIG_TOOL_ATOM_ACTED_SECONDARY(object tooltype) => Dm.text("tool_atom_acted_[]", tooltype);
}
