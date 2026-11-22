// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/food/chilling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the chill of ice cream from the vat and of the frozen treats. The var blocks are generated (D111).

///food effect applied by ice cream and frozen treats
// tg: code/datums/status_effects/buffs/food/chilling.dm:2
public partial class datum_status_effect_food_chilling : datum_status_effect_food
{
    // tg: code/datums/status_effects/buffs/food/chilling.dm:5
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float minimum_temp = BODYTEMP_HEAT_DAMAGE_LIMIT - 12 * Dm.num(strength);
        if (owner.bodytemperature >= minimum_temp)
            owner.adjust_bodytemperature(-2.75f * Dm.num(strength) * Dm.num(seconds_between_ticks), min_temp: minimum_temp);
        return null;
    }
}
