// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/android.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI10 (D326): the whole file. The var block is generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/android.dm:1
public partial class datum_species_android : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/android.dm:51
    public override object get_physical_attributes()
        => "Androids are almost, but not quite, identical to fully augmented humans. Unlike those, though, they're completely immune to toxin damage, don't have blood or organs (besides their head), don't get hungry, and can reattach their limbs! That said, an EMP will devastate them and they cannot process any chemicals.";

    // tg: code/modules/mob/living/carbon/human/species_types/android.dm:56
    public override object get_species_description() => "Androids are an entirely synthetic species.";

    // tg: code/modules/mob/living/carbon/human/species_types/android.dm:59
    public override object get_species_lore() => Dm.list("Androids are a synthetic species created by Nanotrasen as an intermediary between humans and cyborgs.");

    // tg: code/modules/mob/living/carbon/human/species_types/android.dm:64
    public override object create_pref_traits_perks()
    {
        DmList perks = Dm.list();
        perks.Add(Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_SHIELD_ALT),
            Dm.kv(SPECIES_PERK_NAME, "Android Aptitude"),
            Dm.kv(SPECIES_PERK_DESC, "As a synthetic lifeform, Androids are immune to many forms of damage humans are susceptible to. Fire, cold, heat, pressure, radiation, and toxins are all ineffective against them. They also can't overdose on drugs, don't need to breathe or eat, can't catch on fire, and are immune to being pierced."))));
        perks.Add(Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_COGS),
            Dm.kv(SPECIES_PERK_NAME, "Modular Lifeform"),
            Dm.kv(SPECIES_PERK_DESC, "Android limbs are modular, allowing them to easily reattach severed bodyparts."))));
        perks.Add(Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_DNA),
            Dm.kv(SPECIES_PERK_NAME, "Not Human After All"),
            Dm.kv(SPECIES_PERK_DESC, "There is no humanity behind the eyes of the Android, and as such, they have no DNA to genetically alter."))));
        return perks;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/android.dm:88
    public override object create_pref_unique_perks()
    {
        DmList perks = Dm.list();
        perks.Add(Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_SHIELD_HEART),
            Dm.kv(SPECIES_PERK_NAME, "Some Components Optional"),
            Dm.kv(SPECIES_PERK_DESC, "Androids have very few internal organs. While they can survive without many of them, they don't have any benefits from them either."))));
        perks.Add(Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_ROBOT),
            Dm.kv(SPECIES_PERK_NAME, "Synthetic"),
            Dm.kv(SPECIES_PERK_DESC, "Being synthetic, Androids are vulnernable to EMPs."))));
        return perks;
    }
}
