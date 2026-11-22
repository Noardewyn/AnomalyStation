// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice A (docs/ai/specs/social.md).

public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_say.dm:1
    public override object can_speak__proc(float? allow_mimes = null)
    {
        allow_mimes ??= FALSE;
        // If we're not a nobreath species, and we don't have lungs, we can't talk
        if (Dm.truthy(dna?.species) && !HAS_TRAIT_FROM(this, TRAIT_NOBREATH, SPECIES_TRAIT) && !Dm.truthy(get_organ_slot(ORGAN_SLOT_LUNGS)))
            // How do species that don't breathe, talk? Magic, that's what.
            return FALSE;

        return base.can_speak__proc(allow_mimes);
    }

    // tg: code/modules/mob/living/carbon/carbon_say.dm:9
    public override object could_speak_language(Type language_path = null)
    {
        obj_item_organ_tongue spoken_with = get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (Dm.truthy(spoken_with))
            // the tower of babel needs to bypass the tongue language restrictions without giving omnitongue
            return HAS_MIND_TRAIT(this, TRAIT_TOWER_OF_BABEL) ? TRUE : spoken_with.could_speak_language(language_path);

        return Dm.bitand(Dm.initial(language_path, (datum_language p) => p.flags), LANGUAGE_TONGUELESS_SPEECH);
    }
}
