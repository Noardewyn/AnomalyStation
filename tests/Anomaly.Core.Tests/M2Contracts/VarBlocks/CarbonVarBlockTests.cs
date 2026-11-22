using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M2Contracts.VarBlocks;

/// <summary>
/// The var blocks of the carbon area written by the M2 contracts step (rules 10.2): /mob/living/carbon
/// (carbon_defines.dm), /mob/living/carbon/human (human_defines.dm), /mob/living/carbon/human/consistent (dummy.dm:137),
/// /datum/species (_species.dm:18), /datum/species/human (species_types/humans.dm) and /datum/dna (dna.dm:29). Each type
/// has its tg path, and initial() reads tg's defaults from the prototype; the object-creating defaults are checked on a
/// world-free instance (a constructor and dm_init_vars touch no World, object-model.md section 5).
/// </summary>
public class CarbonVarBlockTests
{
    [Fact]
    public void EveryTypeHasItsTgPath()
    {
        Assert.Equal("/mob/living/carbon", DmTypes.PathOf(typeof(mob_living_carbon)));
        Assert.Equal("/mob/living/carbon/human", DmTypes.PathOf(typeof(mob_living_carbon_human)));
        Assert.Equal("/mob/living/carbon/human/consistent", DmTypes.PathOf(typeof(mob_living_carbon_human_consistent)));
        Assert.Equal("/datum/species", DmTypes.PathOf(typeof(datum_species)));
        Assert.Equal("/datum/species/human", DmTypes.PathOf(typeof(datum_species_human)));
        Assert.Equal("/datum/dna", DmTypes.PathOf(typeof(datum_dna)));
    }

    [Fact]
    public void CarbonDefaults()
    {
        // carbon_defines.dm:2, :4, :5, :14 (overrides), :52, :84, :112 (declarations)
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.abstract_type), typeof(mob_living_carbon)));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.gender), "male"));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.pressure_resistance), 15));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.blocks_emissive), EMISSIVE_BLOCK_NONE));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.type_of_meat), typeof(obj_item_food_meat_slab)));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.heat_protection), 0));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.bodytype), BODYTYPE_ORGANIC));
        // A list() default runs in dm_init_vars, which prototypes skip: initial() reads null, as in DM.
        Assert.Null(initial(typeof(mob_living_carbon), (mob_living_carbon p) => p.bodyparts));

        // carbon_defines.dm:6, :8, :59, :68 on a world-free instance (constructor and dm_init_vars only)
        var carbon = new mob_living_carbon();
        carbon.dm_init_vars();
        Assert.Equal(4f, carbon.hud_possible.len);
        Assert.True(Dm.eq(carbon.hud_possible[1f], HEALTH_HUD));
        Assert.Equal(2f, carbon.held_items.len);
        Assert.Null(carbon.held_items[2f]);
        Assert.Equal(6f, carbon.bodyparts.len);
        Assert.True(Dm.eq(carbon.bodyparts[1f], typeof(obj_item_bodypart_chest)));
        Assert.True(Dm.eq(carbon.bodyparts[6f], typeof(obj_item_bodypart_leg_left)));
        Assert.Equal(0f, carbon.real_bodypart_cache.len);

        // human_defines.dm:8: the human list replaces the carbon one
        var human = new mob_living_carbon_human();
        human.dm_init_vars();
        Assert.Equal(11f, human.hud_possible.len);
        Assert.True(Dm.eq(human.hud_possible[11f], FAN_HUD));
    }

    [Fact]
    public void HumanDefaults()
    {
        // human_defines.dm:3, :7, :9, :13, :14, :16 (overrides), :21, :46, :87, :104 (declarations)
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.name), "Unknown"));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.appearance_flags), 801)); // KEEP_TOGETHER|TILE_BOUND|PIXEL_SCALE|LONG_GLIDE
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.hud_type), typeof(datum_hud_human)));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.can_be_shoved_into), TRUE));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.initial_language_holder), typeof(datum_language_holder_empty)));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.max_grab), GRAB_KILL));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.hairstyle), "Bald"));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.age), 30));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.coretemperature), 310.15f));
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon_human p) => p.cached_body_width), 32));
        // The carbon override of a declaration on the parent line is inherited; the human one wins (:13).
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human), (mob_living_carbon p) => p.pressure_resistance), 25));
        // An override in the human type reaches its subtypes (/mob/living/carbon/human/consistent has an empty block).
        Assert.True(Dm.eq(initial(typeof(mob_living_carbon_human_consistent), (mob_living_carbon_human p) => p.real_name), "Unknown"));
    }

    [Fact]
    public void SpeciesDefaults()
    {
        // _species.dm:32, :35, :52, :54, :77, :114, :132, :137
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.sexes), TRUE));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.max_bodypart_count), 6));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.blood_deficiency_drain_rate), 0.25f + 0.025f));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.meat), typeof(obj_item_food_meat_slab_human)));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.mutantbrain), typeof(obj_item_organ_brain)));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.fixed_mut_color), ""));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.fire_overlay), "human"));
        Assert.True(Dm.eq(initial(typeof(datum_species), (datum_species p) => p.inherent_biotypes), MOB_ORGANIC + MOB_HUMANOID));
        Assert.Null(initial(typeof(datum_species), (datum_species p) => p.id));

        var species = new datum_species();
        species.dm_init_vars();
        Assert.Equal(6f, species.bodypart_overrides.len);
        Assert.True(Dm.eq(species.bodypart_overrides["head"], typeof(obj_item_bodypart_head)));
        Assert.True(Dm.eq(species.bodypart_overrides["l_arm"], typeof(obj_item_bodypart_arm_left)));
        Assert.Equal(0f, species.inherent_traits.len);
    }

    [Fact]
    public void HumanSpeciesDefaults()
    {
        // species_types/humans.dm:2, :3, :7, :8, :9
        Assert.True(Dm.eq(initial(typeof(datum_species_human), (datum_species p) => p.name), Dm.text("\\improper Human")));
        Assert.True(Dm.eq(initial(typeof(datum_species_human), (datum_species p) => p.id), "human"));
        Assert.True(Dm.eq(initial(typeof(datum_species_human), (datum_species p) => p.skinned_type), typeof(obj_item_stack_sheet_animalhide_carbon_human)));
        Assert.True(Dm.eq(initial(typeof(datum_species_human), (datum_species p) => p.changesource_flags), 254)); // MIRROR_BADMIN | WABBAJACK | MIRROR_MAGIC | MIRROR_PRIDE | ERT_SPAWN | RACE_SWAP | SLIME_EXTRACT
        Assert.True(Dm.eq(initial(typeof(datum_species_human), (datum_species p) => p.payday_modifier), 1.1f));
        // An inherited declaration keeps the parent's default.
        Assert.True(Dm.eq(initial(typeof(datum_species_human), (datum_species p) => p.species_language_holder), typeof(datum_language_holder_human_basic)));

        var human = new datum_species_human();
        human.dm_init_vars();
        Assert.Equal(1f, human.inherent_traits.len);
        Assert.True(Dm.eq(human.inherent_traits[1f], "uses_skintones"));
        Assert.Equal(6f, human.bodypart_overrides.len);
    }

    [Fact]
    public void DnaDefaults()
    {
        // dna.dm:37, :52, :54
        Assert.True(Dm.eq(initial(typeof(datum_dna), (datum_dna p) => p.species), typeof(datum_species_human)));
        Assert.True(Dm.eq(initial(typeof(datum_dna), (datum_dna p) => p.stability), 100));
        Assert.True(Dm.eq(initial(typeof(datum_dna), (datum_dna p) => p.scrambled), FALSE));
        Assert.Null(initial(typeof(datum_dna), (datum_dna p) => p.features));

        var dna = new datum_dna();
        dna.dm_init_vars();
        Assert.True(Dm.eq(dna.features["mcolor"], "#FFFFFF"));
        Assert.Equal(8f, dna.mutation_index.len);       // var/mutation_index[DNA_MUTATION_BLOCKS]
        Assert.Null(dna.mutation_index[1f]);
        Assert.Equal(8f, dna.default_mutation_genes.len);
    }
}
