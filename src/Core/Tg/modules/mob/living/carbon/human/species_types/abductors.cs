// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/abductors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the abductor species (the grey's limbs, brain and tongue). The abductor team, its gear and the
// consoles are the antagonist side and stay out (D333). The var block is generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/abductors.dm:2
public partial class datum_species_abductor : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/abductors.dm:32
    public override object get_physical_attributes()
    {
        return I18n.T("Abductors do not need to breathe, eat, do not have blood, a heart, stomach, or lungs and cannot be infected by human viruses. Their hardy physique prevents their skin from being wounded or dismembered, but their chunky tridactyl hands make it hard to operate human equipment.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/abductors.dm:36
    public override object get_species_description()
    {
        return I18n.T("Abductors, colloquially known as \"Greys\" (or \"Grays\"), are, three fingered, pale skinned inquisitive aliens who can't communicate well to the average crew-member.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/abductors.dm:40
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Little are known about Abductors. While they (as a species) have been known to abduct other species of 'lesser intellect' for experimentation, some have been known to - on rare occasions - work with the very species they abduct, for reasons unknown."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/abductors.dm:47
    public override object create_pref_traits_perks()
    {
        DmList perks = Dm.list();
        Dm.append(ref perks, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_WIND),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Lungs Optional")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Abductors don't need to breathe, though exposure to a vacuum is still a hazard.")))));
        Dm.append(ref perks, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_SHIELD),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Resilient Skin")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("The grey (or gray) skin of an Abductor is tough and resistant. They cannot be wounded or dismembered by conventional means.")))));
        return perks;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/abductors.dm:64
    public override object create_pref_unique_perks()
    {
        DmList perks = Dm.list();
        Dm.append(ref perks, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_SYRINGE),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Disease Immunity")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Abductors are immune to all viral infections found naturally on the station.")))));
        Dm.append(ref perks, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK), // It may be a stretch to call nohunger a neutral perk but the Abductor's tongue describes it as much, so.
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_UTENSILS),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Hungry for Knowledge")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Abductors have a greater hunger for knowledge than food, and as such don't need to eat. Which is fortunate, as their speech matrix prevents them from consuming food.")))));
        Dm.append(ref perks, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_VOLUME_XMARK),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Superlingual Matrix")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Abductors cannot physically speak with their natural tongue. They intead naturally communicate telepathically to other Abductors, a process which all other species cannot hear. Great for secret conversations, not so great for ordering something from the bar.")))));
        Dm.append(ref perks, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_HANDSHAKE_SLASH),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Tridactyl Hands")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Abductor hands are not designed for human equipment. Utilizing the station's equipment is difficult for them.")))));
        return perks;
    }
}
