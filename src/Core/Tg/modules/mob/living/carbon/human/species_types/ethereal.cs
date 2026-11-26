// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/ethereal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the ethereal and its subspecies the lustrous. The charge, the crystal core and the glow
// live in their own organs and status effect (stomach_ethereal.cs, heart_ethereal.cs, ethereal_glow.cs). The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:1
public partial class datum_species_ethereal : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:40
    public override async DmTask on_species_gain(mob_living_carbon_human new_ethereal = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(new_ethereal, old_species, pref_load, regenerate_icons, replace_missing);
        if (!ishuman(new_ethereal))
            return;
        obj_item_organ_heart_ethereal ethereal_heart = new_ethereal.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart_ethereal;
        await ethereal_heart.set_ethereal_color(new_ethereal.dna.features[FEATURE_MUTANT_COLOR]);
        // Trigger a refresh as lazy attached limbs do not cause those
        datum_status_effect_grouped_bodypart_effect_ethereal_glow glow_status = new_ethereal.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_ethereal_glow)) as datum_status_effect_grouped_bodypart_effect_ethereal_glow;
        if (glow_status != null)
            glow_status.refresh_light_color();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:51
    public override object randomize_features()
    {
        DmList features = base.randomize_features() as DmList;
        features[FEATURE_MUTANT_COLOR] = GLOB.color_list_ethereal[pick(GLOB.color_list_ethereal)];
        return features;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:56
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human human = null)
    {
        human.dna.features[FEATURE_MUTANT_COLOR] = GLOB.color_list_ethereal["Green"];
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:59
    public override object get_features()
    {
        DmList features = base.get_features() as DmList;
        // tg appends to the cached list on every call; the key is added once here
        if (!Dm.@in("feature_ethcolor", features))
            Dm.append(ref features, "feature_ethcolor");
        return features;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:64
    public override object get_physical_attributes()
    {
        return I18n.T("Ethereals process electricity as their power supply, not food, and are somewhat resistant to it. They do so via their crystal core, their equivalent of a human heart, which will also encase them in a reviving crystal if they die. However, their skin is very thin and easy to pierce with brute weaponry.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:69
    public override object get_species_description()
    {
        return I18n.T("Coming from the planet of Sprout, the theocratic ethereals are separated socially by caste, and espouse a dogma of aiding the weak and downtrodden.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:74
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Ethereals are a species native to the planet Sprout. When they were originally discovered, they were at a medieval level of technological progression, but due to their natural acclimation with electricity, they felt easy among the large Nanotrasen installations."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:81
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "bolt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Shockingly Tasty")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ethereals can feed on electricity from APCs, and do not otherwise need to eat."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "lightbulb"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Disco Ball")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ethereals passively generate their own light."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "gem"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Crystal Core")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("The Ethereal's heart will encase them in crystal should they die, returning them to life after a time - at the cost of a permanent brain trauma."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "fist-raised"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Elemental Attacker")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ethereals deal burn damage with their punches instead of brute."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "biohazard"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Starving Artist")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ethereals take toxin damage while starving.")))));

        return to_add;
    }
}

//Ethereal pirates with an inherent bluespace prophet trauma.
// tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:121
public partial class datum_species_ethereal_lustrous : datum_species_ethereal
{
    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:145
    public override object get_physical_attributes()
    {
        return I18n.T("Lustrous are what remains of an Ethereal after freebasing esoteric drugs. They are pressure immune, virus immune, can see bluespace tears in reality, and have a really weird scream. They remain vulnerable to physical damage.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ethereal.dm:149
    public override object randomize_features()
    {
        DmList features = base.randomize_features() as DmList;
        features[FEATURE_MUTANT_COLOR] = GLOB.color_list_lustrous[pick(GLOB.color_list_lustrous)];
        return features;
    }
}
