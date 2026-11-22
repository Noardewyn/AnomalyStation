// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/wires.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the signal macros COMSIG_CUT_WIRE and COMSIG_MEND_WIRE (M3 P3: the wires' cut and the airlock's power
// wires); the file's constants are emitted by the defines converter.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/wires.dm:2  #define COMSIG_CUT_WIRE(wire) "cut_wire [wire]"
    public static string COMSIG_CUT_WIRE(object wire) => Dm.text("cut_wire []", wire);

    // tg: code/__DEFINES/wires.dm:3  #define COMSIG_MEND_WIRE(wire) "mend_wire [wire]"
    public static string COMSIG_MEND_WIRE(object wire) => Dm.text("mend_wire []", wire);
}
