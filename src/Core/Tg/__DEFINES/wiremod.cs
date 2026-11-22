// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/wiremod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CI1, D339, D341): the function-like macros of the circuits (rules 11.1: GlobalProcs members); the constants are
// generated (Defines).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/wiremod.dm:1  #define COMPONENT_TRIGGERED_BY(trigger, port) (trigger.value && trigger == port)
    public static bool COMPONENT_TRIGGERED_BY(datum_port trigger, datum_port port) => Dm.truthy(trigger.value) && trigger == port;

    /// List datatype
    // tg: code/__DEFINES/wiremod.dm:38  #define PORT_TYPE_LIST(datatype) SSwiremod_composite.composite_datatype(PORT_COMPOSITE_TYPE_LIST, datatype)
    public static object PORT_TYPE_LIST(object datatype) => SSwiremod_composite?.composite_datatype(PORT_COMPOSITE_TYPE_LIST, datatype); // SSwiremod_composite?: GLOB's lists read it, and a test harness world builds GLOB without Master (rules 10.6)

    /// Associative List datatype. Derivative of list.
    // tg: code/__DEFINES/wiremod.dm:42  #define PORT_TYPE_ASSOC_LIST(key_datatype, datatype) SSwiremod_composite.composite_datatype(PORT_COMPOSITE_TYPE_ASSOC_LIST, key_datatype, datatype)
    public static object PORT_TYPE_ASSOC_LIST(object key_datatype, object datatype) => SSwiremod_composite?.composite_datatype(PORT_COMPOSITE_TYPE_ASSOC_LIST, key_datatype, datatype); // (rules 10.6, as above)

    /// The value that is sent whenever a component is simply sending a signal. This can be anything, and is currently the seconds since roundstart.
    // tg: code/__DEFINES/wiremod.dm:79  #define COMPONENT_SIGNAL (world.time / (1 SECONDS))
    public static float COMPONENT_SIGNAL => Dm.num(world.time) / (1 * SECONDS);
}
