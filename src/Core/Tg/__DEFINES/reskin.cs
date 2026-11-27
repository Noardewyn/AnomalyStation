// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/reskin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The four var macros (APPLY_VAR_OR_RESET_INITIAL, APPLY_VAR_OR_RESET_TO, RESET_INITIAL_IF_SET, RESET_TO_IF_SET, :9-23) are
// expanded inline at their sites in datums/components/reskinnable_atom.cs (rules 11.1).

/// Global list of available atom skins
// tg: code/__DEFINES/reskin.dm:2  GLOBAL_LIST_EMPTY_TYPED(atom_skins, /datum/atom_skin)
public partial class datum_controller_global_vars
{
    public DmList atom_skins;

    // tg: code/__DEFINES/reskin.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalatom_skins()
    {
        atom_skins = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(atom_skins));
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/reskin.dm:4
    public static DmList get_atom_skins()
    {
        if (!Dm.truthy(Dm.length(GLOB.atom_skins)))
            GLOB.atom_skins = Waiting.NoSleep(init_subtypes_w_path_keys(typeof(datum_atom_skin))) as DmList;
        return GLOB.atom_skins;
    }
}
