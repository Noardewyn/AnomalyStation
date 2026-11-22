// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/chameleon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Chameleon, the skin fading while the owner stands still. The var blocks are generated (D111).

//Chameleon causes the owner to slowly become transparent when not moving.
// tg: code/datums/mutations/chameleon.dm:2
public partial class datum_mutation_chameleon : datum_mutation
{
    // tg: code/datums/mutations/chameleon.dm:12
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        owner.alpha = CHAMELEON_MUTATION_DEFAULT_TRANSPARENCY;
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(owner, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_attack_hand));
        return dot;
    }

    // tg: code/datums/mutations/chameleon.dm:20
    public override DmTask on_life(object seconds_per_tick = null)
    {
        owner.alpha = Dm.max(Dm.num(owner.alpha) - (12.5f * GET_MUTATION_POWER(this) * Dm.num(seconds_per_tick)), 0);
        return DmTask.CompletedTask;
    }

    /**
     * Resets the alpha of the host to the chameleon default if they move.
     */
    // tg: code/datums/mutations/chameleon.dm:39
    [SignalHandler]
    public virtual void on_move(atom_movable source = null, atom old_loc = null, object move_dir = null, object forced = null, DmList old_locs = null)
    {
        owner.alpha = CHAMELEON_MUTATION_DEFAULT_TRANSPARENCY;
    }

    /**
     * Resets the alpha of the host if they click on something nearby.
     */
    // tg: code/datums/mutations/chameleon.dm:53
    [SignalHandler]
    public virtual void on_attack_hand(mob_living_carbon_human source = null, atom target = null, object proximity = null, DmList modifiers = null)
    {
        if (!Dm.truthy(proximity)) //stops tk from breaking chameleon
            return;
        owner.alpha = CHAMELEON_MUTATION_DEFAULT_TRANSPARENCY;
    }

    // tg: code/datums/mutations/chameleon.dm:60
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        owner.alpha = 255;
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_LIVING_UNARMED_ATTACK));
        return dot;
    }
}
