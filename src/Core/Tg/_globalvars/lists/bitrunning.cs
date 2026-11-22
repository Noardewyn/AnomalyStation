// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/bitrunning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the grades of the quantum console's domain list (SSbitrunning.get_available_domains) and
// of the server's best grade (virtual_domain.dm's submit_grade).

public partial class datum_controller_global_vars
{
    /// Assoc list of bitrunning grades in ascending order and their associated fontawesome icons.
    // tg: code/_globalvars/lists/bitrunning.dm:2  GLOBAL_LIST_INIT(bitrunning_grades, list(...))
    public DmList bitrunning_grades;

    // tg: code/_globalvars/lists/bitrunning.dm:2  (GLOBAL_MANAGED(bitrunning_grades, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbitrunning_grades()
    {
        bitrunning_grades = Dm.list(
            Dm.kv(BITRUNNING_GRADE_NONE, FA_ICON_MINUS),
            Dm.kv(BITRUNNING_GRADE_D, FA_ICON_D),
            Dm.kv(BITRUNNING_GRADE_C, FA_ICON_C),
            Dm.kv(BITRUNNING_GRADE_B, FA_ICON_B),
            Dm.kv(BITRUNNING_GRADE_A, FA_ICON_A),
            Dm.kv(BITRUNNING_GRADE_S, FA_ICON_S));
        Dm.append(ref gvars_datum_init_order, "bitrunning_grades");
    }
}
