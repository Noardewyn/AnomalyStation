// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/explosive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// A durable guardian which can convert objects into hidden explosives.
// tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:2
public partial class mob_living_basic_guardian_explosive : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        bomb = Dm.@new<datum_action_cooldown_mob_cooldown_explosive_booby_trap>(this);
        Waiting.NoSleep(bomb.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:20
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref bomb);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:24
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && Dm.truthy(proximity_flag) && isobj(attack_target) && Dm.truthy(can_unarmed_attack()))
        {
            await bomb.Trigger(null, null, attack_target);
            return null;
        }
        return await base.UnarmedAttack(attack_target, proximity_flag, modifiers);
    }
}


/// An ability which can turn an object into a bomb
// tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:32
public partial class datum_action_cooldown_mob_cooldown_explosive_booby_trap : datum_action_cooldown_mob_cooldown
{
    /// Static list of signals that activate the bomb.
    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:44
    private static DmList boom_signals => TgState.Current.datum_action_cooldown_mob_cooldown_explosive_booby_trap__boom_signals ??= Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_BUMPED, COMSIG_ATOM_ATTACK_HAND);

    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:46
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (!isobj(target))
            return FALSE;
        if (!Dm.truthy(await owner.Adjacent(target)))
            return FALSE;
        return await base.PreActivate(target);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:53
    public override async DmTask<object> Activate(atom target = null)
    {
        object glow_colour = COLOR_RED;
        mob_living_basic_guardian guardian_owner = owner as mob_living_basic_guardian;
        if (guardian_owner != null)
            glow_colour = guardian_owner.guardian_colour;
        await target.AddComponent(
            typeof(datum_component_direct_explosive_trap),
            Dm.named("saboteur", owner),
            Dm.named("expire_time", decay_time),
            Dm.named("glow_colour", glow_colour),
            Dm.named("explosive_checks", Dm.@new<datum_callback>(this, nameof(validate_target))),
            Dm.named("triggering_signals", boom_signals)
        );
        target.balloon_alert(owner, "bomb planted");
        StartCooldown();
        return TRUE;
    }

    /// Validate that we should blow up on this thing, preferably not on one of our allies
    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:71
    public virtual object validate_target(mob_living target = null)
    {
        if (target == owner)
            return FALSE;
        mob_living_basic_guardian guardian_owner = owner as mob_living_basic_guardian;
        if (guardian_owner == null)
            return TRUE;
        return Dm.num(target != guardian_owner.summoner && !Dm.truthy(guardian_owner.shares_summoner(target as mob_living_basic_guardian)));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/explosive.dm:44  var/static/list/boom_signals
    internal DmList datum_action_cooldown_mob_cooldown_explosive_booby_trap__boom_signals;
}
