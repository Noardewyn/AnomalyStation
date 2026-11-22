// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/lizardpeople.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: the lizardperson, the ash walker and the silver scale. The lizard is drawn on the body as tg draws it
// (its greyscale limbs in its mutant colour, its organs' bodypart overlays; docs/ai/specs/replication.md, the species' parts).

// tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:1
public partial class datum_species_lizard : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:51
    public override object randomize_features()
    {
        DmList features = base.randomize_features() as DmList;
        features[FEATURE_LIZARD_MARKINGS] = pick(SSaccessories.feature_list[FEATURE_LIZARD_MARKINGS]);
        return features;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:56
    public override object get_physical_attributes()
    {
        return I18n.T("Lizardpeople can withstand slightly higher temperatures than most species, but they are very vulnerable to the cold and can't regulate their body-temperature internally, making the vacuum of space extremely deadly to them.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:60
    public override object get_species_description()
    {
        return I18n.T("The militaristic Lizardpeople hail originally from Tizira, but have grown throughout their centuries in the stars to possess a large spacefaring empire: though now they must contend with their younger, more technologically advanced Human neighbours.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:66
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("The face of conspiracy theory was changed forever the day mankind met the lizards."),

            I18n.T("Hailing from the arid world of Tizira, lizards were travelling the stars back when mankind was first discovering how neat trains could be. However, much like the space-fable of the space-tortoise and space-hare, lizards have rejected their kin's motto of \"slow and steady\" in favor of resting on their laurels and getting completely surpassed by 'bald apes', due in no small part to their lack of access to plasma."),

            I18n.T("The history between lizards and humans has resulted in many conflicts that lizards ended on the losing side of, with the finale being an explosive remodeling of their moon. Today's lizard-human relations are seeing the continuance of a record period of peace."),

            I18n.T("Lizard culture is inherently militaristic, though the influence the military has on lizard culture begins to lessen the further colonies lie from their homeworld - with some distanced colonies finding themselves subsumed by the cultural practices of other species nearby."),

            I18n.T("On their homeworld, lizards celebrate their 16th birthday by enrolling in a mandatory 5 year military tour of duty. Roles range from combat to civil service and everything in between. As the old slogan goes: \"Your place will be found!\""));
    }

    // Override for the default temperature perks, so we can give our specific "cold blooded" perk.
    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:86
    public override object create_pref_temperature_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, "thermometer-empty"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Cold-blooded")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Lizardpeople have higher tolerance for hot temperatures, but lower tolerance for cold temperatures. Additionally, they cannot self-regulate their body temperature - they are as cold or as warm as the environment around them is. Stay warm!")))));

        return to_add;
    }
}

/*
Lizard subspecies: ASHWALKERS
*/
// tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:104
public partial class datum_species_lizard_ashwalker : datum_species_lizard
{
    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:135
    public override object get_physical_attributes()
    {
        return I18n.T("Ash Walkers are identical to lizardpeople in almost all aspects. Unlike them, they're always digitigrade, they can breathe Lavaland's often noxious atmosphere and resist viruses. They are usually illiterate.");
    }
}

/*
Lizard subspecies: SILVER SCALED
*/
// tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:143
public partial class datum_species_lizard_silverscale : datum_species_lizard
{
    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:164
    public override object get_physical_attributes()
    {
        return I18n.T("Silver Scales are to lizardpeople what angels are to humans. Mostly identical, they are holy, don't breathe, don't get viruses, their hide cannot be pierced, love the taste of wine, and their tongue allows them to turn into a statue, for some reason.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:169
    public override async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        mob_living_carbon_human new_silverscale = human_who_gained_species;
        old_mutcolor = new_silverscale.dna.features[FEATURE_MUTANT_COLOR];
        new_silverscale.dna.features[FEATURE_MUTANT_COLOR] = "#eeeeee";
        await new_silverscale.add_eye_color("#0000a0", EYE_COLOR_SPECIES_PRIORITY);
        await base.on_species_gain(human_who_gained_species, old_species, pref_load, regenerate_icons, replace_missing);
        new_silverscale.add_filter("silver_glint", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#ffffff63"), Dm.kv("size", 2)));
        new_silverscale.damage_resistance += 10;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/lizardpeople.dm:177
    public override async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        mob_living_carbon_human was_silverscale = human;
        was_silverscale.dna.features[FEATURE_MUTANT_COLOR] = old_mutcolor;
        await was_silverscale.remove_eye_color(EYE_COLOR_SPECIES_PRIORITY);
        was_silverscale.remove_filter("silver_glint");
        was_silverscale.damage_resistance -= 10;
        await base.on_species_loss(human, new_species, pref_load, __extra);
    }
}
