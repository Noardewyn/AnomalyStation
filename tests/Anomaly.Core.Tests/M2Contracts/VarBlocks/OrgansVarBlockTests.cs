using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M2Contracts.VarBlocks;

/// <summary>
/// The var blocks of the organs area of the M2 contracts step: /obj/item/organ and the nine human organs
/// (surgery/organs/_organ.dm, mob/living/brain/brain_item.dm, organs/internal/*), /datum/gas_mixture, /datum/gas and its
/// subtypes, and the immutable mixtures (atmospherics/gasmixtures). Each type carries its tg path, and initial() reads
/// tg's defaults and overrides (translation-rules.md 10.2, 10.3); the object-creating defaults run in dm_init_vars.
/// </summary>
public class OrgansVarBlockTests
{
    [Theory]
    [InlineData(typeof(obj_item_organ), "/obj/item/organ")]
    [InlineData(typeof(obj_item_organ_brain), "/obj/item/organ/brain")]
    [InlineData(typeof(obj_item_organ_heart), "/obj/item/organ/heart")]
    [InlineData(typeof(obj_item_organ_lungs), "/obj/item/organ/lungs")]
    [InlineData(typeof(obj_item_organ_lungs_smoker_lungs), "/obj/item/organ/lungs/smoker_lungs")]
    [InlineData(typeof(obj_item_organ_eyes), "/obj/item/organ/eyes")]
    [InlineData(typeof(obj_item_organ_ears), "/obj/item/organ/ears")]
    [InlineData(typeof(obj_item_organ_tongue), "/obj/item/organ/tongue")]
    [InlineData(typeof(obj_item_organ_liver), "/obj/item/organ/liver")]
    [InlineData(typeof(obj_item_organ_stomach), "/obj/item/organ/stomach")]
    [InlineData(typeof(obj_item_organ_appendix), "/obj/item/organ/appendix")]
    [InlineData(typeof(datum_gas_mixture), "/datum/gas_mixture")]
    [InlineData(typeof(datum_gas_mixture_immutable), "/datum/gas_mixture/immutable")]
    [InlineData(typeof(datum_gas_mixture_immutable_space), "/datum/gas_mixture/immutable/space")]
    [InlineData(typeof(datum_gas_mixture_immutable_planetary), "/datum/gas_mixture/immutable/planetary")]
    [InlineData(typeof(datum_gas), "/datum/gas")]
    [InlineData(typeof(datum_reagent_consumable_nutriment_organ_tissue_stomach_lining), "/datum/reagent/consumable/nutriment/organ_tissue/stomach_lining")]
    [InlineData(typeof(mob_living_brain), "/mob/living/brain")]
    public void TypesCarryTheirTgPaths(Type type, string path)
    {
        Assert.Equal(path, DmTypes.PathOf(type));
        Assert.Equal(type, DmTypes.FromPath(path));
    }

    [Fact]
    public void OrganBaseDefaults()
    {
        Assert.Equal("organ", Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.name));        // _organ.dm:2
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.icon), Dm.resource("icons/obj/medical/organs/organs.dmi")));
        Assert.Equal(typeof(obj_item_organ), Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.abstract_type));
        Assert.Equal(BODY_ZONE_CHEST, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.zone));
        Assert.Null(Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.slot));
        Assert.Equal(129f, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.organ_flags));   // ORGAN_ORGANIC | ORGAN_EDIBLE
        Assert.Equal(100f, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.maxHealth));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.damage), 0));
        Assert.Equal(45f, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.high_threshold)); // STANDARD_ORGAN_THRESHOLD * 0.45
        Assert.Equal(10f, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.low_threshold));
        Assert.Equal((float)(RAW | MEAT | GORE), Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.foodtype_flags));
        Assert.Equal(10f, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.reagent_vol));
        Assert.Equal(Dm.TRUE, Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.visual));
        Assert.StartsWith("has decayed for too long", Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.failing_desc));
        Assert.Null(Dm.initial(typeof(obj_item_organ), (obj_item_organ p) => p.food_reagents));       // dm_init_vars
    }

    [Fact]
    public void BrainDefaults()
    {
        Assert.Equal("brain", Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ_brain p) => p.name));
        Assert.Equal(BODY_ZONE_HEAD, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.zone));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.slot), ORGAN_SLOT_BRAIN));
        Assert.Equal((float)(ORGAN_ORGANIC | ORGAN_VITAL | ORGAN_PROMINENT), Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.organ_flags));
        Assert.Equal(STANDARD_ORGAN_DECAY * 0.5f, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.decay_factor));
        Assert.Equal(200f, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.maxHealth)); // BRAIN_DAMAGE_DEATH
        Assert.Equal(45f, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.low_threshold));
        Assert.Equal(120f, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ p) => p.high_threshold));
        Assert.Equal(ABOVE_MOB_LAYER, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ_brain p) => p.layer));
        Assert.Equal(3f, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ_brain p) => p.throw_speed));
        Assert.Equal(5f, Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ_brain p) => p.max_skillchip_slots));
        Assert.Equal("pink", Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ_brain p) => p.shade_color));
        Assert.Null(Dm.initial(typeof(obj_item_organ_brain), (obj_item_organ_brain p) => p.brainmob));
    }

    [Fact]
    public void HeartDefaults()
    {
        Assert.Equal("heart-on", Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ_heart p) => p.icon_state));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ_heart p) => p.base_icon_state), "heart"));
        Assert.Equal(2.5f * STANDARD_ORGAN_DECAY, Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ p) => p.decay_factor));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ p) => p.now_fixed), "<span class='info'>Your heart begins to beat again.</span>"));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ p) => p.cell_line), CELL_LINE_ORGAN_HEART));
        Assert.Equal(Dm.FALSE, Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ p) => p.visual));
        Assert.Equal(Dm.TRUE, Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ_heart p) => p.beating));
        Assert.Equal((float)BEAT_NONE, Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ_heart p) => p.beat));
        Assert.Equal("a rhythmic thumping", Dm.initial(typeof(obj_item_organ_heart), (obj_item_organ_heart p) => p.beat_noise));
    }

    [Fact]
    public void LungsDefaults()
    {
        Assert.Equal(Dm.PLURAL, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.gender));
        Assert.Equal(16f, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.safe_oxygen_min));
        Assert.Equal(0.05f, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.safe_plasma_max));
        Assert.Equal(OXY, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.oxy_damage_type));
        Assert.Equal(TOX, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.plas_damage_type));
        Assert.Equal(1000f, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.heat_level_3_threshold));
        Assert.Equal(0.5f, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.cold_level_1_damage));
        Assert.Equal("steady in- and exhalation", Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.breath_noise));
        Assert.Null(Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.last_partial_pressures)); // dm_init_vars

        Assert.Equal("smoker lungs", Dm.initial(typeof(obj_item_organ_lungs_smoker_lungs), (obj_item_organ_lungs p) => p.name));
        Assert.Equal(75f, Dm.initial(typeof(obj_item_organ_lungs_smoker_lungs), (obj_item_organ_lungs p) => p.maxHealth)); // SMOKER_ORGAN_HEALTH
        Assert.Equal(STANDARD_ORGAN_HEALING * 0.75f, Dm.initial(typeof(obj_item_organ_lungs_smoker_lungs), (obj_item_organ_lungs p) => p.healing_factor));
        Assert.Equal("an unsteady, wheezing rhythm", Dm.initial(typeof(obj_item_organ_lungs_smoker_lungs), (obj_item_organ_lungs p) => p.breath_noise));
        Assert.Equal(16f, Dm.initial(typeof(obj_item_organ_lungs_smoker_lungs), (obj_item_organ_lungs p) => p.safe_oxygen_min)); // inherited
    }

    [Fact]
    public void EyesEarsTongueDefaults()
    {
        Assert.Equal(BODY_ZONE_PRECISE_EYES, Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes p) => p.name));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ p) => p.slot), ORGAN_SLOT_EYES));
        Assert.Equal(50f, Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ p) => p.maxHealth));
        Assert.Equal(0.3f * STANDARD_ORGAN_THRESHOLD, Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ p) => p.high_threshold));
        Assert.Equal((float)SEE_INVISIBLE_LIVING, Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes p) => p.see_invisible));
        Assert.Equal(90f, Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes p) => p.native_fov));
        Assert.Null(Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes p) => p.lighting_cutoff));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes p) => p.eye_icon), Dm.resource("icons/mob/human/human_eyes.dmi")));
        Assert.Equal("eyes_iris", Dm.initial(typeof(obj_item_organ_eyes), (obj_item_organ_eyes p) => p.iris_overlay));

        Assert.Equal(BODY_ZONE_HEAD, Dm.initial(typeof(obj_item_organ_ears), (obj_item_organ p) => p.zone));
        Assert.Equal((float)EAR_PROTECTION_NONE, Dm.initial(typeof(obj_item_organ_ears), (obj_item_organ_ears p) => p.bang_protect));
        Assert.Equal(1f, Dm.initial(typeof(obj_item_organ_ears), (obj_item_organ_ears p) => p.damage_multiplier));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_ears), (obj_item_organ p) => p.now_failing), "<span class='warning'>You are unable to hear at all!</span>"));

        Assert.Equal("says", Dm.initial(typeof(obj_item_organ_tongue), (obj_item_organ_tongue p) => p.say_mod));
        Assert.Equal(15f, Dm.initial(typeof(obj_item_organ_tongue), (obj_item_organ_tongue p) => p.taste_sensitivity));
        Assert.Equal((float)(JUNKFOOD | FRIED), Dm.initial(typeof(obj_item_organ_tongue), (obj_item_organ_tongue p) => p.liked_foodtypes));
        Assert.Equal((float)(GROSS | RAW | CLOTH | BUGS | GORE), Dm.initial(typeof(obj_item_organ_tongue), (obj_item_organ_tongue p) => p.disliked_foodtypes));
        Assert.Equal(BODY_ZONE_PRECISE_MOUTH, Dm.initial(typeof(obj_item_organ_tongue), (obj_item_organ p) => p.zone));
    }

    [Fact]
    public void LiverStomachAppendixDefaults()
    {
        Assert.Equal(3f, Dm.initial(typeof(obj_item_organ_liver), (obj_item_organ_liver p) => p.toxTolerance));      // LIVER_DEFAULT_TOX_TOLERANCE
        Assert.Equal(1f, Dm.initial(typeof(obj_item_organ_liver), (obj_item_organ_liver p) => p.liver_resistance));
        Assert.Equal(ALCOHOL_RATE, Dm.initial(typeof(obj_item_organ_liver), (obj_item_organ_liver p) => p.alcohol_tolerance));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_liver), (obj_item_organ p) => p.slot), ORGAN_SLOT_LIVER));

        Assert.Equal(1000f, Dm.initial(typeof(obj_item_organ_stomach), (obj_item_organ p) => p.reagent_vol));
        Assert.Equal(0.05f, Dm.initial(typeof(obj_item_organ_stomach), (obj_item_organ_stomach p) => p.metabolism_efficiency));
        Assert.Equal(STANDARD_ORGAN_DECAY * 1.15f, Dm.initial(typeof(obj_item_organ_stomach), (obj_item_organ p) => p.decay_factor));

        Assert.Equal(BODY_ZONE_PRECISE_GROIN, Dm.initial(typeof(obj_item_organ_appendix), (obj_item_organ p) => p.zone));
        Assert.True(Dm.eq(Dm.initial(typeof(obj_item_organ_appendix), (obj_item_organ_appendix p) => p.base_icon_state), "appendix"));
        Assert.Equal(0f, Dm.initial(typeof(obj_item_organ_appendix), (obj_item_organ_appendix p) => p.inflamation_stage));
    }

    [Fact]
    public void GasMixtureDefaults()
    {
        Assert.Equal(TCMB, Dm.initial(typeof(datum_gas_mixture), (datum_gas_mixture p) => p.temperature));
        Assert.Equal(TCMB, Dm.initial(typeof(datum_gas_mixture), (datum_gas_mixture p) => p.temperature_archived));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_gas_mixture), (datum_gas_mixture p) => p.volume), CELL_VOLUME));
        Assert.Equal(-1f, Dm.initial(typeof(datum_gas_mixture), (datum_gas_mixture p) => p.pipeline_cycle));
        Assert.Equal(Dm.FALSE, Dm.initial(typeof(datum_gas_mixture), (datum_gas_mixture p) => p.gc_share));
        Assert.Null(Dm.initial(typeof(datum_gas_mixture), (datum_gas_mixture p) => p.moles));

        Assert.Equal(Dm.TRUE, Dm.initial(typeof(datum_gas_mixture_immutable), (datum_gas_mixture p) => p.gc_share));
        Assert.Null(Dm.initial(typeof(datum_gas_mixture_immutable), (datum_gas_mixture_immutable p) => p.initial_temperature));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_gas_mixture_immutable_space), (datum_gas_mixture_immutable p) => p.initial_temperature), TCMB));
        Assert.Null(Dm.initial(typeof(datum_gas_mixture_immutable_planetary), (datum_gas_mixture_immutable_planetary p) => p.initial_gas));
    }

    [Fact]
    public void GasMetaIsAPerWorldStatic()
    {
        var a = new World();
        var b = new World();
        using (a.Enter())
            datum_gas_mixture.gas_meta = Dm.list(1);
        using (b.Enter())
            Assert.Null(datum_gas_mixture.gas_meta);
        using (a.Enter())
            Assert.Equal(1, datum_gas_mixture.gas_meta.Count);
    }

    [Fact]
    public void GasBaseDefaults()
    {
        Assert.True(Dm.eq(Dm.initial(typeof(datum_gas), (datum_gas p) => p.id), ""));
        Assert.Equal("", Dm.initial(typeof(datum_gas), (datum_gas p) => p.name));
        Assert.Null(Dm.initial(typeof(datum_gas), (datum_gas p) => p.desc));
        Assert.Null(Dm.initial(typeof(datum_gas), (datum_gas p) => p.moles_visible));
        Assert.Equal((float)NONE, Dm.initial(typeof(datum_gas), (datum_gas p) => p.cargo_flags));
        Assert.Equal(0f, Dm.initial(typeof(datum_gas), (datum_gas p) => p.specific_heat));
    }

    [Theory]
    [InlineData(typeof(datum_gas_oxygen), "/datum/gas/oxygen", GAS_O2, 20f, "Oxygen")]
    [InlineData(typeof(datum_gas_nitrogen), "/datum/gas/nitrogen", GAS_N2, 20f, "Nitrogen")]
    [InlineData(typeof(datum_gas_carbon_dioxide), "/datum/gas/carbon_dioxide", GAS_CO2, 30f, "Carbon Dioxide")]
    [InlineData(typeof(datum_gas_plasma), "/datum/gas/plasma", GAS_PLASMA, 200f, "Plasma")]
    [InlineData(typeof(datum_gas_water_vapor), "/datum/gas/water_vapor", GAS_WATER_VAPOR, 40f, "Water Vapor")]
    [InlineData(typeof(datum_gas_hypernoblium), "/datum/gas/hypernoblium", GAS_HYPER_NOBLIUM, 2000f, "Hyper-Noblium")]
    [InlineData(typeof(datum_gas_nitrous_oxide), "/datum/gas/nitrous_oxide", GAS_N2O, 40f, "Nitrous Oxide")]
    [InlineData(typeof(datum_gas_nitrium), "/datum/gas/nitrium", GAS_NITRIUM, 10f, "Nitrium")]
    [InlineData(typeof(datum_gas_tritium), "/datum/gas/tritium", GAS_TRITIUM, 10f, "Tritium")]
    [InlineData(typeof(datum_gas_bz), "/datum/gas/bz", GAS_BZ, 20f, "BZ")]
    [InlineData(typeof(datum_gas_pluoxium), "/datum/gas/pluoxium", GAS_PLUOXIUM, 80f, "Pluoxium")]
    [InlineData(typeof(datum_gas_miasma), "/datum/gas/miasma", GAS_MIASMA, 20f, "Miasma")]
    [InlineData(typeof(datum_gas_freon), "/datum/gas/freon", GAS_FREON, 600f, "Freon")]
    [InlineData(typeof(datum_gas_hydrogen), "/datum/gas/hydrogen", GAS_HYDROGEN, 15f, "Hydrogen")]
    [InlineData(typeof(datum_gas_healium), "/datum/gas/healium", GAS_HEALIUM, 10f, "Healium")]
    [InlineData(typeof(datum_gas_proto_nitrate), "/datum/gas/proto_nitrate", GAS_PROTO_NITRATE, 30f, "Proto-Nitrate")]
    [InlineData(typeof(datum_gas_zauker), "/datum/gas/zauker", GAS_ZAUKER, 350f, "Zauker")]
    [InlineData(typeof(datum_gas_halon), "/datum/gas/halon", GAS_HALON, 175f, "Halon")]
    [InlineData(typeof(datum_gas_helium), "/datum/gas/helium", GAS_HELIUM, 15f, "Helium")]
    [InlineData(typeof(datum_gas_antinoblium), "/datum/gas/antinoblium", GAS_ANTINOBLIUM, 1f, "Anti-Noblium")]
    public void GasSubtypesCarryTheirPathAndOverrides(Type type, string path, string id, float specificHeat, string name)
    {
        Assert.Equal(path, DmTypes.PathOf(type));
        Assert.True(Dm.eq(Dm.initial(type, (datum_gas p) => p.id), id));
        Assert.Equal(specificHeat, Dm.initial(type, (datum_gas p) => p.specific_heat));
        Assert.Equal(name, Dm.initial(type, (datum_gas p) => p.name));
        Assert.True(istext(Dm.initial(type, (datum_gas p) => p.desc)));
    }

    [Fact]
    public void GasOverridesOfFlagsColorsAndVisibility()
    {
        Assert.Equal((float)(GAS_PURCHASABLE | GAS_DANGEROUS), Dm.initial(typeof(datum_gas_carbon_dioxide), (datum_gas p) => p.cargo_flags));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_gas_carbon_dioxide), (datum_gas p) => p.primary_color), COLOR_GRAY));
        Assert.Equal(0.2f, Dm.initial(typeof(datum_gas_oxygen), (datum_gas p) => p.base_value));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_gas_plasma), (datum_gas p) => p.moles_visible), MOLES_GAS_VISIBLE));
        Assert.True(Dm.eq(Dm.initial(typeof(datum_gas_nitrous_oxide), (datum_gas p) => p.moles_visible), MOLES_GAS_VISIBLE * 2));
        Assert.Equal("nitrous_oxide", Dm.initial(typeof(datum_gas_nitrous_oxide), (datum_gas p) => p.gas_overlay));
        Assert.Equal(-10f, Dm.initial(typeof(datum_gas_pluoxium), (datum_gas p) => p.fusion_power));
        Assert.StartsWith("An inert noble gas produced by the fusion of Hydrogen and its derivatives. Commonly known",
            (string)Dm.initial(typeof(datum_gas_helium), (datum_gas p) => p.desc));                    // the \ line continuation
    }

    [Fact]
    public void ObjectCreatingDefaultsRunInDmInitVars()
    {
        var organ = new obj_item_organ();
        organ.dm_init_vars();
        Assert.Equal(1, organ.food_reagents.Count);
        Assert.True(Dm.eq(organ.food_reagents[typeof(datum_reagent_consumable_nutriment)], 5));

        var heart = new obj_item_organ_heart();
        heart.dm_init_vars();
        Assert.Equal(2, heart.food_reagents.Count);
        Assert.True(Dm.eq(heart.food_reagents[typeof(datum_reagent_love)], 2.5f));

        var brain = new obj_item_organ_brain();
        brain.dm_init_vars();
        Assert.Equal(new object[] { TRAIT_ADVANCEDTOOLUSER, TRAIT_LITERATE, TRAIT_CAN_STRIP }, Dm.each(brain.organ_traits).Cast<object>().ToArray());
        Assert.Equal(0, brain.traumas.Count);

        var lungs = new obj_item_organ_lungs_smoker_lungs();
        lungs.dm_init_vars();
        Assert.True(Dm.eq(lungs.food_reagents[typeof(datum_reagent_medicine_salbutamol)], 5));
        Assert.Equal(0, lungs.last_partial_pressures.Count);
        Assert.Equal(0, lungs.treat_as.Count);
        Assert.NotSame(lungs.breath_present, lungs.breath_lost);

        var stomach = new obj_item_organ_stomach();
        stomach.dm_init_vars();
        Assert.True(Dm.eq(stomach.food_reagents[typeof(datum_reagent_consumable_nutriment_organ_tissue_stomach_lining)], 5));

        var planetary = new datum_gas_mixture_immutable_planetary();
        planetary.dm_init_vars();
        Assert.Equal(0, planetary.initial_gas.Count);
    }
}
