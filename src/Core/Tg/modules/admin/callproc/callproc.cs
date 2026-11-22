// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/callproc/callproc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the admin proc-call globals and IsAdminAdvancedProcCall translated (the configuration guards its writes with
// it, boot package, wave 3), and the /mob/proccall_handler type declared (its [DmPath]: this file declares it first, rules
// 2.3) for the type of GLOB.AdminProcCallHandler. The admin proc-call verbs and wrappers are admin tooling (M10).

public partial class datum_controller_global_vars
{
    // tg: code/modules/admin/callproc/callproc.dm:1  GLOBAL_DATUM_INIT(AdminProcCallHandler, /mob/proccall_handler, new())
    // Its InitGlobalAdminProcCallHandler (and the GLOBAL_PROTECT override of :2) is not translated yet: it creates a mob in
    // nullspace, which needs the atom wave's /atom/New; typesof(/datum/controller/global_vars/proc) skips it (DV9), so the
    // var stays null and IsAdminAdvancedProcCall reads FALSE from its second half, as it does in tg until an admin calls.
    public mob_proccall_handler AdminProcCallHandler;

    // tg: code/modules/admin/callproc/callproc.dm:170  GLOBAL_VAR(AdminProcCaller)
    public object AdminProcCaller;

    // tg: code/modules/admin/callproc/callproc.dm:170  (GLOBAL_UNMANAGED(AdminProcCaller), __DEFINES/__globals.dm:11; the earlier
    // of two definitions on this type, rules 7.5)
    public void InitGlobalAdminProcCaller__d0()
    {
        return;
    }

    // tg: code/modules/admin/callproc/callproc.dm:171  GLOBAL_PROTECT(AdminProcCaller) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobalAdminProcCaller()
    {
        InitGlobalAdminProcCaller__d0();
        gvars_datum_protected_varlist["AdminProcCaller"] = TRUE;
    }

    // tg: code/modules/admin/callproc/callproc.dm:172  GLOBAL_VAR_INIT(AdminProcCallCount, 0)
    public float AdminProcCallCount;

    // tg: code/modules/admin/callproc/callproc.dm:172  (GLOBAL_MANAGED(AdminProcCallCount, 0), __DEFINES/__globals.dm:5)
    public void InitGlobalAdminProcCallCount__d0()
    {
        AdminProcCallCount = 0;
        Dm.append(ref gvars_datum_init_order, "AdminProcCallCount");
    }

    // tg: code/modules/admin/callproc/callproc.dm:173  GLOBAL_PROTECT(AdminProcCallCount)
    public virtual void InitGlobalAdminProcCallCount()
    {
        InitGlobalAdminProcCallCount__d0();
        gvars_datum_protected_varlist["AdminProcCallCount"] = TRUE;
    }

    // tg: code/modules/admin/callproc/callproc.dm:174  GLOBAL_VAR(LastAdminCalledTargetRef)
    public object LastAdminCalledTargetRef;

    // tg: code/modules/admin/callproc/callproc.dm:174  (GLOBAL_UNMANAGED(LastAdminCalledTargetRef))
    public void InitGlobalLastAdminCalledTargetRef__d0()
    {
        return;
    }

    // tg: code/modules/admin/callproc/callproc.dm:175  GLOBAL_PROTECT(LastAdminCalledTargetRef)
    public virtual void InitGlobalLastAdminCalledTargetRef()
    {
        InitGlobalLastAdminCalledTargetRef__d0();
        gvars_datum_protected_varlist["LastAdminCalledTargetRef"] = TRUE;
    }

    // tg: code/modules/admin/callproc/callproc.dm:176  GLOBAL_VAR(LastAdminCalledTarget)
    public object LastAdminCalledTarget;

    // tg: code/modules/admin/callproc/callproc.dm:176  (GLOBAL_UNMANAGED(LastAdminCalledTarget))
    public void InitGlobalLastAdminCalledTarget__d0()
    {
        return;
    }

    // tg: code/modules/admin/callproc/callproc.dm:177  GLOBAL_PROTECT(LastAdminCalledTarget)
    public virtual void InitGlobalLastAdminCalledTarget()
    {
        InitGlobalLastAdminCalledTarget__d0();
        gvars_datum_protected_varlist["LastAdminCalledTarget"] = TRUE;
    }

    // tg: code/modules/admin/callproc/callproc.dm:178  GLOBAL_VAR(LastAdminCalledProc)
    public object LastAdminCalledProc;

    // tg: code/modules/admin/callproc/callproc.dm:178  (GLOBAL_UNMANAGED(LastAdminCalledProc))
    public void InitGlobalLastAdminCalledProc__d0()
    {
        return;
    }

    // tg: code/modules/admin/callproc/callproc.dm:179  GLOBAL_PROTECT(LastAdminCalledProc)
    public virtual void InitGlobalLastAdminCalledProc()
    {
        InitGlobalLastAdminCalledProc__d0();
        gvars_datum_protected_varlist["LastAdminCalledProc"] = TRUE;
    }
}

/// Used to handle proccalls called indirectly by an admin (e.g. tgs, circuits).
/// Has to be a mob because IsAdminAdvancedProcCall() checks usr, which is a mob variable.
/// So usr is set to this for any proccalls that don't have any usr mob/client to refer to.
// tg: code/modules/admin/callproc/callproc.dm:8  (the type only: its vars and procs are admin tooling, M10)
[DmPath("/mob/proccall_handler")]
public partial class mob_proccall_handler : mob
{
}

public static partial class GlobalProcs
{
    // tg: code/modules/admin/callproc/callproc.dm:226
    public static object IsAdminAdvancedProcCall()
    {
        // tg: #ifdef TESTING ... #else: TESTING is not defined in production (rules 1.7)
        return Dm.num((Dm.truthy(GLOB.AdminProcCaller) && Dm.eq(GLOB.AdminProcCaller, usr?.client?.ckey)) || (Dm.truthy(GLOB.AdminProcCallHandler) && usr == GLOB.AdminProcCallHandler));
    }
}
