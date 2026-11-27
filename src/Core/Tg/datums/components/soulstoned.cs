// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/soulstoned.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a shade held in a soulstone (godmode, immobile, no hands) until it leaves it. The var block is generated (D111).

//adds godmode while in the container, prevents moving, and clears these effects up after leaving the stone
// tg: code/datums/components/soulstoned.dm:2
public partial class datum_component_soulstoned : datum_component
{
    // tg: code/datums/components/soulstoned.dm:5
    [DmParams("container")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom_movable container = Dm.arg<atom_movable>(__extra, 0);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        mob_living stoned = parent as mob_living;

        this.container = container;

        await stoned.forceMove(container);
        await stoned.fully_heal();
        stoned.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), SOULSTONE_TRAIT);

        RegisterSignal(stoned, COMSIG_MOVABLE_MOVED, nameof(free_prisoner));
        return null;
    }

    // tg: code/datums/components/soulstoned.dm:18
    [SignalHandler]
    public virtual void free_prisoner()
    {
        mob_living stoned = parent as mob_living;
        if (stoned.loc != container)
            qdel(this);
    }

    // tg: code/datums/components/soulstoned.dm:25
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        mob_living stoned = parent as mob_living;
        stoned.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), SOULSTONE_TRAIT);
        await DmTask.CompletedTask;
    }
}
