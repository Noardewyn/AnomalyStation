// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/time_vars.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the station year of a PDA's date and of the IC timestamps). world.timeofday is the world's clock (D31).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/time_vars.dm:1  GLOBAL_VAR_INIT(year, time2text(world.timeofday, "YYYY", TIMEZONE_UTC))
    public object year;

    // tg: code/_globalvars/time_vars.dm:1  (GLOBAL_MANAGED(year, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalyear()
    {
        year = time2text(world.timeofday, "YYYY", TIMEZONE_UTC);
        Dm.append(ref gvars_datum_init_order, "year");
    }

    // tg: code/_globalvars/time_vars.dm:2  GLOBAL_VAR_INIT(year_integer, text2num(year)) // = 2013???
    public object year_integer;

    // tg: code/_globalvars/time_vars.dm:2  (GLOBAL_MANAGED(year_integer, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalyear_integer()
    {
        year_integer = text2num(year);
        Dm.append(ref gvars_datum_init_order, "year_integer");
    }
}
