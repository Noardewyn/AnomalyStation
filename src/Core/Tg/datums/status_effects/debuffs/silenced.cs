// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/silenced.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): the silence (the sleeping carp's Kraken Wrack), whole.

// tg: code/datums/status_effects/debuffs/silenced.dm:1
public partial class datum_status_effect_silenced : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/silenced.dm:6
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/silenced.dm:10
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(clear_silence));
        ADD_TRAIT(owner, TRAIT_MUTE, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/silenced.dm:15
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        REMOVE_TRAIT(owner, TRAIT_MUTE, TRAIT_STATUS_EFFECT(id));
        return null;
    }

    /// Signal proc that clears any silence we have (self-deletes).
    // tg: code/datums/status_effects/debuffs/silenced.dm:20
    [SignalHandler]
    public virtual void clear_silence(mob_living source = null)
    {
        qdel(this);
    }
}
