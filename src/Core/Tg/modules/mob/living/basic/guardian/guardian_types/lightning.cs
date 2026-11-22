// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/lightning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// A reasonably durable guardian linked to you by a chain of lightning, zapping people who get between you
// tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:2
public partial class mob_living_basic_guardian_lightning : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:21
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        clear_chains();
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:25
    public override object Destroy(float? force = null)
    {
        clear_chains();
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:29
    public override async DmTask manifest_effects()
    {
        await base.manifest_effects();
        if (summoner == null)
            return;
        summoner_chain = await chain_to(summoner, max_range: INFINITY) as datum_component; // Functionally it's actually our leash range but admins might fuck with it
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:35
    public override async DmTask recall_effects()
    {
        await base.recall_effects();
        clear_chains();
    }

    /// Remove all of our chains
    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:40
    public virtual void clear_chains()
    {
        QDEL_NULL(ref summoner_chain);
        QDEL_LIST_ASSOC_VAL(enemy_chains);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:44
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || !Dm.truthy(validate_target(target)) || Dm.@in(target, enemy_chains))
            return dot;
        if (length(enemy_chains) == 2)
        {
            object old_target = enemy_chains[1];
            datum old_chain = enemy_chains[old_target] as datum;
            qdel(old_chain);
        }
        datum new_chain = await chain_to(target) as datum;
        RegisterSignal(new_chain, COMSIG_QDELETING, nameof(on_chain_deleted));
        LAZYADDASSOC(ref enemy_chains, target, new_chain);
        return dot;
    }

    /// Create a damaging lightning chain between ourselves and a target
    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:57
    public virtual async DmTask<object> chain_to(atom target = null, float? max_range = null)
    {
        max_range ??= 7;
        datum_component chain = await AddComponent(
            typeof(datum_component_damage_chain),
            Dm.named("linked_to", target),
            Dm.named("max_distance", max_range),
            Dm.named("beam_state", Dm.text("lightning[]", rand(1, 12))),
            Dm.named("beam_type", typeof(obj_effect_ebeam_chain)),
            Dm.named("validate_target", Dm.@new<datum_callback>(this, nameof(validate_target))),
            Dm.named("chain_damage_feedback", Dm.@new<datum_callback>(this, nameof(on_chain_zap)))
        ) as datum_component;
        return chain;
    }

    /// Handle losing our reference when we delete a chain
    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:70
    [SignalHandler]
    public virtual void on_chain_deleted(datum source = null)
    {
        foreach (object target in Dm.each(enemy_chains))
        {
            if (enemy_chains[target] != source)
                continue;
            Dm.remove(enemy_chains, target);
            return;
        }
    }

    /// Confirm whether something is valid to zap with lightning
    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:79
    public virtual object validate_target(atom target = null)
    {
        return Dm.num(isliving(target) && target != this && target != summoner && !Dm.truthy(shares_summoner(target as mob_living_basic_guardian)));
    }

    /// Called every few zaps by a chain
    // tg: code/modules/mob/living/basic/guardian/guardian_types/lightning.dm:83
    public virtual async DmTask on_chain_zap(mob_living target = null)
    {
        await target.electrocute_act(shock_damage: 0, source: "lightning chain");
        target.visible_message(
            span_danger(Dm.text("[] was shocked by the lightning chain!", target)),
            span_userdanger("You are shocked by the lightning chain!"),
            span_hear("You hear a heavy electrical crack.")
        );
    }
}
