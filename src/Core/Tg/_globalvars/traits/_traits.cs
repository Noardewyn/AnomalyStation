// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/traits/_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the movement type lists (movement_type_trait_to_flag, movement_type_addtrait_signals,
// movement_type_removetrait_signals) and the two procs that build them translated (M2 integration: the movetype handler of
// every movable reads them); traits_by_type and the trait name maps are the admin tooling's (M10).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/traits/_traits.dm:900  GLOBAL_LIST_INIT(movement_type_trait_to_flag, list(...))
    public DmList movement_type_trait_to_flag;

    // tg: code/_globalvars/traits/_traits.dm:900  (GLOBAL_MANAGED(movement_type_trait_to_flag, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmovement_type_trait_to_flag()
    {
        movement_type_trait_to_flag = Dm.list(
            Dm.kv(TRAIT_MOVE_GROUND, GROUND),
            Dm.kv(TRAIT_MOVE_FLYING, FLYING),
            Dm.kv(TRAIT_MOVE_VENTCRAWLING, VENTCRAWLING),
            Dm.kv(TRAIT_MOVE_FLOATING, FLOATING),
            Dm.kv(TRAIT_MOVE_PHASING, PHASING),
            Dm.kv(TRAIT_MOVE_UPSIDE_DOWN, UPSIDE_DOWN)
        );
        Dm.append(ref gvars_datum_init_order, "movement_type_trait_to_flag");
    }

    // tg: code/_globalvars/traits/_traits.dm:909  GLOBAL_LIST_INIT(movement_type_addtrait_signals, set_movement_type_addtrait_signals())
    public DmList movement_type_addtrait_signals;

    // tg: code/_globalvars/traits/_traits.dm:909  (GLOBAL_MANAGED(movement_type_addtrait_signals, ...))
    public virtual void InitGlobalmovement_type_addtrait_signals()
    {
        movement_type_addtrait_signals = set_movement_type_addtrait_signals() as DmList;
        Dm.append(ref gvars_datum_init_order, "movement_type_addtrait_signals");
    }

    // tg: code/_globalvars/traits/_traits.dm:910  GLOBAL_LIST_INIT(movement_type_removetrait_signals, set_movement_type_removetrait_signals())
    public DmList movement_type_removetrait_signals;

    // tg: code/_globalvars/traits/_traits.dm:910  (GLOBAL_MANAGED(movement_type_removetrait_signals, ...))
    public virtual void InitGlobalmovement_type_removetrait_signals()
    {
        movement_type_removetrait_signals = set_movement_type_removetrait_signals() as DmList;
        Dm.append(ref gvars_datum_init_order, "movement_type_removetrait_signals");
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/traits/_traits.dm:912
    public static object set_movement_type_addtrait_signals(object signal_prefix = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object trait in Dm.each(GLOB.movement_type_trait_to_flag))
            Dm.append(ref dot, SIGNAL_ADDTRAIT(trait));

        return dot;
    }

    // tg: code/_globalvars/traits/_traits.dm:919
    public static object set_movement_type_removetrait_signals(object signal_prefix = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (object trait in Dm.each(GLOB.movement_type_trait_to_flag))
            Dm.append(ref dot, SIGNAL_REMOVETRAIT(trait));

        return dot;
    }
}
