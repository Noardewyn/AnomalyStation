// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/dna_transformation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the temporary DNA transformation, the changeling's transformation sting's pausing kind and the
// DNA console's makeup injector's kind. The var blocks are generated (D111).

/// Transforms a carbon mob into a new DNA for a set amount of time,
/// then turns them back to how they were before transformation.
// tg: code/datums/status_effects/debuffs/dna_transformation.dm:3
public partial class datum_status_effect_temporary_transformation : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:15
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force); // parent must be called first, so we clear DNA refs AFTER transforming back... yeah i know
        QDEL_NULL(ref new_dna);
        QDEL_NULL(ref old_dna);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:20
    [DmParams("new_owner", "new_duration", "dna_to_copy")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object new_duration = Dm.arg(__extra, 0) ?? 1 * MINUTES;
        datum_dna dna_to_copy = Dm.arg(__extra, 1) as datum_dna;
        if (!iscarbon(new_owner) || dna_to_copy == null)
        {
            qdel(this);
            return null;
        }

        this.duration = Dm.num(new_duration);
        this.new_dna = Dm.@new<datum_dna>();
        this.old_dna = Dm.@new<datum_dna>();
        await init_dna(new_owner as mob_living_carbon, dna_to_copy);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:31
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;

        mob_living_carbon transforming = owner as mob_living_carbon;
        if (!Dm.truthy(transforming.has_dna()))
            return FALSE;

        await save_dna();
        await apply_dna();
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:43
    public override async DmTask<object> on_remove()
    {
        mob_living_carbon transforming = owner as mob_living_carbon;

        if (!QDELING(owner)) // Don't really need to do appearance stuff if we're being deleted
        {
            await old_dna.copy_dna(transforming.dna, copy_dna_flags);
            await transforming.updateappearance(mutcolor_update: TRUE);
            await transforming.domutcheck();
        }

        transforming.real_name = old_dna.real_name; // Name is fine though
        transforming.name = Dm.str(transforming.get_visible_name());
        return null;
    }

    /// Called when initializing the DNA that the mob is transforming into
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:54
    public virtual async DmTask init_dna(mob_living_carbon new_owner = null, datum_dna dna_to_copy = null)
    {
        await dna_to_copy.copy_dna(new_dna, copy_dna_flags);
    }

    /// Called when saving the mob's DNA before transformation
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:58
    public virtual async DmTask save_dna()
    {
        mob_living_carbon transforming = owner as mob_living_carbon;
        await transforming.dna.copy_dna(old_dna, copy_dna_flags);
    }

    /// Applies the DNA to the mob
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:63
    public virtual async DmTask apply_dna()
    {
        mob_living_carbon transforming = owner as mob_living_carbon;
        await new_dna.copy_dna(transforming.dna, copy_dna_flags);
        transforming.real_name = new_dna.real_name;
        transforming.name = Dm.str(transforming.get_visible_name());
        await transforming.updateappearance(mutcolor_update: TRUE);
        await transforming.domutcheck();
    }
}

// tg: code/datums/status_effects/debuffs/dna_transformation.dm:72
public partial class datum_status_effect_temporary_transformation_trans_sting : datum_status_effect_temporary_transformation
{
    /// Signals which we react to to determine if we should pause the effect.
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:76  var/static/list/update_on_signals (a type static, rules 10.6)
    private static DmList update_on_signals => TgState.Current.datum_status_effect_temporary_transformation_trans_sting__update_on_signals ??= Dm.list(
        COMSIG_MOB_STATCHANGE,
        SIGNAL_ADDTRAIT(TRAIT_STASIS),
        SIGNAL_REMOVETRAIT(TRAIT_STASIS),
        SIGNAL_ADDTRAIT(TRAIT_DEATHCOMA),
        SIGNAL_REMOVETRAIT(TRAIT_DEATHCOMA));

    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:84
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignals(owner, update_on_signals, nameof(pause_effect));
        pause_effect(owner); // for if we sting a dead guy
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:91
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, update_on_signals);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:95
    [SignalHandler]
    public virtual void pause_effect(mob_living source = null, params object[] __extra)
    {
        // Pause if we're dead, appear dead, or in stasis
        if (IS_DEAD_OR_FAKING(source) || HAS_TRAIT(source, TRAIT_STASIS))
        {
            if (Dm.eq(duration, STATUS_EFFECT_PERMANENT))
                return; // Already paused

            time_before_pause = Dm.num(duration);
            duration = STATUS_EFFECT_PERMANENT;
        }

        // Resume if we're none of the above and also were paused
        else if (time_before_pause != -1)
        {
            duration = time_before_pause;
            time_before_pause = -1;
        }
    }
}

// tg: code/datums/status_effects/debuffs/dna_transformation.dm:112
public partial class datum_status_effect_temporary_transformation_dna_injector : datum_status_effect_temporary_transformation
{
    // when initting dna, any unset fields are copied from the mob's dna (so nothing changes effectively)
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:118
    public override async DmTask init_dna(mob_living_carbon new_owner = null, datum_dna dna_to_copy = null)
    {
        await base.init_dna(new_owner, dna_to_copy);
        if (!Dm.truthy(new_dna.real_name))
            new_dna.real_name = new_owner.dna.real_name;
        if (!Dm.truthy(new_dna.unique_enzymes))
            new_dna.unique_enzymes = new_owner.dna.unique_enzymes;
        if (!Dm.truthy(new_dna.unique_features))
            new_dna.unique_features = new_owner.dna.unique_features;
        if (!Dm.truthy(new_dna.unique_identity))
            new_dna.unique_identity = new_owner.dna.unique_identity;
        if (!Dm.truthy(new_dna.blood_type))
            new_dna.blood_type = new_owner.dna.blood_type;
        // just to put something there, it'll get updated if UF does anyways
        new_dna.features = new_owner.dna.features.Copy();
    }

    // ensure secondary transformation make a copy of the original dna (to prevent latter effects that expire earlier from returning to the wrong dna)
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:129
    public override async DmTask save_dna()
    {
        foreach (datum_status_effect_temporary_transformation_dna_injector other_effect in Dm.each<datum_status_effect_temporary_transformation_dna_injector>(owner.status_effects))
        {
            await other_effect.old_dna.copy_dna(this.old_dna, copy_dna_flags);
            return;
        }

        await base.save_dna();
    }

    // when the effect ends, see if there's any other active effects, and re-apply them if necessary
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:137
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        if (QDELING(owner))
            return dot;
        foreach (datum_status_effect_temporary_transformation_dna_injector other_effect in Dm.each<datum_status_effect_temporary_transformation_dna_injector>(owner.status_effects))
            await other_effect.apply_dna();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/debuffs/dna_transformation.dm:76  var/static/list/update_on_signals
    internal DmList datum_status_effect_temporary_transformation_trans_sting__update_on_signals;
}
