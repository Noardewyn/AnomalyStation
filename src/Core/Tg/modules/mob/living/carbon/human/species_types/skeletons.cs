// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/skeletons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the skeleton. Its traits, bone organs, bone limbs and the milk it is healed by are generated or live
// already (the bone organs, the skeleton language holder, the milk reagent); here are the roundstart check and its pref texts.

// tg: code/modules/mob/living/carbon/human/species_types/skeletons.dm:3
public partial class datum_species_skeleton : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/skeletons.dm:48
    public override object check_roundstart_eligible()
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/skeletons.dm:53
    public override object get_physical_attributes()
    {
        return I18n.T("These humerus folk lack any fleshy biology, which allows them to resist pressure, temperature, radiation, asphyxiation and even toxins. However, due to that same fact, it is quite hard to heal them as well. The calcium found in common space milk is highly effective at treating their wounds. Their limbs are easy to pop off their joints, but they can somehow just slot them back in.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/skeletons.dm:58
    public override object get_species_description()
    {
        return I18n.T("A rattling skeleton! They descend upon Space Station 13 Every year to spook the crew! \"I've got a BONE to pick with you!\"");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/skeletons.dm:62
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Skeletons want to be feared again! Their presence in media has been destroyed, or at least that's what they firmly believe. They're always the first thing fought in an RPG, they're Flanderized into pun rolling JOKES, and it's really starting to get to them. You could say they're deeply RATTLED. Hah."));
    }
}
