// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/basic_mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The file's globals; its constants are generated (Defines).
public partial class datum_controller_global_vars
{
    // RULE-GAP: rules 3.2 types a var by its declaration's default; GLOBAL_VAR_INIT declares the var without one and assigns
    // its InitValue in InitGlobal. The InitValue (0) is taken as the default (V4).
    // tg: code/__DEFINES/basic_mobs.dm:41  GLOBAL_VAR_INIT(gutlunch_count, 0)
    public float gutlunch_count;

    // tg: code/__DEFINES/basic_mobs.dm:41  (GLOBAL_MANAGED(gutlunch_count, 0), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgutlunch_count()
    {
        gutlunch_count = 0;
        Dm.append(ref gvars_datum_init_order, "gutlunch_count");
    }
}
