// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/basic_mob_attack_telegraph.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * Delays outgoing attacks which are directed at mobs to give players time to get out of the way
 */
// tg: code/datums/components/basic_mob_attack_telegraph.dm:4
public partial class datum_component_basic_mob_attack_telegraph : datum_component
{
    // tg: code/datums/components/basic_mob_attack_telegraph.dm:14
    [DmParams("telegraph_icon", "telegraph_state", "display_telegraph_overlay", "telegraph_duration", "on_began_forecast")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object telegraph_icon = Dm.arg(__extra, 0);
        object telegraph_state = Dm.arg(__extra, 1);
        object display_telegraph_overlay = Dm.arg(__extra, 2);
        object telegraph_duration = Dm.arg(__extra, 3);
        datum_callback on_began_forecast = Dm.arg<datum_callback>(__extra, 4);
        telegraph_icon ??= Dm.resource("icons/mob/telegraphing/telegraph.dmi");
        telegraph_state ??= ATTACK_EFFECT_BITE;
        display_telegraph_overlay ??= TRUE;
        telegraph_duration ??= 0.4f * SECONDS;
        object dot = await base.Initialize(__extra);
        if (!isbasicmob(parent) && !ishostile(parent))
            return ELEMENT_INCOMPATIBLE;

        if (Dm.truthy(display_telegraph_overlay))
            target_overlay = mutable_appearance(telegraph_icon, telegraph_state as string) as mutable_appearance;

        this.telegraph_duration = telegraph_duration;
        this.on_began_forecast = on_began_forecast;
        return dot;
    }

    // tg: code/datums/components/basic_mob_attack_telegraph.dm:31
    public override object Destroy(float? force = null)
    {
        if (current_target != null)
            forget_target(current_target);
        target_overlay = null;
        on_began_forecast = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/basic_mob_attack_telegraph.dm:38
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_attack));
    }

    // tg: code/datums/components/basic_mob_attack_telegraph.dm:42
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (current_target != null)
            forget_target(current_target);
        QDEL_NULL(ref target_overlay);
        REMOVE_TRAIT(parent, TRAIT_BASIC_ATTACK_FORECAST, REF(this));
        UnregisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
        await base.UnregisterFromParent(__extra);
    }

    /// When we attempt to attack, check if it is allowed
    // tg: code/datums/components/basic_mob_attack_telegraph.dm:51
    [SignalHandler]
    public virtual object on_attack(mob_living source = null, atom target = null)
    {
        if (!(isliving(target) || ismecha(target))) // Curse you CLARKE
            return null;
        if (HAS_TRAIT_FROM(source, TRAIT_BASIC_ATTACK_FORECAST, REF(this)))
        {
            REMOVE_TRAIT(source, TRAIT_BASIC_ATTACK_FORECAST, REF(this));
            return null;
        }

        if (!Dm.truthy(DOING_INTERACTION(source, INTERACTION_BASIC_ATTACK_FORCEAST)))
            Waiting.Detach(delayed_attack(source, target)); // INVOKE_ASYNC
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    /// Perform an attack after a delay
    // tg: code/datums/components/basic_mob_attack_telegraph.dm:64
    public virtual async DmTask delayed_attack(mob_living source = null, atom target = null)
    {
        current_target = target as mob_living;

        if (target_overlay != null)
        {
            RegisterSignal(target, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_target_overlays_update));
            target.update_appearance();
        }

        RegisterSignal(target, COMSIG_QDELETING, nameof(forget_target));
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(target_moved));

        if (on_began_forecast != null)
            await on_began_forecast.Invoke(target);
        //we stop the do_after if the target moves out of neighboring turfs but if they dance around us they get their face smashed
        if (!Dm.truthy(await do_after(source, delay: Dm.num(telegraph_duration), target: target, timed_action_flags: IGNORE_TARGET_LOC_CHANGE, extra_checks: Dm.@new<datum_callback>(source, nameof(atom_movable.Adjacent), target), interaction_key: INTERACTION_BASIC_ATTACK_FORCEAST, cog_icon: null)))
        {
            forget_target(target);
            return;
        }
        if (target == null) // They got out of the way :(
            return;
        ADD_TRAIT(source, TRAIT_BASIC_ATTACK_FORECAST, REF(this));
        forget_target(target);
        if (isbasicmob(source))
        {
            mob_living_basic basic_source = source as mob_living_basic;
            await basic_source.melee_attack(target, ignore_cooldown: TRUE); // We already started the cooldown when we triggered the forecast
            return;
        }
        mob_living_simple_animal_hostile hostile_source = source as mob_living_simple_animal_hostile;
        await hostile_source.AttackingTarget(target);
    }

    /// The guy we're trying to attack moved, is he still in range?
    // tg: code/datums/components/basic_mob_attack_telegraph.dm:92
    [SignalHandler]
    public virtual void target_moved(atom target = null)
    {
        if (Dm.truthy(in_range(parent, target)))
            return;
        forget_target(target);
    }

    /// The guy we're trying to attack isn't a valid target any more
    // tg: code/datums/components/basic_mob_attack_telegraph.dm:99
    [SignalHandler]
    public virtual void forget_target(atom target = null)
    {
        current_target = null;
        target.update_appearance();
        UnregisterSignal(target, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED, COMSIG_ATOM_UPDATE_OVERLAYS));
    }

    // tg: code/datums/components/basic_mob_attack_telegraph.dm:105
    [SignalHandler]
    public virtual void on_target_overlays_update(atom parent_atom = null, DmList overlays = null)
    {
        if (parent_atom == current_target)
            Dm.append(overlays, target_overlay);
    }
}
