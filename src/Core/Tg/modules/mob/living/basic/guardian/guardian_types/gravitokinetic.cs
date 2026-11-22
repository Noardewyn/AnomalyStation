// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// Somewhat durable guardian who can increase gravity in an area
// tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:2
public partial class mob_living_basic_guardian_gravitokinetic : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:24  var/static/list/container_connections
    private static DmList container_connections => TgState.Current.mob_living_basic_guardian_gravitokinetic__container_connections ??= Dm.list(
        Dm.kv(COMSIG_MOVABLE_MOVED, nameof(on_moved)));

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_forced_gravity), 1));

        Waiting.NoSleep(AddComponent(typeof(datum_component_connect_containers), this, container_connections));
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:30
    public override async DmTask set_summoner(mob_living to_who = null, float? different_person = null)
    {
        await base.set_summoner(to_who, different_person);
        if (!QDELETED(this)) // tg's inverted check: the summoner never gets the forced gravity (kept)
            return;
        await to_who.AddElement(typeof(datum_element_forced_gravity), 1);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:36
    public override async DmTask cut_summoner(float? different_person = null)
    {
        summoner?.RemoveElement(typeof(datum_element_forced_gravity), 1);
        await base.cut_summoner(different_person);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:40
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        clear_gravity();
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:44
    public override async DmTask recall_effects()
    {
        await base.recall_effects();
        if (Dm.truthy(length(gravity_targets)))
            to_chat(this, span_bolddanger("You have released your gravitokinetic powers!"));
        clear_gravity();
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:50
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || !isliving(target) || target == this || target == summoner || Dm.truthy(shares_summoner(target as mob_living_basic_guardian)) || Dm.truthy(gravity_targets[target]))
            return dot;
        to_chat(this, span_bolddanger(Dm.text("Your punch has applied heavy gravity to []!", target)));
        await add_gravity(target, punch_gravity);
        to_chat(target, span_userdanger("Everything feels really heavy!"));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:59
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && Dm.truthy(proximity_flag) && !Dm.truthy(gravity_targets[attack_target]) && Dm.truthy(can_unarmed_attack()))
        {
            await slam_turf(attack_target as turf_open);
            return null;
        }
        return await base.UnarmedAttack(attack_target, proximity_flag, modifiers);
    }

    /// Apply forced gravity to the floor
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:66
    public virtual async DmTask slam_turf(turf_open slammed = null)
    {
        if (!isopenturf(slammed) || isgroundlessturf(slammed))
            return;
        visible_message(span_danger(Dm.text("[] slams their fist into the []!", this, slammed)), span_notice(Dm.text("You amplify gravity around the [].", slammed)));
        await do_attack_animation(slammed);
        await add_gravity(slammed, turf_gravity);
    }

    /// Remove our forced gravity from all targets
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:74
    public virtual void clear_gravity()
    {
        foreach (object gravity_target in Dm.each(gravity_targets.Copy()))
            remove_gravity(gravity_target as atom);
    }

    /// Make something heavier
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:79
    public virtual async DmTask add_gravity(atom target = null, float? new_gravity = null)
    {
        new_gravity ??= 3;
        if (Dm.truthy(gravity_targets[target]))
            return;
        await target.AddElement(typeof(datum_element_forced_gravity), new_gravity);
        gravity_targets[target] = new_gravity;
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_target_moved));
        playsound(this, Dm.resource("sound/effects/gravhit.ogg"), 100, TRUE);
    }

    /// Stop making something heavier
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:88
    public virtual void remove_gravity(atom target = null, float? too_far = null)
    {
        too_far ??= FALSE;
        if (gravity_targets[target] == null)
            return;
        if (Dm.truthy(too_far))
            to_chat(this, span_bolddanger(Dm.text("You are too far away from [] to amplify gravity's hold on them!", target)));
        UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
        target.RemoveElement(typeof(datum_element_forced_gravity), gravity_targets[target]);
        Dm.remove(gravity_targets, target);
    }

    /// When we or something we are inside move check if we are now too far away
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:98
    [SignalHandler]
    public virtual void on_moved()
    {
        foreach (object gravity_target in Dm.each(gravity_targets.Copy()))
        {
            if (Dm.num(get_dist(this, gravity_target)) > gravity_power_range)
                remove_gravity(gravity_target as atom, too_far: TRUE);
        }
    }

    /// When something we put gravity on moves check if it's too far away
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:104
    [SignalHandler]
    public virtual void on_target_moved(atom_movable moving_target = null, object old_loc = null, object dir = null, object forced = null)
    {
        if (Dm.num(get_dist(this, moving_target)) > gravity_power_range)
            remove_gravity(moving_target, too_far: TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/gravitokinetic.dm:24  var/static/list/container_connections
    internal DmList mob_living_basic_guardian_gravitokinetic__container_connections;
}
