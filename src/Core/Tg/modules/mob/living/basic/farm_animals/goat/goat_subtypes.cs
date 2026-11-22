// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/goat/goat_subtypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/goat/goat_subtypes.dm:2
public partial class mob_living_basic_goat_pete : mob_living_basic_goat // Pete!
{
    // tg: code/modules/mob/living/basic/farm_animals/goat/goat_subtypes.dm:6
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        area goat_area = get_area(this) as area;
        if ((Dm.num(bodytemperature) < T20C) || goat_area is area_station_service_kitchen_coldroom)
            Dm.append(ref dot, span_notice(Dm.text("[] []n't seem to be too bothered about the cold.", p_They(), p_do()))); // special for pete
        return dot;
    }
}
