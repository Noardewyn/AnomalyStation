// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/temporary_body.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327, with Mafia: a ghost playing Mafia goes back to its old mind when the game's body goes). The var
// block is generated (D111).

/**
 * ## temporary_body
 *
 * Used on mobs when they are meant to be a 'temporary body'
 * Holds a reference to an old mind, to put them back in
 * once the body this component is attached to, is being deleted.
 */
// tg: code/datums/components/temporary_body.dm:8
public partial class datum_component_temporary_body : datum_component
{
    // tg: code/datums/components/temporary_body.dm:18
    [DmParams("old_mind", "return_on_death", "return_on_revive")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await DmTask.CompletedTask;
        datum_mind old_mind = Dm.arg(__extra, 0) as datum_mind;
        object return_on_death = Dm.arg(__extra, 1) ?? FALSE;
        object return_on_revive = Dm.arg(__extra, 2) ?? FALSE;
        if (!ismob(parent))
            return COMPONENT_INCOMPATIBLE;
        if (old_mind == null)
        {
            stack_trace("Tried to create a temporary_body component without an old_mind!", "code/datums/components/temporary_body.dm", 22);
            return COMPONENT_INCOMPATIBLE;
        }

        this.old_mind = old_mind;
        old_body = old_mind.current;
        this.return_on_death = Dm.num(return_on_death);
        this.return_on_revive = Dm.num(return_on_revive);
        return null;
    }

    // tg: code/datums/components/temporary_body.dm:30
    public override async DmTask RegisterWithParent()
    {
        await DmTask.CompletedTask;
        RegisterSignal(parent, COMSIG_QDELETING, nameof(return_mind));
        if (Dm.truthy(return_on_death))
            RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(return_mind));

        RegisterSignal(old_mind, COMSIG_MIND_TRANSFERRED, nameof(on_mind_transfer));

        if (old_body != null)
            register_body_signals();
    }

    // tg: code/datums/components/temporary_body.dm:40
    public virtual void register_body_signals()
    {
        ADD_TRAIT(old_body, TRAIT_MIND_TEMPORARILY_GONE, REF(this));
        RegisterSignal(old_body, COMSIG_QDELETING, nameof(on_body_destroy));
        if (Dm.truthy(return_on_revive))
            RegisterSignal(old_body, COMSIG_LIVING_REVIVE, nameof(return_mind));
    }

    // tg: code/datums/components/temporary_body.dm:46
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        UnregisterSignal(parent, COMSIG_QDELETING);
        UnregisterSignal(parent, COMSIG_LIVING_DEATH);
        UnregisterSignal(old_mind, COMSIG_MIND_TRANSFERRED);
        if (old_body != null)
            unregister_body_signals();
    }

    // tg: code/datums/components/temporary_body.dm:53
    public virtual void unregister_body_signals()
    {
        REMOVE_TRAIT(old_body, TRAIT_MIND_TEMPORARILY_GONE, REF(this));
        UnregisterSignal(old_body, COMSIG_QDELETING);
        UnregisterSignal(old_body, COMSIG_LIVING_REVIVE);
    }

    // tg: code/datums/components/temporary_body.dm:58
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        old_mind = null;
        old_body = null;
        return dot;
    }

    /// Swap the target of old_body if old_mind is transferred somewhere while we're away
    // tg: code/datums/components/temporary_body.dm:64
    [SignalHandler]
    public virtual void on_mind_transfer(params object[] __extra)
    {
        if (old_body != null)
            unregister_body_signals();
        old_body = old_mind.current;
        if (old_body != null)
            register_body_signals();
    }

    /**
     * Sends the mind of the temporary body back into their previous host
     * If the previous host is alive, we'll also force them into the body.
     * Otherwise we'll let them hang out as a ghost still.
     */
    // tg: code/datums/components/temporary_body.dm:78
    [SignalHandler]
    public virtual void return_mind(params object[] __extra)
    {
        mob new_body = parent as mob;
        mob_dead_observer ghost = (new_body.get_ghost() ?? new_body.ghostize(forced: TRUE)) as mob_dead_observer;
        if (QDELETED(ghost))
        {
            stack_trace(Dm.text("[] belonging to [] was completely unable to find a ghost to put back into a body!", this, parent), "code/datums/components/temporary_body.dm", 84);
            qdel(this); // i guess this is useless now
            return;
        }

        ghost.mind = old_mind;
        if (old_body != null)
        {
            if (old_mind.current != old_body)
            {
                stack_trace("Temporary body returning mind to old body, but the mind's current body doesn't match the old body!", "code/datums/components/temporary_body.dm", 91);
                old_mind.set_current(old_body);
            }
            if (old_body.stat != DEAD && !IS_FAKE_KEY(old_body.key))
                ghost.reenter_corpse();
        }

        qdel(this); // we're done here
    }

    /// Body reference handling
    // tg: code/datums/components/temporary_body.dm:99
    [SignalHandler]
    public virtual void on_body_destroy(params object[] __extra)
    {
        old_body = null;
    }
}
