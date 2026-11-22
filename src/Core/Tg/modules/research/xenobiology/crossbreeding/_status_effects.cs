// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_status_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the frozen stasis translated (gaps RG2, D339: cryogelidia, the inverse of cryostylane, freezes its patient in an
// ice block). The other crossbreeding status effects wait for xenobiology's crossbreeds. The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:93
public partial class datum_status_effect_frozenstasis : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:103
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        cube = Dm.@new<obj_structure_ice_stasis>(get_turf(owner));
        await owner.forceMove(cube);
        RegisterSignal(cube, COMSIG_QDELETING, nameof(clear_effect));
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(has_escaped));
        if (Dm.truthy(resistable))
            RegisterSignal(owner, COMSIG_LIVING_RESIST, nameof(clear_effect));
        ADD_TRAIT(owner, TRAIT_GODMODE, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:113
    [SignalHandler]
    public virtual void clear_effect(params object[] __extra)
    {
        qdel(this);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:118
    [SignalHandler]
    public virtual void has_escaped(params object[] __extra)
    {
        if (owner.loc != cube)
            qdel(this);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:124
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_GODMODE, TRAIT_STATUS_EFFECT(id));
        UnregisterSignal(owner, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(owner, COMSIG_LIVING_RESIST);
        UnregisterSignal(cube, COMSIG_QDELETING);
        QDEL_NULL(ref cube);
        return null;
    }
}
