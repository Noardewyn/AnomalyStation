// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/adult/life.dm:3
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/life.dm:3
    public virtual object adjust_body_temperature(object current = null, object loc_temp = null, object boost = null)
    {
        float temperature = Dm.num(current);
        float difference = abs(Dm.num(current) - Dm.num(loc_temp)); //get difference
        float increments = 0;// = difference/10 //find how many increments apart they are
        if (difference > 50)
            increments = difference / 5;
        else
            increments = difference / 10;
        float change = increments * Dm.num(boost); // Get the amount to change by (x per increment)
        float temp_change = 0;
        if (Dm.num(current) < Dm.num(loc_temp))
            temperature = min(Dm.num(loc_temp), temperature + change);
        else if (Dm.num(current) > Dm.num(loc_temp))
            temperature = max(Dm.num(loc_temp), temperature - change);
        temp_change = (temperature - Dm.num(current));
        return temp_change;
    }
}
