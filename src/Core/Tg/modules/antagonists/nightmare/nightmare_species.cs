// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nightmare/nightmare_species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the nightmare species, a highly aggressive subset of the shadowpeople. The var block is generated (D111).

/**
 * A highly aggressive subset of shadowlings
 */
// tg: code/modules/antagonists/nightmare/nightmare_species.dm:5
public partial class datum_species_shadow_nightmare : datum_species_shadow
{
    // tg: code/modules/antagonists/nightmare/nightmare_species.dm:38
    public override async DmTask on_species_gain(mob_living_carbon_human C = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(C, old_species, pref_load, regenerate_icons, replace_missing);

        await C.fully_replace_character_name(null, pick(GLOB.nightmare_names));
    }

    // tg: code/modules/antagonists/nightmare/nightmare_species.dm:43
    public override object check_roundstart_eligible()
    {
        return FALSE;
    }
}
