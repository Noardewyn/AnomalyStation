// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/wiremod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the circuits' datatype lists.

public partial class datum_controller_global_vars
{
    /// The basic player-facing types that don't have any super special behaviour.
    // tg: code/_globalvars/lists/wiremod.dm:2  GLOBAL_LIST_INIT(wiremod_basic_types, list(...))
    public DmList wiremod_basic_types;

    // tg: code/_globalvars/lists/wiremod.dm:2  (GLOBAL_MANAGED(wiremod_basic_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwiremod_basic_types()
    {
        wiremod_basic_types = Dm.list(
            PORT_TYPE_ANY,
            PORT_TYPE_ATOM,
            PORT_TYPE_NUMBER,
            PORT_TYPE_SIGNAL,
            PORT_TYPE_STRING,
            PORT_TYPE_TABLE);
        Dm.append(ref gvars_datum_init_order, "wiremod_basic_types");
    }

    /// The fundamental datatypes of the byond game engine.
    // tg: code/_globalvars/lists/wiremod.dm:12  GLOBAL_LIST_INIT(wiremod_fundamental_types, list(...))
    public DmList wiremod_fundamental_types;

    // tg: code/_globalvars/lists/wiremod.dm:12  (GLOBAL_MANAGED(wiremod_fundamental_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwiremod_fundamental_types()
    {
        wiremod_fundamental_types = Dm.list(
            PORT_TYPE_ANY,
            PORT_TYPE_ASSOC_LIST(PORT_TYPE_ANY, PORT_TYPE_ANY),
            PORT_TYPE_ATOM,
            PORT_TYPE_DATUM,
            PORT_TYPE_LIST(PORT_TYPE_ANY),
            PORT_TYPE_NUMBER,
            PORT_TYPE_STRING);
        Dm.append(ref gvars_datum_init_order, "wiremod_fundamental_types");
    }
}
