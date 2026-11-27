// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/dreams/deaconize_dreamer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dream sect's deaconize rite, which a sect may use up to three times and which refuses a nightmare. The var
// block is generated (D111).

// tg: code/modules/religion/dreams/deaconize_dreamer.dm:1
public partial class datum_religion_rites_deaconize_dreamers : datum_religion_rites_deaconize
{
    // tg: code/modules/religion/dreams/deaconize_dreamer.dm:6
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        if (HAS_TRAIT(potential_deacon, TRAIT_NIGHTMARISH))
        {
            to_chat(user, span_warning(I18n.T("[] is a nightmare, an affront to [] and all they stand for!", potential_deacon, GLOB.deity)));
            return FALSE;
        }
        return await base.invoke_effect(user, religious_tool);
    }

    // tg: code/modules/religion/dreams/deaconize_dreamer.dm:12
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        if (GLOB.religious_sect is not datum_religion_sect_dreams sect)
            return;

        sect.deacon_count += 1;
        if (sect.deacon_count >= sect.max_deacons)
            Dm.remove(ref sect.rites_list, type);
    }
}
