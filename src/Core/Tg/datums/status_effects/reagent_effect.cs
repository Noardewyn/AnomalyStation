// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/reagent_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RG2, D339): the status effects tied to a reagent in the mob, zombie powder's fake death and cryogelidia's
// (the inverse of cryostylane) ice block. The var blocks are generated (D111).

/// Status effect that is tied to the existence of a reagent in a mob's system
// tg: code/datums/status_effects/reagent_effect.dm:3
public partial class datum_status_effect_reagent_effect : datum_status_effect
{
    // tg: code/datums/status_effects/reagent_effect.dm:12
    [DmParams("new_owner", "reagent_typepath", "subtypes_allowed")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object reagent_typepath = Dm.arg(__extra, 0);
        object subtypes_allowed = Dm.arg(__extra, 1);
        subtypes_allowed ??= TRUE;
        if (this.reagent_typepath == null)
        {
            if (reagent_typepath == null)
                stack_trace(Dm.text("Reagent effect [] created without a reagent typepath!", this), "code/datums/status_effects/reagent_effect.dm", 15);
            this.reagent_typepath = reagent_typepath;
        }
        if (this.subtypes_allowed == null)
            this.subtypes_allowed = subtypes_allowed;
        return await base.on_creation(new_owner, Dm.forward(__extra, reagent_typepath, subtypes_allowed));
    }

    // tg: code/datums/status_effects/reagent_effect.dm:21
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner.reagents == null || reagent_typepath == null || !Dm.truthy(can_effect()))
            return FALSE;

        RegisterSignal((datum)owner.reagents, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(check_reagents));
        await add_effect();
        return TRUE;
    }

    // tg: code/datums/status_effects/reagent_effect.dm:29
    public override async DmTask<object> on_remove()
    {
        remove_effect();
        UnregisterSignal((datum)owner.reagents, COMSIG_REAGENTS_HOLDER_UPDATED);
        return null;
    }

    // tg: code/datums/status_effects/reagent_effect.dm:33
    [SignalHandler]
    public virtual void check_reagents(datum_reagents updated_reagents = null)
    {
        if (Dm.truthy((owner.reagents as datum_reagents)?.has_reagent(reagent_typepath, check_subtypes: Dm.num_or_null(subtypes_allowed))))
            return;
        qdel(this);
    }

    /// Can we add this effect to the owner?
    // tg: code/datums/status_effects/reagent_effect.dm:41
    public virtual object can_effect()
    {
        return TRUE;
    }

    /// Add the side effect to the owner
    // tg: code/datums/status_effects/reagent_effect.dm:45
    public virtual async DmTask add_effect()
    {
        return;
    }

    /// Remove the side effect from the owner
    // tg: code/datums/status_effects/reagent_effect.dm:49
    public virtual void remove_effect()
    {
        return;
    }
}

// tg: code/datums/status_effects/reagent_effect.dm:53
public partial class datum_status_effect_reagent_effect_fakedeath : datum_status_effect_reagent_effect
{
    // tg: code/datums/status_effects/reagent_effect.dm:55
    public override async DmTask add_effect()
    {
        await owner.fakedeath(type);
    }

    // tg: code/datums/status_effects/reagent_effect.dm:58
    public override void remove_effect()
    {
        owner.cure_fakedeath(type);
    }
}

// tg: code/datums/status_effects/reagent_effect.dm:62
public partial class datum_status_effect_reagent_effect_freeze : datum_status_effect_reagent_effect
{
    // tg: code/datums/status_effects/reagent_effect.dm:64
    public override object can_effect()
    {
        return Dm.num(!HAS_TRAIT(owner, TRAIT_RESISTCOLD));
    }

    // tg: code/datums/status_effects/reagent_effect.dm:67
    public override async DmTask add_effect()
    {
        await owner.apply_status_effect(typeof(datum_status_effect_frozenstasis_irresistable));
        await owner.apply_status_effect(typeof(datum_status_effect_grouped_stasis), type);
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(escape_prison));
    }

    // tg: code/datums/status_effects/reagent_effect.dm:72
    public override void remove_effect()
    {
        owner.remove_status_effect(typeof(datum_status_effect_frozenstasis_irresistable));
        owner.remove_status_effect(typeof(datum_status_effect_grouped_stasis), type);
        UnregisterSignal(owner, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/datums/status_effects/reagent_effect.dm:77
    [SignalHandler]
    public virtual void escape_prison(params object[] __extra)
    {
        if (isturf(owner.loc) && owner.reagents is datum_reagents owner_reagents) // we escaped ice prison
            Waiting.NoSleep(owner_reagents.del_reagent(reagent_typepath));
        if (!QDELETED(this))
        {
            stack_trace(Dm.text("Despite nuking the reagent from the mob, [] still has []", owner, type), "code/datums/status_effects/reagent_effect.dm", 82);
            qdel(this);
        }
    }
}
