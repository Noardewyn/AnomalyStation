// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, the heretical hunt: the fake crewmen with their weapons, their corpses and their behaviour trees, the bodies dragged
// onto the ritual rune for points by rank (the rune's flick and sound), and the virtual heretic's outfit with its rust healing. The
// raw prophets the map places are the heretic's own summons and stay inert until the heretic is ported (not-translated.md). The var
// blocks are generated.

// tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:1
public partial class datum_lazy_template_virtual_domain_heretic_hunt : datum_lazy_template_virtual_domain
{
    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:13
    public override async DmTask setup_domain(DmList created_atoms = null)
    {
        foreach (mob_living_basic_fake_crewman target in Dm.each<mob_living_basic_fake_crewman>(created_atoms))
            RegisterSignal(target, COMSIG_LIVING_DROPPED_LOOT, nameof(on_body_spawned));

        foreach (mob_living_basic_heretic_summon helper in Dm.each<mob_living_basic_heretic_summon>(created_atoms))
        {
            helper.ai_controller = Dm.@new<datum_ai_controller_basic_controller_simple_simple_hostile>(helper);
            ((datum_ai_controller)helper.ai_controller).set_blackboard_key(BB_BASIC_MOB_IDLE_WALK_CHANCE, 0.1f);
        }

        obj_effect_heretic_rune_big rune = Dm.locate<obj_effect_heretic_rune_big>(created_atoms);
        await rune.set_greyscale(pick(assoc_to_values(GLOB.heretic_path_to_color)));
    }

    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:25
    [SignalHandler]
    public virtual void on_body_spawned(mob_living source = null, DmList loot = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
            return;

        foreach (mob_living_carbon_human body in Dm.each<mob_living_carbon_human>(loot))
        {
            RegisterSignal(body, COMSIG_MOVABLE_MOVED, nameof(check_loc));
            // let's be safe
            foreach (obj_item_modular_computer_pda pda in Dm.each<obj_item_modular_computer_pda>(body.get_all_gear()))
                qdel(pda);
        }
    }

    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:37
    [SignalHandler]
    public virtual void check_loc(mob_living_carbon_human source = null, params object[] __extra)
    {
        obj_effect_heretic_rune_big rune = Dm.locate<obj_effect_heretic_rune_big>(source.loc);
        if (rune == null)
            return;

        datum_job fake_job = (SSjob.get_job(source.job) ?? SSjob.get_job_type(typeof(datum_job_unassigned))) as datum_job;
        // 10 points are needed
        if (Dm.truthy(Dm.bitand(fake_job.departments_bitflags, DEPARTMENT_BITFLAG_COMMAND)))
            Waiting.NoSleep(add_points(3));
        else if (Dm.truthy(Dm.bitand(fake_job.departments_bitflags, DEPARTMENT_BITFLAG_SECURITY)))
            Waiting.NoSleep(add_points(2));
        else
            Waiting.NoSleep(add_points(1));
        // cleanup
        Waiting.NoSleep(source.gib(ALL));

        // mimic a ritual effect
        if (Dm.locate<obj_structure_closet_crate_secure_bitrunning_encrypted>(range(1, rune)) != null)
            rune.balloon_alert_to_viewers(I18n.T("ritual completed"));
        else
            rune.balloon_alert_to_viewers(I18n.T("sacrifice accepted"));
        flick(Dm.text("[]_active", rune.icon_state), rune);
        playsound(rune, Dm.resource("sound/effects/magic/castsummon.ogg"), 50, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_exponent: 10, ignore_walls: FALSE);
    }
}

// tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:63
public partial class datum_outfit_virtual_domain_heretic : datum_outfit
{
    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:76
    public override async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        ADD_TRAIT(user, TRAIT_ACT_AS_HERETIC, INNATE_TRAIT);
        ADD_TRAIT(user, TRAIT_NO_TELEPORT, INNATE_TRAIT);
        await user.AddElement(typeof(datum_element_rust_healing));
        user.add_faction(FACTION_HERETIC);
    }
}

// The actual crewmate
// tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:104
public partial class mob_living_basic_fake_crewman : mob_living_basic
{
    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), string_list(Dm.list(weapon, death_spawner))));
        Type lhand = prob(50) ? weapon : null;
        Type rhand = lhand != null ? null : weapon;
        apply_dynamic_human_appearance(this, mob_spawn_path: death_spawner, l_hand: lhand, r_hand: rhand);

        if (!Dm.truthy(attack_sound))
            attack_sound = Dm.initial(weapon, (obj_item p) => p.hitsound);
        if (!Dm.truthy(melee_damage_type))
            melee_damage_type = Dm.initial(weapon, (obj_item p) => p.damtype);
        sharpness = Dm.initial(weapon, (obj_item p) => p.sharpness);
        wound_bonus = Dm.initial(weapon, (obj_item p) => p.wound_bonus) * 0.5f;
        exposed_wound_bonus = Dm.initial(weapon, (obj_item p) => p.exposed_wound_bonus) * 0.5f;
        return dot;
    }
}

// tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:184
public partial class mob_living_basic_fake_crewman_boss_ce : mob_living_basic_fake_crewman_boss
{
    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:190
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), typeof(obj_item_ammo_casing_energy_plasma_adv), Dm.named("projectile_sound", Dm.resource("sound/items/weapons/plasma_cutter.ogg")), Dm.named("cooldown_time", 1.6f * SECONDS)));
        return dot;
    }
}

// tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:194
public partial class mob_living_basic_fake_crewman_boss_hos : mob_living_basic_fake_crewman_boss
{
    // tg: code/modules/bitrunning/virtual_domain/domains/heretic_hunt.dm:201
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), typeof(obj_item_ammo_casing_energy_laser), Dm.named("projectile_sound", Dm.resource("sound/items/weapons/laser.ogg")), Dm.named("cooldown_time", 1.2f * SECONDS)));
        return dot;
    }
}
