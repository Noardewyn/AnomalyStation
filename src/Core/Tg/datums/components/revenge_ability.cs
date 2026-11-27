// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/revenge_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Automatically triggers a linked ability at a target who attacks us.
 * The ability might not necessarily be on our mob.
 * Make sure that /datum/element/relay_attackers is also present or you'll never receive the triggering signal.
 */
// tg: code/datums/components/revenge_ability.dm:6
public partial class datum_component_revenge_ability : datum_component
{
    // tg: code/datums/components/revenge_ability.dm:21
    [DmParams("ability", "targeting", "min_range", "max_range", "target_self", "activate_with_mind")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_action_cooldown ability = Dm.arg<datum_action_cooldown>(__extra, 0);
        datum_targeting_strategy targeting = Dm.arg<datum_targeting_strategy>(__extra, 1);
        object min_range = Dm.arg(__extra, 2);
        object max_range = Dm.arg(__extra, 3);
        object target_self = Dm.arg(__extra, 4);
        object activate_with_mind = Dm.arg(__extra, 5);
        min_range ??= 0;
        max_range ??= INFINITY;
        target_self ??= FALSE;
        activate_with_mind ??= FALSE;
        object dot = await base.Initialize(Dm.forward(__extra, ability, targeting, min_range, max_range, target_self, activate_with_mind));
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.ability = ability;
        this.targeting = targeting;
        this.min_range = min_range;
        this.max_range = max_range;
        this.target_self = target_self;
        this.activate_with_mind = activate_with_mind;

        RegisterSignal(ability, COMSIG_QDELETING, nameof(ability_destroyed));
        return dot;
    }

    // tg: code/datums/components/revenge_ability.dm:34
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
    }

    // tg: code/datums/components/revenge_ability.dm:38
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_WAS_ATTACKED);
        if (ability != null)
            UnregisterSignal(ability, COMSIG_QDELETING);
        await base.UnregisterFromParent(__extra);
    }

    /// If we were attacked, get revenge
    // tg: code/datums/components/revenge_ability.dm:45
    [SignalHandler]
    public virtual void on_attacked(mob_living victim = null, atom attacker = null)
    {
        if (victim.mind != null && !Dm.truthy(activate_with_mind))
            return; // This is mostly a component for the use of AI
        atom ability_user = ability.owner;
        float distance = get_dist(ability_user, attacker);
        if (distance < Dm.num(min_range) || distance > Dm.num(max_range))
            return;
        if (targeting != null && !Dm.truthy(Waiting.NoSleep(targeting.is_valid_target(victim, attacker))))
            return;
        Waiting.Detach(ability.InterceptClickOn(ability_user as mob_living, null, Dm.truthy(target_self) ? ability_user : attacker));
    }

    /// For whatever reason we lost our linked ability so we can drop this behaviour
    // tg: code/datums/components/revenge_ability.dm:58
    [SignalHandler]
    public virtual void ability_destroyed(datum source = null)
    {
        qdel(this);
    }
}
