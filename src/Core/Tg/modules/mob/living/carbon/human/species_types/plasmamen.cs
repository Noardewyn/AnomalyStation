// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/plasmamen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the whole file: the plasmaman, who spawns in his job's envirosuit (or, with no job outfit, the species' life-saving
// outfit); the burning without it is the plasma_based bodypart effect. The var block is generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/plasmamen.dm:2
public partial class datum_species_plasmaman : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/plasmamen.dm:72
    public override async DmTask pre_equip_species_outfit(datum_job job = null, mob_living_carbon_human equipping = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (job?.plasmaman_outfit != null)
            await equipping.equipOutfit(job.plasmaman_outfit, visuals_only);
        else
            await give_important_for_life(equipping);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/plasmamen.dm:78
    public override object get_physical_attributes()
    {
        return I18n.T("Plasmamen literally breathe and live plasma. They spontaneously combust on contact with oxygen, and besides all the quirks that go with that, they're very vulnerable to all kinds of physical damage due to their brittle structure.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/plasmamen.dm:82
    public override object get_species_description()
    {
        return I18n.T("Found on the Icemoon of Freyja, plasmamen consist of colonial fungal organisms which together form a sentient being. In human space, they're usually attached to skeletons to afford a human touch.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/plasmamen.dm:87
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("A confusing species, plasmamen are truly \"a fungus among us\". What appears to be a singular being is actually a colony of millions of organisms surrounding a found (or provided) skeletal structure."),
            I18n.T("Originally discovered by NT when a researcher fell into an open tank of liquid plasma, the previously unnoticed fungal colony overtook the body creating the first \"true\" plasmaman. The process has since been streamlined via generous donations of convict corpses and plasmamen have been deployed en masse throughout NT to bolster the workforce."),
            I18n.T("New to the galactic stage, plasmamen are a blank slate. Their appearance, generally regarded as \"ghoulish\", inspires a lot of apprehension in their crewmates. It might be the whole \"flammable purple skeleton\" thing."),
            I18n.T("The colonids that make up plasmamen require the plasma-rich atmosphere they evolved in. Their psuedo-nervous system runs with externalized electrical impulses that immediately ignite their plasma-based bodies when oxygen is present."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/plasmamen.dm:106
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "user-shield"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Protected")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Plasmamen are immune to radiation, poisons, and most diseases."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "bone"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Wound Resistance")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Plasmamen have higher tolerance for damage that would wound others."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "wind"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Plasma Healing")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Plasmamen can heal wounds by consuming plasma."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "hard-hat"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Protective Helmet")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Plasmamen's helmets provide them shielding from the flashes of welding, as well as an inbuilt flashlight."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "fire"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Living Torch")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Plasmamen instantly ignite when their body makes contact with oxygen."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "briefcase-medical"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Complex Biology")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Plasmamen take specialized medical knowledge to be treated. Do not expect speedy revival, if you are lucky enough to get one at all.")))));

        return to_add;
    }
}
