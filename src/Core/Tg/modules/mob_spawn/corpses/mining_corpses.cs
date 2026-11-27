// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob_spawn/corpses/mining_corpses.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the tendrils' charred skeletons, the legion-infested corpses and the outfits they wear. The golem, moth,
// plasmaman and skeleton species the corpses may take are inert: such a corpse keeps tg's species type with its inert procs.

//Tendril-spawned Legion remains, the charred skeletons of those whose bodies sank into laval or fell into chasms.
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:4
public partial class obj_effect_mob_spawn_corpse_human_charredskeleton : obj_effect_mob_spawn_corpse_human
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:10
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        mob_living_carbon_human spawned_human = spawned_mob as mob_living_carbon_human;
        spawned_human.color = "#454545";
        spawned_human.gender = NEUTER;
        //don't need to set the human's body type (neuter)
    }
}

/// Mob spawner used by Legion to spawn costumed bodies
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:30
public partial class obj_effect_mob_spawn_corpse_human_legioninfested : obj_effect_mob_spawn_corpse_human
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        outfit = select_outfit() as Type;
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:37
    public override object get_mob_species(mob mob_possessor = null, object apply_prefs = null)
    {
        if (ispath(outfit, typeof(datum_outfit_consumed_miner)))
        {
            return pick_weight(Dm.list(
                Dm.kv(typeof(datum_species_human), 70),
                Dm.kv(typeof(datum_species_lizard), 26),
                Dm.kv(typeof(datum_species_fly), 2),
                Dm.kv(typeof(datum_species_plasmaman), 2)));
        }
        if (ispath(outfit, typeof(datum_outfit_consumed_ashwalker)))
            return typeof(datum_species_lizard_ashwalker);
        if (ispath(outfit, typeof(datum_outfit_consumed_golem)))
            return typeof(datum_species_golem);
        if (ispath(outfit, typeof(datum_outfit_consumed_shadowperson)))
            return typeof(datum_species_shadow);
        if (ispath(outfit, typeof(datum_outfit_consumed_heremoth)))
            return typeof(datum_species_moth);
        return base.get_mob_species(mob_possessor, apply_prefs);
    }

    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:53
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        obj_item_organ_legion_tumour cancer = Dm.@new<obj_item_organ_legion_tumour>();
        Waiting.NoSleep(cancer.Insert(spawned_mob as mob_living_carbon, special: TRUE, movement_flags: DELETE_IF_REPLACED));
    }

    /// Returns the outfit worn by our corpse
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:59
    public virtual object select_outfit()
    {
        object corpse_theme = pick_weight(Dm.list(
            Dm.kv("Miner", 64),
            Dm.kv("Clown", 5),
            Dm.kv("Ashwalker", 15),
            Dm.kv("Golem", 10),
            Dm.kv(pick(Dm.list(
                "Cultist",
                "Dame",
                "Operative",
                "Shadow")), 4)));

        object sw__1 = corpse_theme;
        if (Dm.eq(sw__1, "Miner"))
            return typeof(datum_outfit_consumed_miner);
        else if (Dm.eq(sw__1, "Ashwalker"))
            return typeof(datum_outfit_consumed_ashwalker);
        else if (Dm.eq(sw__1, "Golem"))
            return typeof(datum_outfit_consumed_golem);
        else if (Dm.eq(sw__1, "Clown"))
            return typeof(datum_outfit_consumed_clown);
        else if (Dm.eq(sw__1, "Cultist"))
            return typeof(datum_outfit_consumed_cultist);
        else if (Dm.eq(sw__1, "Dame"))
            return typeof(datum_outfit_consumed_dame);
        else if (Dm.eq(sw__1, "Operative"))
            return typeof(datum_outfit_syndicatecommandocorpse_lessenedgear);
        else if (Dm.eq(sw__1, "Shadow"))
            return typeof(datum_outfit_consumed_shadowperson);
        return null;
    }
}

/// Corpse spawner used by dwarf legions to make small corpses
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:95
public partial class obj_effect_mob_spawn_corpse_human_legioninfested_dwarf : obj_effect_mob_spawn_corpse_human_legioninfested
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:98
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep((spawned_mob as mob_living_carbon_human).dna.add_mutation(typeof(datum_mutation_dwarfism), MUTATION_SOURCE_MUTATOR));
    }
}

/// Corpse spawner used by snow legions with alternate costumes
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:102
public partial class obj_effect_mob_spawn_corpse_human_legioninfested_snow : obj_effect_mob_spawn_corpse_human_legioninfested
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:105
    public override object select_outfit()
    {
        object corpse_theme = pick_weight(Dm.list(
            Dm.kv("Miner", 64),
            Dm.kv("Clown", 5),
            Dm.kv("Golem", 15),
            Dm.kv("Settler", 10),
            Dm.kv(pick(Dm.list(
                "Cultist",
                "Heremoth",
                "Operative",
                "Shadow")), 4)));

        object sw__1 = corpse_theme;
        if (Dm.eq(sw__1, "Miner"))
            return typeof(datum_outfit_consumed_miner);
        else if (Dm.eq(sw__1, "Settler"))
            return typeof(datum_outfit_consumed_ice_settler);
        else if (Dm.eq(sw__1, "Heremoth"))
            return typeof(datum_outfit_consumed_heremoth);
        else if (Dm.eq(sw__1, "Clown"))
            return typeof(datum_outfit_consumed_clown);
        else if (Dm.eq(sw__1, "Cultist"))
            return typeof(datum_outfit_consumed_cultist);
        else if (Dm.eq(sw__1, "Golem"))
            return typeof(datum_outfit_consumed_golem);
        else if (Dm.eq(sw__1, "Operative"))
            return typeof(datum_outfit_syndicatecommandocorpse_lessenedgear);
        else if (Dm.eq(sw__1, "Shadow"))
            return typeof(datum_outfit_consumed_shadowperson);
        return null;
    }
}

/// Creates a dead legion-infested skeleton
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:136
public partial class obj_effect_mob_spawn_corpse_human_legioninfested_skeleton : obj_effect_mob_spawn_corpse_human_legioninfested
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:141
    public override object select_outfit()
    {
        return null;
    }

    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:144
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        spawned_mob.gender = NEUTER;
    }
}

/// Creates a dead and burned legion-infested skeleton
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:149
public partial class obj_effect_mob_spawn_corpse_human_legioninfested_skeleton_charred : obj_effect_mob_spawn_corpse_human_legioninfested_skeleton
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:156
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        spawned_mob.color = "#454545";
        Waiting.NoSleep(spawned_mob.AddComponent(typeof(datum_component_storm_hating)));
    }
}

// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:161
public partial class datum_outfit_consumed_miner : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:167
    public override async DmTask pre_equip(mob_living_carbon_human miner = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        float regular_uniform = FALSE;
        if (Dm.truthy(visuals_only))
            regular_uniform = TRUE; //assume human
        else
        {
            Type new_species_type = (miner.dna.species as datum)?.type;
            if (new_species_type != typeof(datum_species_plasmaman))
                regular_uniform = TRUE;
            else
            {
                uniform = typeof(obj_item_clothing_under_plasmaman);
                belt = typeof(obj_item_tank_internals_plasmaman_belt);
                head = typeof(obj_item_clothing_head_helmet_space_plasmaman);
            }
            if (new_species_type == typeof(datum_species_lizard))
                shoes = null; //digitigrade says no
        }
        if (Dm.truthy(regular_uniform))
        {
            uniform = typeof(obj_item_clothing_under_rank_cargo_miner_lavaland);
            if (prob(4))
            {
                belt = pick_weight(Dm.list(
                    Dm.kv(typeof(obj_item_storage_belt_mining), 2),
                    Dm.kv(typeof(obj_item_storage_belt_mining_alt), 2))) as Type;
            }
            else if (prob(10))
            {
                belt = pick_weight(Dm.list(
                    Dm.kv(typeof(obj_item_pickaxe), 8),
                    Dm.kv(typeof(obj_item_pickaxe_mini), 4),
                    Dm.kv(typeof(obj_item_pickaxe_silver), 2),
                    Dm.kv(typeof(obj_item_pickaxe_diamond), 1))) as Type;
            }
            else
                belt = typeof(obj_item_tank_internals_emergency_oxygen_engi);
        }

        if (prob(20))
        {
            suit = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_clothing_suit_hooded_explorer), 18),
                Dm.kv(typeof(obj_item_clothing_suit_hooded_cloak_goliath), 2))) as Type;
        }
        if (prob(30))
        {
            r_pocket = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_stack_candela_beacon), 20),
                Dm.kv(typeof(obj_item_stack_marker_beacon), 15),
                Dm.kv(typeof(obj_item_stack_spacecash_c1000), 7),
                Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_survival), 2),
                Dm.kv(typeof(obj_item_borg_upgrade_modkit_damage), 1))) as Type;
        }
        if (prob(10))
        {
            l_pocket = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_stack_spacecash_c1000), 7),
                Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_survival), 2),
                Dm.kv(typeof(obj_item_borg_upgrade_modkit_cooldown), 1))) as Type;
        }
    }
}

// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:218
public partial class datum_outfit_consumed_ashwalker : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:222
    public override async DmTask pre_equip(mob_living_carbon_human ashwalker = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (prob(95))
            head = typeof(obj_item_clothing_head_helmet_gladiator);
        else
        {
            suit = typeof(obj_item_clothing_suit_armor_bone);
            gloves = typeof(obj_item_clothing_gloves_bracer);
            head = typeof(obj_item_clothing_head_helmet_skull);
        }
        if (prob(5))
        {
            back = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_spear_bonespear), 3),
                Dm.kv(typeof(obj_item_fireaxe_boneaxe), 2))) as Type;
        }
        if (prob(10))
            belt = typeof(obj_item_storage_belt_mining_primitive);
        if (prob(30))
            l_pocket = typeof(obj_item_knife_combat_bone);
        if (prob(30))
            r_pocket = typeof(obj_item_knife_combat_bone);
    }
}

//takes a lot from the clown job, notably NO PDA and different backpack loot + pocket goodies
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:242
public partial class datum_outfit_consumed_clown : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:259
    public override async DmTask pre_equip(mob_living_carbon_human clown = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (!Dm.truthy(visuals_only))
            await clown.fully_replace_character_name(clown.name, pick(GLOB.clown_names));
        if (prob(70))
        {
            object backpack_loot = pick(Dm.list(
                Dm.kv(typeof(obj_item_food_grown_banana), 1),
                Dm.kv(typeof(obj_item_megaphone_clown), 1),
                typeof(obj_item_pneumatic_cannon_pie),
                Dm.kv(typeof(obj_item_reagent_containers_cup_soda_cans_canned_laughter), 1),
                Dm.kv(typeof(obj_item_reagent_containers_spray_waterflower), 1),
                Dm.kv(typeof(obj_item_stamp_clown), 1)));
            if (Dm.eq(backpack_loot, typeof(obj_item_pneumatic_cannon_pie)))
                drop_a_pie_cannon = TRUE;
            else
                Dm.append(ref backpack_contents, backpack_loot);
        }
        if (prob(30))
            Dm.append(ref backpack_contents, Dm.list(Dm.kv(typeof(obj_item_stack_sheet_mineral_bananium), pick_weight(Dm.alist(Dm.kv(1, 3), Dm.kv(2, 2), Dm.kv(3, 1))))));
        if (prob(10))
        {
            l_pocket = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_bikehorn_golden), 3),
                Dm.kv(typeof(obj_item_bikehorn_airhorn), 1))) as Type;
        }
        if (prob(10))
            r_pocket = typeof(obj_item_implanter_sad_trombone);
    }

    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:285
    public override async DmTask post_equip(mob_living_carbon_human clown = null, float? visuals_only = null)
    {
        await base.post_equip(clown, visuals_only);
        if (Dm.truthy(drop_a_pie_cannon))
            Dm.@new<obj_item_pneumatic_cannon_pie>(get_turf(clown));
    }
}

// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:290
public partial class datum_outfit_consumed_golem : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:294
    public override async DmTask pre_equip(mob_living_carbon_human golem = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (prob(30))
        {
            glasses = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_clothing_glasses_hud_diagnostic), 2),
                Dm.kv(typeof(obj_item_clothing_glasses_hud_health), 2),
                Dm.kv(typeof(obj_item_clothing_glasses_meson), 2),
                Dm.kv(typeof(obj_item_clothing_glasses_science), 2),
                Dm.kv(typeof(obj_item_clothing_glasses_welding), 2),
                Dm.kv(typeof(obj_item_clothing_glasses_night), 1))) as Type;
        }
        if (prob(10) && !Dm.truthy(visuals_only)) //visuals_only = not a golem = can't put things in the belt slot without a jumpsuit
        {
            belt = pick(Dm.list(
                typeof(obj_item_crowbar_power),
                typeof(obj_item_screwdriver_power),
                typeof(obj_item_storage_belt_mining_vendor),
                typeof(obj_item_storage_belt_utility_full),
                typeof(obj_item_weldingtool_experimental))) as Type;
        }
        if (prob(50))
            neck = typeof(obj_item_bedsheet_rd_royal_cape);
    }
}

// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:315
public partial class datum_outfit_consumed_ice_settler : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:322
    public override async DmTask pre_equip(mob_living_carbon_human ice_settler = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (prob(40))
        {
            r_pocket = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_coin_silver), 5),
                Dm.kv(typeof(obj_item_fishing_hook), 2),
                Dm.kv(typeof(obj_item_coin_gold), 2),
                Dm.kv(typeof(obj_item_fishing_hook_shiny), 1))) as Type;
        }
        if (prob(30))
        {
            back = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_pickaxe), 4),
                Dm.kv(typeof(obj_item_tank_internals_oxygen), 6))) as Type;
        }
        else
        {
            back = typeof(obj_item_storage_backpack_satchel_explorer);
            backpack_contents = Dm.list();
            object backpack_loot = pick(Dm.list(
                Dm.kv(typeof(obj_item_food_fishmeat), 89),
                Dm.kv(typeof(obj_item_food_fishmeat_carp), 10),
                Dm.kv(typeof(obj_item_skeleton_key), 1)));
            Dm.append(ref backpack_contents, backpack_loot);
        }
    }
}

//this is so pointlessly gendered but whatever bro i'm here to refactor not judge
// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:346
public partial class datum_outfit_consumed_dame : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:356
    public override async DmTask pre_equip(mob_living_carbon_human dame = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (!Dm.truthy(visuals_only))
        {
            dame.gender = FEMALE;
            dame.physique = FEMALE;
            await dame.update_body();
        }
        if (prob(30))
            back = typeof(obj_item_nullrod_claymore_talking);
        else
        {
            back = typeof(obj_item_shield_buckler);
            belt = typeof(obj_item_nullrod_claymore);
        }
    }
}

// tg: code/modules/mob_spawn/corpses/mining_corpses.dm:392
public partial class datum_outfit_consumed_heremoth : datum_outfit
{
    // tg: code/modules/mob_spawn/corpses/mining_corpses.dm:398
    public override async DmTask pre_equip(mob_living_carbon_human moth = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (prob(70))
            glasses = typeof(obj_item_clothing_glasses_blindfold);
        if (prob(90))
        {
            back = typeof(obj_item_storage_backpack_cultpack);
            backpack_contents = Dm.list();
            object backpack_loot = pick(Dm.list(
                Dm.kv(typeof(obj_item_flashlight_lantern), 1),
                Dm.kv(typeof(obj_item_toy_plush_moth), 1),
                Dm.kv(typeof(obj_item_toy_eldritch_book), 2),
                Dm.kv(typeof(obj_item_knife_combat_survival), 2)));
            Dm.append(ref backpack_contents, backpack_loot);
        }
    }
}
