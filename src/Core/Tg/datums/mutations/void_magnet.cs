// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/void_magnet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): The void magnet, a pocket of the void that sometimes pulls its owner in by itself. The var blocks are generated (D111).

// tg: code/datums/mutations/void_magnet.dm:1
public partial class datum_mutation_void : datum_mutation
{
    // tg: code/datums/mutations/void_magnet.dm:11
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_spell_void_cursed to_modify) // null or invalid
            return dot;

        to_modify.curse_probability_modifier = GET_MUTATION_SYNCHRONIZER(this);
        return dot;
    }
}

/// The base "void invocation" action. No side effects.
// tg: code/datums/mutations/void_magnet.dm:21
public partial class datum_action_cooldown_spell_void : datum_action_cooldown_spell
{
    // tg: code/datums/mutations/void_magnet.dm:34
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return isturf(cast_on.loc) ? TRUE : FALSE;
    }

    // tg: code/datums/mutations/void_magnet.dm:37
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        Dm.@new<obj_effect_immortality_talisman_void>(get_turf(cast_on), cast_on);
        return dot;
    }
}

/// The cursed "void invocation" action, that has a chance of casting itself on its owner randomly on life ticks.
// tg: code/datums/mutations/void_magnet.dm:42
public partial class datum_action_cooldown_spell_void_cursed : datum_action_cooldown_spell_void
{
    // tg: code/datums/mutations/void_magnet.dm:48
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (owner == null)
            return dot;

        RegisterSignal(grant_to, COMSIG_LIVING_LIFE, nameof(on_life));
        return dot;
    }

    // tg: code/datums/mutations/void_magnet.dm:55
    public override async DmTask Remove(mob remove_from = null)
    {
        UnregisterSignal(remove_from, COMSIG_LIVING_LIFE);
        await base.Remove(remove_from);
    }

    /// Signal proc for [COMSIG_LIVING_LIFE]. Has a chance of casting itself randomly.
    // tg: code/datums/mutations/void_magnet.dm:60
    [SignalHandler]
    public virtual void on_life(mob_living source = null, object seconds_per_tick = null)
    {
        if (HAS_TRAIT(source, TRAIT_STASIS) || source.stat == DEAD)
            return;

        if (!Dm.truthy(Waiting.NoSleep(is_valid_target(source))))
            return;

        float prob_of_curse = 0.25f;

        if (source is mob_living_carbon carbon_source && carbon_source.dna != null)
        {
            // If we have DNA, the probability of curse changes based on how stable we are
            prob_of_curse += (100 - carbon_source.dna.stability) / 40;
        }

        prob_of_curse *= curse_probability_modifier;

        if (!SPT_PROB(prob_of_curse, seconds_per_tick))
            return;

        Waiting.NoSleep(cast(source));
    }
}
