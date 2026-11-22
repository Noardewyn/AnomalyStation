// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/reta.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps G3, D291: the request consoles register in reta_consoles_by_origin).

/// Used by RETA system - code/modules/reta/reta_system.dm
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/reta.dm:2  GLOBAL_LIST_EMPTY(reta_job_trims)
    public DmList reta_job_trims;

    // tg: code/_globalvars/reta.dm:2  (GLOBAL_MANAGED(reta_job_trims, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_job_trims()
    {
        reta_job_trims = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_job_trims");
    }

    // tg: code/_globalvars/reta.dm:3  GLOBAL_LIST_EMPTY(reta_dept_grants)
    public DmList reta_dept_grants;

    // tg: code/_globalvars/reta.dm:3  (GLOBAL_MANAGED(reta_dept_grants, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_dept_grants()
    {
        reta_dept_grants = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_dept_grants");
    }

    // tg: code/_globalvars/reta.dm:4  GLOBAL_LIST_EMPTY(reta_cooldown)
    public DmList reta_cooldown;

    // tg: code/_globalvars/reta.dm:4  (GLOBAL_MANAGED(reta_cooldown, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_cooldown()
    {
        reta_cooldown = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_cooldown");
    }

    // tg: code/_globalvars/reta.dm:5  GLOBAL_LIST_EMPTY(reta_recent_calls)
    public DmList reta_recent_calls;

    // tg: code/_globalvars/reta.dm:5  (GLOBAL_MANAGED(reta_recent_calls, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_recent_calls()
    {
        reta_recent_calls = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_recent_calls");
    }

    // tg: code/_globalvars/reta.dm:6  GLOBAL_LIST_EMPTY(reta_active_grants)
    public DmList reta_active_grants;

    // tg: code/_globalvars/reta.dm:6  (GLOBAL_MANAGED(reta_active_grants, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_active_grants()
    {
        reta_active_grants = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_active_grants");
    }

    // tg: code/_globalvars/reta.dm:7  GLOBAL_LIST_EMPTY(reta_consoles_by_origin)
    public DmList reta_consoles_by_origin;

    // tg: code/_globalvars/reta.dm:7  (GLOBAL_MANAGED(reta_consoles_by_origin, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_consoles_by_origin()
    {
        reta_consoles_by_origin = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_consoles_by_origin");
    }

    // tg: code/_globalvars/reta.dm:8  GLOBAL_LIST_EMPTY(reta_active_cards)
    public DmList reta_active_cards;

    // tg: code/_globalvars/reta.dm:8  (GLOBAL_MANAGED(reta_active_cards, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreta_active_cards()
    {
        reta_active_cards = Dm.list();
        Dm.append(ref gvars_datum_init_order, "reta_active_cards");
    }
}
