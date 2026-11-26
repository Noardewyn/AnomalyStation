// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/ethereal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the ethereal's colour in Character Setup. The choices' pictures reach the client as /icon
// recipes (DV44, universal_icon.cs).

// tg: code/modules/client/preferences/species_features/ethereal.dm:1
public partial class datum_preference_choiced_ethereal_color : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/ethereal.dm:8
    public override object has_relevant_feature(datum_preferences preferences = null)
    {
        // Skips checks for relevant_organ, relevant trait etc. because ethereal color is tied directly to species (atm)
        return current_species_has_savekey(preferences);
    }

    // tg: code/modules/client/preferences/species_features/ethereal.dm:12
    public override DmList init_possible_values()
    {
        return assoc_to_keys(GLOB.color_list_ethereal) as DmList;
    }

    // tg: code/modules/client/preferences/species_features/ethereal.dm:15
    public override object icon_for(object value = null)
    {
        // var/static/datum/universal_icon/ethereal_base: made each call (an /icon is a short list of operations)
        icon ethereal_base = Dm.icon(Dm.resource("icons/mob/human/species/ethereal/bodyparts.dmi"), "ethereal_head");
        ethereal_base.Blend(Dm.icon(Dm.resource("icons/mob/human/species/ethereal/bodyparts.dmi"), "ethereal_chest"), ICON_OVERLAY);
        ethereal_base.Blend(Dm.icon(Dm.resource("icons/mob/human/species/ethereal/bodyparts.dmi"), "ethereal_l_arm"), ICON_OVERLAY);
        ethereal_base.Blend(Dm.icon(Dm.resource("icons/mob/human/species/ethereal/bodyparts.dmi"), "ethereal_r_arm"), ICON_OVERLAY);

        icon eyes = Dm.icon(Dm.resource("icons/mob/human/human_eyes.dmi"), "eyes_l");
        eyes.Blend(Dm.icon(Dm.resource("icons/mob/human/human_eyes.dmi"), "eyes_r"), ICON_OVERLAY);
        eyes.Blend(COLOR_BLACK, ICON_MULTIPLY);
        ethereal_base.Blend(eyes, ICON_OVERLAY);

        ethereal_base.Scale(64, 64);
        ethereal_base.Crop(15, 64 - 31, 15 + 31, 64);

        icon final_icon = Dm.icon(ethereal_base);
        final_icon.Blend(GLOB.color_list_ethereal[value], ICON_MULTIPLY);
        return final_icon;
    }

    // tg: code/modules/client/preferences/species_features/ethereal.dm:35
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (Dm.truthy(current_species_has_savekey(preferences)))
            target.dna.features[FEATURE_MUTANT_COLOR] = GLOB.color_list_ethereal[value];
    }
}
