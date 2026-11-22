// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/netherworld/creature.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:1
public partial class mob_living_basic_creature : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_NETHER, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 0f));
        if (Dm.truthy(health_scaling))
            Waiting.NoSleep(AddComponent(typeof(datum_component_health_scaling_effects), Dm.named("min_health_attack_modifier_lower", 15f), Dm.named("min_health_attack_modifier_upper", 30f), Dm.named("min_health_slowdown", -1.5f)));

        Waiting.NoSleep(Dm.@new<datum_action_cooldown_spell_jaunt_creature_teleport>(this).Grant(this)); // GRANT_ACTION
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:47
    public virtual object can_be_seen(turf location = null)
    {
        // Check for darkness
        if (location?.lighting_object != null && Dm.truthy(location.check_lumcount_below(0.1f))) // No one can see us in the darkness, right?
            return null;

        // We aren't in darkness, loop for viewers.
        DmList check_list = Dm.list(this);
        if (location != null)
            Dm.append(ref check_list, location);

        // This loop will, at most, loop twice.
        foreach (atom check in Dm.each<atom>(check_list))
        {
            foreach (mob_living mob_target in Dm.each<mob_living>(oview(7, this))) // They probably cannot see us if we cannot see them... can they?
            {
                if (mob_target.client != null && !Dm.truthy(mob_target.is_blind()) && !HAS_TRAIT(mob_target, TRAIT_UNOBSERVANT))
                    return mob_target;
            }
            foreach (obj_vehicle_sealed_mecha mecha_mob_target in Dm.each<obj_vehicle_sealed_mecha>(oview(7, this)))
            {
                foreach (mob mechamob_target in Dm.each_cast<mob>(mecha_mob_target.occupants))
                {
                    if (mechamob_target.client != null && !Dm.truthy((mechamob_target as mob_living)?.is_blind()))
                        return mechamob_target;
                }
            }
        }
        return null;
    }
}

/// Jaunt spell used by creature. Can only jaunt or unjaunt if nothing can see you.
// tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:71
public partial class datum_action_cooldown_spell_jaunt_creature_teleport : datum_action_cooldown_spell_jaunt
{
    // tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:82
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        observed_blocker = await owner.AddComponent(typeof(datum_component_unobserved_actor), Dm.named("unobserved_flags", NO_OBSERVED_ACTIONS), Dm.named("affected_actions", Dm.list(type))) as datum_component_unobserved_actor;
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:88
    public override async DmTask Remove(mob removed_from = null)
    {
        QDEL_NULL(ref observed_blocker);
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:92
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        if (owner == null)
            return SPELL_CANCEL_CAST;
        if (!Dm.truthy(await do_after(owner, 6 * SECONDS, owner.loc)))
        {
            owner.balloon_alert(owner, "interrupted!");
            return SPELL_CANCEL_CAST;
        }
        return await base.before_cast(cast_on);
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/creature.dm:100
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        playsound(get_turf(owner), Dm.resource("sound/effects/podwoosh.ogg"), 50, TRUE, -1);
        if (cast_on.loc is obj_effect_dummy_phased_mob) // is_jaunting (__DEFINES/magic.dm:109)
        {
            await exit_jaunt(cast_on as mob_living);
            return dot;
        }
        await enter_jaunt(cast_on as mob_living);
        return dot;
    }
}
