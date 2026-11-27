// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/slime/slime_food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the pheromones of a monkey the slime console spits out). The var block is generated (D111).

///Adds pheromones to a mob. If the target slime drains the mob to death, they might befriend the user
// tg: code/datums/status_effects/debuffs/slime/slime_food.dm:2
public partial class datum_status_effect_slime_food : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/slime/slime_food.dm:8
    [DmParams("new_owner", "feeder", "befriend_chance")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        mob_living_carbon_human feeder = Dm.arg(__extra, 0) as mob_living_carbon_human;
        object befriend_chance = Dm.arg(__extra, 1) ?? 100;
        this.befriend_chance = Dm.num(befriend_chance);
        this.feeder = feeder;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/debuffs/slime/slime_food.dm:13
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (feeder == null)
            return FALSE;

        if (!ishuman(feeder)) //don't give the AI pheromones
            return FALSE;

        RegisterSignal(feeder, COMSIG_QDELETING, nameof(on_feeder_deleted));
        RegisterSignal(owner, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_feeder_deleted));
        RegisterSignal(owner, COMSIG_SLIME_DRAINED, nameof(on_drained));
        RegisterSignal(owner, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/slime/slime_food.dm:26
    public override async DmTask<object> on_remove()
    {
        feeder = null;
        return null;
    }

    ///Handles the source of the pheromones getting deleted, or the owner getting washed
    // tg: code/datums/status_effects/debuffs/slime/slime_food.dm:30
    [SignalHandler]
    public virtual object on_feeder_deleted(datum source = null)
    {
        qdel(this);
        return COMPONENT_CLEANED;
    }

    ///Gaze upon the target
    // tg: code/datums/status_effects/debuffs/slime/slime_food.dm:38
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (HAS_TRAIT(user, TRAIT_ANOSMIA))
            return;
        if (get_dist(user, owner) > 1 && !Dm.truthy((user as mob_living_carbon)?.dna.get_mutation(typeof(datum_mutation_olfaction))))
            return;
        if (user == feeder)
            Dm.append(ref examine_list, span_boldnotice(I18n.T("Their smell reminds you of serenity and yourself.")));
        else
            Dm.append(ref examine_list, span_boldnotice(I18n.T("Their smell reminds you of serenity and [].", feeder)));
    }

    ///Handles a slime completely draining someone
    // tg: code/datums/status_effects/debuffs/slime/slime_food.dm:50
    [SignalHandler]
    public virtual void on_drained(datum source = null, mob_living_basic_slime draining_slime = null)
    {
        if (draining_slime == null || !isslime(draining_slime))
        {
            qdel(this);
            return;
        }

        if (!prob(befriend_chance) || Dm.truthy(((datum_ai_controller)draining_slime.ai_controller).blackboard[BB_SLIME_RABID]))
        {
            qdel(this);
            return;
        }

        Waiting.NoSleep(draining_slime.befriend(feeder));
        Dm.@new<obj_effect_temp_visual_heart>(draining_slime.loc);
        qdel(this);
    }
}
