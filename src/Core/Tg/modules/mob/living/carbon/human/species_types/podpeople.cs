// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/podpeople.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the podperson. Its light-fed healing is the photosynthesis its limbs add
// (bodypart_effect.cs). The var block is generated (D111).

// A mutation caused by a human being ressurected in a revival pod. These regain health in light, and begin to wither in darkness.
// tg: code/modules/mob/living/carbon/human/species_types/podpeople.dm:1
public partial class datum_species_pod : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/podpeople.dm:43
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human human = null)
    {
        human.dna.features[FEATURE_MUTANT_COLOR] = "#886600";
        human.dna.features[FEATURE_POD_HAIR] = "Rose";
        await human.update_body(TRUE); // update_body(is_creating = TRUE)
    }

    // tg: code/modules/mob/living/carbon/human/species_types/podpeople.dm:48
    public override object get_physical_attributes()
    {
        return I18n.T("Podpeople are in many ways the inverse of shadows, healing in light and starving with the dark. Their bodies are like tinder and easy to char.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/podpeople.dm:52
    public override object get_species_description()
    {
        return I18n.T("Podpeople are largely peaceful plant based lifeforms, resembling a humanoid figure made of leaves, flowers, and vines.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/podpeople.dm:55
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Not much is known about the origins of the Podpeople. Many assume them to be the result of a long forgotten botanical experiment, slowly mutating for years on years until they became the beings they are today. Ever since they were uncovered long ago, their kind have been found on board stations and planets across the galaxy, often working in hydroponics bays, kitchens, or science departments, working with plants and other botanical lifeforms."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/podpeople.dm:63
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, "lightbulb"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Photosynthetic")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("As long as you are conscious, and within a well-lit area, you will slowly heal brute, burn, toxin and oxygen damage and gain nutrition - and never get fat! However, if you are LOW on nutrition, you will progressively take brute damage until you die or enter the light once more.")))));

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "biohazard"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Weedkiller Susceptability")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Being a floral life form, you are susceptable to anti-florals and will take extra toxin damage from it!")))));

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "briefcase-medical"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Semi-Complex Biology")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Your biology is extremely complex, making ordinary health scanners unable to scan you. Make sure the doctor treating you either has a plant analyzer or a advanced health scanner!")))));

        return to_add;
    }
}
