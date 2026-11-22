// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/_flags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The constants of _flags.dm are generated into Defines; this file holds its one global, GLOB.bitflags (:4), which the
// Master's Loop reads (controllers/master.dm:625), and the macros other files need as members (rules 11.1: M3 P4 added
// ADD_KEEP_TOGETHER and REMOVE_KEEP_TOGETHER for atom_color.dm). The [DmPath] of /datum/controller/global_vars stays on
// controllers/globals.cs: a declaration that only a GLOBAL_* macro expansion produces does not place it (rules 2.3).

// tg: code/__DEFINES/_flags.dm:4  GLOBAL_LIST_INIT(bitflags, list(1, 2, 4, ..., 32768))
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__DEFINES/_flags.dm:4  (GLOBAL_RAW(/list/bitflags), __DEFINES/__globals.dm:32)
    public DmList bitflags;

    // tg: code/__DEFINES/_flags.dm:4  (GLOBAL_MANAGED(bitflags, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbitflags()
    {
        bitflags = Dm.list(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768);
        Dm.append(ref gvars_datum_init_order, "bitflags");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/_flags.dm:284
    public static void ADD_KEEP_TOGETHER(object x, object source)
    {
        if (Dm.truthy(Dm.bitand(((atom)x).appearance_flags, KEEP_TOGETHER)) && !HAS_TRAIT(x, TRAIT_KEEP_TOGETHER))
            ADD_TRAIT(x, TRAIT_KEEP_TOGETHER, KEEP_TOGETHER_ORIGINAL);
        ADD_TRAIT(x, TRAIT_KEEP_TOGETHER, source);
        ((atom)x).appearance_flags = Dm.bitor(((atom)x).appearance_flags, KEEP_TOGETHER);
    }

    // tg: code/__DEFINES/_flags.dm:289
    public static void REMOVE_KEEP_TOGETHER(object x, object source)
    {
        REMOVE_TRAIT(x, TRAIT_KEEP_TOGETHER, source);
        if (HAS_TRAIT_FROM_ONLY(x, TRAIT_KEEP_TOGETHER, KEEP_TOGETHER_ORIGINAL))
            REMOVE_TRAIT(x, TRAIT_KEEP_TOGETHER, KEEP_TOGETHER_ORIGINAL);
        else if (!HAS_TRAIT(x, TRAIT_KEEP_TOGETHER))
            ((atom)x).appearance_flags = Dm.bitand(((atom)x).appearance_flags, Dm.bitnot(KEEP_TOGETHER));
    }
}
