// Translated from tgstation (snapshot 1d99bd7f): code/modules/zombie/organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the zombie infection organ, which the claw's bite plants, which rots its host and, once the host
// is dead, raises him as a zombie (the zombified status effect, species_types/zombies.cs). The var block is generated.

// tg: code/modules/zombie/organs.dm:2
public partial class obj_item_organ_zombie_infection : obj_item_organ
{
    // tg: code/modules/zombie/organs.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (iscarbon(loc))
            Waiting.NoSleep(Insert(loc as mob_living_carbon));
        Dm.append(ref GLOB.zombie_infection_list, this);
        return dot;
    }

    // tg: code/modules/zombie/organs.dm:23
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.zombie_infection_list, this);
        return base.Destroy(force);
    }

    // tg: code/modules/zombie/organs.dm:27
    public override object feel_for_damage(object self_aware = null)
    {
        // keep stealthy for now, revisit later
        return "";
    }

    // tg: code/modules/zombie/organs.dm:31
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/zombie/organs.dm:35
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        STOP_PROCESSING(SSobj, this);
        if (Dm.truthy(timer_id))
            deltimer(timer_id);
        if (ghost_poll != null)
        {
            qdel(ghost_poll);
            ghost_poll = null;
        }
    }

    // tg: code/modules/zombie/organs.dm:42
    public override async DmTask on_find(mob_living finder = null)
    {
        to_chat(finder, span_warning(I18n.T("Inside the head is a disgusting black web of pus and viscera, bound tightly around the brain like some biological harness.")));
    }

    // tg: code/modules/zombie/organs.dm:47
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (owner == null)
            return null;
        if (!Dm.@in(this, owner.organs))
            Waiting.NoSleep(Remove(owner));
        if (!Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_ORGANIC)) || HAS_TRAIT(owner, TRAIT_UNHUSKABLE)) // does not process in inorganic things
            return null;
        if (Dm.truthy(causes_damage) && !Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_zombie))) && owner.stat != DEAD)
        {
            Waiting.NoSleep(owner.adjust_tox_loss(0.5f * Dm.num(seconds_per_tick)));
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(owner, span_danger(I18n.T("You feel sick...")));
        }
        if (Dm.truthy(timer_id) || HAS_TRAIT(owner, TRAIT_SUICIDED) || owner.get_organ_by_type(typeof(obj_item_organ_brain)) == null)
            return null;
        if (owner.stat != DEAD && !Dm.truthy(converts_living))
            return null;
        if (!Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_zombie))))
        {
            to_chat(owner, span_cult_large(I18n.T("You can feel your heart stopping, but something isn't right... life has not abandoned your broken form. You can only feel a deep and immutable hunger that not even death can stop, you will rise again!")));
        }
        float revive_time = rand(revive_time_min, revive_time_max);
        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(zombify), owner), revive_time, TIMER_STOPPABLE, file: "code/modules/zombie/organs.dm", line: 64);
        return null;
    }

    // tg: code/modules/zombie/organs.dm:70
    public virtual async DmTask zombify(mob_living_carbon target = null)
    {
        timer_id = null;

        if (!Dm.truthy(converts_living) && owner.stat != DEAD)
            return;

        // mindless zombies are nerfed a bit to encourage infecting real players instead of random morgue bodies
        await target.apply_status_effect(target.mind == null ? typeof(datum_status_effect_zombie_mindless) : typeof(datum_status_effect_zombie));

        bool stand_up = IS_UNCONSCIOUS(target);

        //Fully heal the zombie's damage the first time they rise
        if (!Dm.truthy(await target.heal_and_revive(0, span_danger(I18n.T("[] suddenly convulses, as [][] gain a ravenous hunger in [] eyes!", target, target.p_they(), stand_up ? I18n.T(" stagger to [] feet and", target.p_their()) : "", target.p_their())))))
            return;

        zombie_welcome(target);
        if (target.mind != null || target.client != null)
        {
            if (stand_up)
                target.get_up();
            return;
        }

        ghost_poll = WEAKREF(await target.AddComponent(typeof(datum_component_ghost_direct_control),
            Dm.named("ban_type", NONE),
            Dm.named("poll_ignore_key", POLL_IGNORE_RECOVERED_CREW),
            Dm.named("poll_question", I18n.T("Do you want to be a []?", span_green(target.mind == null ? I18n.T("mindless zombie") : I18n.T("zombie")))),
            Dm.named("role_name", "zombie"),
            Dm.named("extra_control_checks", Dm.@new<datum_callback>(this, nameof(check_ghost_control_eligibility))),
            Dm.named("after_assumed_control", Dm.@new<datum_callback>(this, nameof(after_ghost_control))),
            Dm.named("joinable_mobs_title", "Zombies")) as datum) as datum_weakref;
        SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_TOMBSTONE] = TRUE;
    }

    // tg: code/modules/zombie/organs.dm:103
    public virtual object check_ghost_control_eligibility(mob candidate = null)
    {
        if (QDELETED(this) || QDELETED(owner))
            return FALSE;
        if (!Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_zombie))) || owner.stat == DEAD)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/zombie/organs.dm:110
    public virtual async DmTask after_ghost_control(mob candidate = null)
    {
        await owner.Knockdown(living_transformation_time);
        zombie_welcome(owner);
        ghost_poll = null;
    }

    // tg: code/modules/zombie/organs.dm:115
    public virtual void zombie_welcome(mob_living_carbon new_zombie = null)
    {
        if (new_zombie.client != null)
        {
            to_chat(new_zombie, span_alien(I18n.T("You HUNGER!")));
            to_chat(new_zombie, span_alertalien(I18n.T("You are now a zombie! Do not seek to be cured, do not help any non-zombies in any way, do not harm your zombie brethren and spread the disease by killing others. You are a creature of hunger and violence.")));
            playsound(new_zombie, Dm.resource("sound/effects/hallucinations/far_noise.ogg"), 50, TRUE);
        }

        new_zombie.do_jitter_animation(living_transformation_time);
        Waiting.NoSleep(new_zombie.Stun(living_transformation_time));
    }
}
