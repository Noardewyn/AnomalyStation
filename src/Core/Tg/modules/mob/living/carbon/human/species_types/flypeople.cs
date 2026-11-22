// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/flypeople.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps TP1, D333: the teleporter's uncalibrated jump turns a man into one): the flypeople, with the fly organs and
// bodyparts of the DNA infuser's fly set (live since gaps P3b). The var block is generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/flypeople.dm:1
public partial class datum_species_fly : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/flypeople.dm:29
    public override object get_physical_attributes()
    {
        return I18n.T("These hideous creatures suffer from pesticide immensely, eat waste, and are incredibly vulnerable to bright lights. They do have wings though.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/flypeople.dm:32
    public override object get_species_description()
    {
        return I18n.T("With no official documentation or knowledge of the origin of this species, they remain a mystery to most. Any and all rumours among Nanotrasen staff regarding flypeople are often quickly silenced by high ranking staff or officials.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/flypeople.dm:38
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Flypeople are a curious species with a striking resemblance to the insect order of Diptera, commonly known as flies. With no publically known origin, flypeople are rumored to be a side effect of bluespace travel, despite statements from Nanotrasen officials."),
            I18n.T("Little is known about the origins of this race, however they posess the ability to communicate with giant spiders, originally discovered in the Australicus sector and now a common occurence in black markets as a result of a breakthrough in syndicate bioweapon research."),
            I18n.T("Flypeople are often feared or avoided among other species, their appearance often described as unclean or frightening in some cases, and their eating habits even more so with an insufferable accent to top it off."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/flypeople.dm:54
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "grin-tongue"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Uncanny Digestive System")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Flypeople regurgitate their stomach contents and drink it off the floor to eat and drink with little care for taste, favoring gross foods."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "fist-raised"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Insectoid Biology")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Fly swatters will deal significantly higher amounts of damage to a Flyperson."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "sun"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Radial Eyesight")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Flypeople can be flashed from all angles."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "briefcase-medical"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Weird Organs")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Flypeople take specialized medical knowledge to be treated. Their organs are disfigured and organ manipulation can be interesting...")))));

        return to_add;
    }
}
