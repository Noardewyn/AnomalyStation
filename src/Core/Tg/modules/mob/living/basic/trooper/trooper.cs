// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/trooper/trooper.dm, nanotrasen.dm, syndicate.dm,
// pirate.dm, russian.dm, abductor.dm (trooper_ai.dm is var blocks; the behaviour trees are compiled, DV251)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the troopers whole (the portal storm's syndicate shocktroops, the shuttle loan's infiltrators, the pirates, the
// Russians, the abductors and Nanotrasen's troops; the viscerator came with gaps E1a, syndicate.cs): a human-looking basic mob dressed by its mob spawner, dropping its
// corpse and loot, punching, slashing or shooting (with bursts and full auto) under the trooper behaviour trees. The var blocks are
// generated (D111).

// tg: code/modules/mob/living/basic/trooper/trooper.dm:1
public partial class mob_living_basic_trooper : mob_living_basic
{
    // tg: code/modules/mob/living/basic/trooper/trooper.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_dynamic_human_appearance(this, mob_spawn_path: mob_spawner, r_hand: r_hand, l_hand: l_hand);
        if (Dm.length(loot) > 0 || corpse != null)
        {
            loot ??= Dm.list(); // LAZYOR(loot, corpse)
            Dm.combine(loot, corpse);
            loot = string_list(loot) as DmList;
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), loot));
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_SHOE)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/trooper/trooper.dm:41
    public override object get_unconscious_appearance()
    {
        return get_generic_humanoid_static_appearance();
    }
}

// tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:2
public partial class mob_living_basic_trooper_nanotrasen : mob_living_basic_trooper
{
    // tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:11
    public override object assess_threat(object judgement_criteria = null, string lasercolor = null, datum_callback weaponcheck = null)
    {
        return -10; // Respect our troops
    }

    // tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:97
    public virtual void ai_retaliate_behaviour(mob_living attacker = null)
    {
        if (attacker == null)
            return;
        foreach (mob_living_basic_trooper_nanotrasen potential_trooper in Dm.each<mob_living_basic_trooper_nanotrasen>(oview(this, 7)))
            ((datum_ai_controller)potential_trooper.ai_controller).set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, attacker, world.time);
    }
}

/// A variant that calls for reinforcements on spotting a target
// tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:18
public partial class mob_living_basic_trooper_nanotrasen_ranged : mob_living_basic_trooper_nanotrasen
{
    // tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks),
            Dm.named("casing_type", casingtype),
            Dm.named("projectile_sound", projectilesound),
            Dm.named("cooldown_time", ranged_cooldown),
            Dm.named("burst_shots", burst_shots)));
        if (ranged_cooldown <= 1 * SECONDS)
            Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_mob_full_auto)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:79
public partial class mob_living_basic_trooper_nanotrasen_peaceful : mob_living_basic_trooper_nanotrasen
{
    // tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var retaliate_callback = Dm.@new<datum_callback>(this, nameof(ai_retaliate_behaviour));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), retaliate_callback));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:88
public partial class mob_living_basic_trooper_nanotrasen_ranged_smg_peaceful : mob_living_basic_trooper_nanotrasen_ranged_smg
{
    // tg: code/modules/mob/living/basic/trooper/nanotrasen.dm:92
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var retaliate_callback = Dm.@new<datum_callback>(this, nameof(ai_retaliate_behaviour));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), retaliate_callback));
        return dot;
    }
}

/// Pirate trooper subtype
// tg: code/modules/mob/living/basic/trooper/pirate.dm:2
public partial class mob_living_basic_trooper_pirate : mob_living_basic_trooper
{
    // tg: code/modules/mob/living/basic/trooper/pirate.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plundering_attacks), Dm.named("plunder_amount", plunder_credits)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/pirate.dm:40
public partial class mob_living_basic_trooper_pirate_melee_space : mob_living_basic_trooper_pirate_melee
{
    // tg: code/modules/mob/living/basic/trooper/pirate.dm:48
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/pirate.dm:52
public partial class mob_living_basic_trooper_pirate_ranged : mob_living_basic_trooper_pirate
{
    // tg: code/modules/mob/living/basic/trooper/pirate.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks),
            Dm.named("projectile_type", projectiletype),
            Dm.named("projectile_sound", projectilesound),
            Dm.named("cooldown_time", ranged_cooldown),
            Dm.named("burst_shots", burst_shots)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/pirate.dm:77
public partial class mob_living_basic_trooper_pirate_ranged_space : mob_living_basic_trooper_pirate_ranged
{
    // tg: code/modules/mob/living/basic/trooper/pirate.dm:85
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        return dot;
    }
}

/// Russian trooper subtype
// tg: code/modules/mob/living/basic/trooper/russian.dm:22
public partial class mob_living_basic_trooper_russian_ranged : mob_living_basic_trooper_russian
{
    // tg: code/modules/mob/living/basic/trooper/russian.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), Dm.named("casing_type", casingtype), Dm.named("projectile_sound", projectilesound), Dm.named("cooldown_time", 1 * SECONDS)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:9
public partial class mob_living_basic_trooper_syndicate_space : mob_living_basic_trooper_syndicate
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        Waiting.NoSleep(set_light(4));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:29
public partial class mob_living_basic_trooper_syndicate_melee : mob_living_basic_trooper_syndicate //dude with a knife and no shields
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:40
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (prob(projectile_deflect_chance))
        {
            visible_message(span_danger(I18n.T("[] blocks [] with its shield!", this, hitting_projectile)));
            return BULLET_ACT_BLOCK;
        }
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:46
public partial class mob_living_basic_trooper_syndicate_melee_space : mob_living_basic_trooper_syndicate_melee
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        Waiting.NoSleep(set_light(4));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:79
public partial class mob_living_basic_trooper_syndicate_melee_sword_space : mob_living_basic_trooper_syndicate_melee_sword
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:101
public partial class mob_living_basic_trooper_syndicate_ranged : mob_living_basic_trooper_syndicate
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:114
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks),
            Dm.named("casing_type", casingtype),
            Dm.named("projectile_sound", projectilesound),
            Dm.named("cooldown_time", ranged_cooldown),
            Dm.named("burst_shots", burst_shots)));
        if (ranged_cooldown <= 1 * SECONDS)
            Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_mob_full_auto)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:130
public partial class mob_living_basic_trooper_syndicate_ranged_space : mob_living_basic_trooper_syndicate_ranged
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:138
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        Waiting.NoSleep(set_light(4));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:169
public partial class mob_living_basic_trooper_syndicate_ranged_smg_space : mob_living_basic_trooper_syndicate_ranged_smg
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:177
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        Waiting.NoSleep(set_light(4));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/trooper/syndicate.dm:195
public partial class mob_living_basic_trooper_syndicate_ranged_shotgun_space : mob_living_basic_trooper_syndicate_ranged_shotgun
{
    // tg: code/modules/mob/living/basic/trooper/syndicate.dm:203
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        Waiting.NoSleep(set_light(4));
        return dot;
    }
}

/// Abductor troopers
// tg: code/modules/mob/living/basic/trooper/abductor.dm:20
public partial class mob_living_basic_trooper_abductor_ranged : mob_living_basic_trooper_abductor
{
    // tg: code/modules/mob/living/basic/trooper/abductor.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks),
            Dm.named("casing_type", casingtype),
            Dm.named("projectile_sound", projectilesound),
            Dm.named("cooldown_time", ranged_cooldown),
            Dm.named("burst_shots", burst_shots)));
        return dot;
    }
}
