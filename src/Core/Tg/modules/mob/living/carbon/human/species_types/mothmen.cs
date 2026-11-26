// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/mothmen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the whole file: the mothman. His wings, antennae, markings and organs are live parts (organs/external/wings, the
// moth bodyparts); the var block is generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/mothmen.dm:2
public partial class datum_species_moth : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/mothmen.dm:35
    public override object randomize_features()
    {
        DmList features = base.randomize_features() as DmList;
        features[FEATURE_MOTH_MARKINGS] = pick(SSaccessories.feature_list[FEATURE_MOTH_MARKINGS]);
        return features;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mothmen.dm:40
    public override object get_physical_attributes()
    {
        return I18n.T("Moths have large and fluffy wings, which help them navigate the station if gravity is offline by pushing the air around them. Due to that, it isn't of much use out in space. Their eyes are very sensitive.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mothmen.dm:44
    public override object get_species_description()
    {
        return I18n.T("Hailing from a planet that was lost long ago, the moths travel the galaxy as a nomadic people aboard a colossal fleet of ships, seeking a new homeland.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mothmen.dm:48
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Their homeworld lost to the ages, the moths live aboard the Grand Nomad Fleet. Made up of what could be found, bartered, repaired, or stolen the armada is a colossal patchwork built on a history of politely flagging travelers down and taking their things. Occasionally a moth will decide to leave the fleet, usually to strike out for fortunes to send back home."),
            I18n.T("Nomadic life produces a tight-knit culture, with moths valuing their friends, family, and vessels highly. Moths are gregarious by nature and do best in communal spaces. This has served them well on the galactic stage, maintaining a friendly and personable reputation even in the face of hostile encounters. It seems that the galaxy has come to accept these former pirates."),
            I18n.T("Surprisingly, living together in a giant fleet hasn't flattened variance in dialect and culture. These differences are welcomed and encouraged within the fleet for the variety that they bring."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mothmen.dm:64
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "feather-alt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Precious Wings")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Moths can fly in pressurized, zero-g environments and safely land short falls using their wings."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "tshirt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Meal Plan")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Moths can eat clothes for temporary nourishment."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "fire"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Ablazed Wings")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Moth wings are fragile, and can be easily burnt off."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "sun"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Bright Lights")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Moths need an extra layer of flash protection to protect themselves, such as against security officers or when welding. Welding masks will work.")))));

        return to_add;
    }
}
