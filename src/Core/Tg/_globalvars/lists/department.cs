// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/department.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/_globalvars/lists/department.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/department.dm:1  GLOBAL_LIST_INIT(security_depts_prefs, sort_list(list(...)))
    public DmList security_depts_prefs;

    // tg: code/_globalvars/lists/department.dm:1  (GLOBAL_MANAGED(security_depts_prefs, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsecurity_depts_prefs()
    {
        security_depts_prefs = sort_list(Dm.list(SEC_DEPT_ENGINEERING, SEC_DEPT_MEDICAL, SEC_DEPT_NONE, SEC_DEPT_SCIENCE, SEC_DEPT_SUPPLY)) as DmList;
        Dm.append(ref gvars_datum_init_order, "security_depts_prefs");
    }
}
