// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/empulse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 EMP slice (D162): tg's electromagnetic pulse. The whole file is translated.

public static partial class GlobalProcs
{
    // tg: code/game/objects/empulse.dm:1
    public static async DmTask<object> empulse(object epicenter = null, object heavy_range = null, object light_range = null, object emp_source = null)
    {
        if (!Dm.truthy(epicenter))
            return null;

        if (!isturf(epicenter))
            epicenter = get_turf(((atom)epicenter).loc);

        if (Dm.truthy(emp_source))
        {
            message_admins(Dm.text("EMP with size ([], []) in [], caused by [] ", heavy_range, light_range, ADMIN_VERBOSEJMP(epicenter as atom), emp_source));
            log_game(Dm.text("EMP with size ([], []) in [], caused by [] ", heavy_range, light_range, ADMIN_VERBOSEJMP(epicenter as atom), emp_source));
        }

        if (Dm.num(heavy_range) > 1)
            Dm.@new<obj_effect_temp_visual_emp_pulse>(epicenter);

        if (Dm.num(heavy_range) > Dm.num(light_range))
            light_range = heavy_range;

        foreach (atom T in Dm.each<atom>(spiral_range(Dm.num(light_range), epicenter)))
        {
            float distance = get_dist(epicenter, T);
            if (distance < 0)
                distance = 0;
            if (distance < Dm.num(heavy_range))
            {
                await T.emp_act(EMP_HEAVY);
            }
            else if (distance == Dm.num(heavy_range))
            {
                if (prob(50))
                    await T.emp_act(EMP_HEAVY);
                else
                    await T.emp_act(EMP_LIGHT);
            }
            else if (distance <= Dm.num(light_range))
            {
                await T.emp_act(EMP_LIGHT);
            }
        }
        return 1;
    }
}
